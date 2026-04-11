using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.YtdlpThemeSongs;

public class ThemeSongsManager : IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILogger<ThemeSongsManager> _logger;

    public ThemeSongsManager(
        ILibraryManager libraryManager,
        IMediaEncoder mediaEncoder,
        ILogger<ThemeSongsManager> logger)
    {
        _libraryManager = libraryManager;
        _mediaEncoder = mediaEncoder;
        _logger = logger;
    }

    public async Task DownloadAllThemeSongsAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var series = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Series],
            IsVirtualItem = false,
            Recursive = true,
        }).OfType<Series>().ToList();

        var movies = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie],
            IsVirtualItem = false,
            Recursive = true,
        }).OfType<Movie>().ToList();

        int total = series.Count + movies.Count;
        int completed = 0;

        foreach (var serie in series)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessSeriesAsync(serie, cancellationToken).ConfigureAwait(false);
            completed++;
            progress?.Report(total > 0 ? 100.0 * completed / total : 100.0);
        }

        foreach (var movie in movies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessMovieAsync(movie, cancellationToken).ConfigureAwait(false);
            completed++;
            progress?.Report(total > 0 ? 100.0 * completed / total : 100.0);
        }
    }

    private async Task ProcessSeriesAsync(Series series, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;

        var seriesThemePath = Path.Combine(series.Path, "theme.mp3");
        if (!File.Exists(seriesThemePath))
        {
            var query = BuildQuery(
                config?.TvSeriesSearchQuery ?? "{title} TV series official theme song",
                series.Name,
                series.ProductionYear?.ToString() ?? string.Empty,
                seasonNumber: null,
                seriesTitle: null);
            await DownloadThemeSongAsync(query, series.Path, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Skipping series {Name}: theme.mp3 already exists", series.Name);
        }

        var seasons = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Season],
            ParentId = series.Id,
            IsVirtualItem = false,
        }).OfType<Season>().ToList();

        foreach (var season in seasons)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (season.IndexNumber == 0)
            {
                continue; // Skip Specials
            }

            if (string.IsNullOrEmpty(season.Path))
            {
                continue;
            }

            var seasonThemePath = Path.Combine(season.Path, "theme.mp3");
            if (!File.Exists(seasonThemePath))
            {
                var query = BuildQuery(
                    config?.TvSeasonSearchQuery ?? "{seriesTitle} Season {seasonNumber} theme song",
                    series.Name,
                    series.ProductionYear?.ToString() ?? string.Empty,
                    season.IndexNumber?.ToString() ?? string.Empty,
                    series.Name);
                await DownloadThemeSongAsync(query, season.Path, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug(
                    "Skipping {SeriesName} Season {Season}: theme.mp3 already exists",
                    series.Name,
                    season.IndexNumber);
            }
        }
    }

    private async Task ProcessMovieAsync(Movie movie, CancellationToken cancellationToken)
    {
        var movieDir = Path.GetDirectoryName(movie.Path);
        if (string.IsNullOrEmpty(movieDir))
        {
            _logger.LogWarning("Could not determine directory for movie {Name}", movie.Name);
            return;
        }

        var themePath = Path.Combine(movieDir, "theme.mp3");
        if (File.Exists(themePath))
        {
            _logger.LogDebug("Skipping movie {Name}: theme.mp3 already exists", movie.Name);
            return;
        }

        var config = Plugin.Instance?.Configuration;
        var query = BuildQuery(
            config?.MovieSearchQuery ?? "{title} {year} official movie theme song",
            movie.Name,
            movie.ProductionYear?.ToString() ?? string.Empty,
            seasonNumber: null,
            seriesTitle: null);
        await DownloadThemeSongAsync(query, movieDir, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildQuery(
        string template,
        string title,
        string year,
        string? seasonNumber,
        string? seriesTitle)
    {
        return template
            .Replace("{title}", title, StringComparison.OrdinalIgnoreCase)
            .Replace("{year}", year, StringComparison.OrdinalIgnoreCase)
            .Replace("{seasonNumber}", seasonNumber ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{seriesTitle}", seriesTitle ?? title, StringComparison.OrdinalIgnoreCase);
    }

    private async Task DownloadThemeSongAsync(string query, string outputDir, CancellationToken cancellationToken)
    {
        var ytDlp = ResolveYtDlp();
        if (ytDlp == null)
        {
            _logger.LogError("yt-dlp not found. Skipping theme song download for {OutputDir}", outputDir);
            return;
        }

        var guid = Guid.NewGuid().ToString("N");
        var tempDir = Path.GetTempPath();
        var tempTemplate = Path.Combine(tempDir, $"theme_{guid}.%(ext)s");

        var args = new[]
        {
            "--default-search", "ytsearch",
            "--extract-audio",
            "--audio-format", "mp3",
            "--audio-quality", "0",
            "--playlist-items", "1",
            "--no-write-thumbnail",
            "--no-playlist",
            "--output", tempTemplate,
            "--match-filter", "duration < 480",
            "--sponsorblock-remove", "sponsor,intro,outro,interaction,preview,selfpromo,filler,music_offtopic",
            $"ytsearch3:{query}"
        };

        _logger.LogInformation(
            "Downloading theme song for {OutputDir} with query: {Query}",
            outputDir,
            query);

        var (exitCode, _, stderr) = await RunProcessAsync(ytDlp, args, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            _logger.LogWarning(
                "yt-dlp exited with code {ExitCode} for query: {Query}. Stderr: {Stderr}",
                exitCode,
                query,
                stderr);
            return;
        }

        var tempFiles = Directory.GetFiles(tempDir, $"theme_{guid}*.mp3");
        if (tempFiles.Length == 0)
        {
            _logger.LogWarning("yt-dlp completed but no .mp3 file found for query: {Query}", query);
            return;
        }

        var downloadedFile = tempFiles[0];
        var processedFile = Path.Combine(tempDir, $"theme_{guid}_processed.mp3");

        try
        {
            await PostProcessAsync(downloadedFile, processedFile, cancellationToken).ConfigureAwait(false);

            var destPath = Path.Combine(outputDir, "theme.mp3");
            File.Move(processedFile, destPath, overwrite: true);
            _logger.LogInformation("Theme song saved to {DestPath}", destPath);
        }
        finally
        {
            foreach (var f in tempFiles)
            {
                try { File.Delete(f); } catch { }
            }

            if (File.Exists(processedFile))
            {
                try { File.Delete(processedFile); } catch { }
            }
        }
    }

    private async Task PostProcessAsync(string inputMp3, string outputMp3, CancellationToken cancellationToken)
    {
        var ffmpeg = _mediaEncoder.EncoderPath;

        // Pass 1: loudnorm analysis
        var pass1Args = new[]
        {
            "-i", inputMp3,
            "-af", "loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json",
            "-f", "null",
            "/dev/null"
        };

        var (_, _, stderr1) = await RunProcessAsync(ffmpeg, pass1Args, cancellationToken).ConfigureAwait(false);

        var jsonMatch = Regex.Match(stderr1, @"\{[^{}]*""input_i""[^{}]*\}", RegexOptions.Singleline);
        if (!jsonMatch.Success)
        {
            _logger.LogWarning("Could not parse loudnorm JSON from ffmpeg output. Using simple copy.");
            File.Copy(inputMp3, outputMp3, overwrite: true);
            return;
        }

        LoudnormStats stats;
        try
        {
            stats = JsonSerializer.Deserialize<LoudnormStats>(jsonMatch.Value)
                    ?? throw new InvalidOperationException("Deserialized null");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize loudnorm stats. Using simple copy.");
            File.Copy(inputMp3, outputMp3, overwrite: true);
            return;
        }

        // Pass 2: silence removal + normalized output
        var audioFilter =
            $"silenceremove=start_periods=1:start_silence=0.1:start_threshold=-50dB," +
            $"loudnorm=I=-16:TP=-1.5:LRA=11:" +
            $"measured_I={stats.InputI}:measured_TP={stats.InputTp}:" +
            $"measured_LRA={stats.InputLra}:measured_thresh={stats.InputThresh}:" +
            $"offset={stats.TargetOffset}:linear=true";

        var pass2Args = new[]
        {
            "-i", inputMp3,
            "-af", audioFilter,
            outputMp3
        };

        var (exitCode2, _, _) = await RunProcessAsync(ffmpeg, pass2Args, cancellationToken).ConfigureAwait(false);
        if (exitCode2 != 0)
        {
            _logger.LogWarning("ffmpeg pass 2 failed (exit {ExitCode}). Using simple copy.", exitCode2);
            File.Copy(inputMp3, outputMp3, overwrite: true);
        }
    }

    private string? ResolveYtDlp()
    {
        var configured = Plugin.Instance?.Configuration?.YtDlpPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var commonPaths = new[]
        {
            "/usr/bin/yt-dlp",
            "/usr/local/bin/yt-dlp",
            "/opt/homebrew/bin/yt-dlp",
            "/root/.local/bin/yt-dlp",
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo("which", "yt-dlp")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            if (!string.IsNullOrEmpty(output) && File.Exists(output))
            {
                return output;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "which yt-dlp failed");
        }

        _logger.LogError("yt-dlp not found in configured path, common paths, or PATH");
        return null;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        string exe,
        string[] args,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return (process.ExitCode, stdout, stderr);
    }

    public void Dispose() { }

    private sealed class LoudnormStats
    {
        [JsonPropertyName("input_i")]
        public string InputI { get; set; } = "0";

        [JsonPropertyName("input_tp")]
        public string InputTp { get; set; } = "0";

        [JsonPropertyName("input_lra")]
        public string InputLra { get; set; } = "0";

        [JsonPropertyName("input_thresh")]
        public string InputThresh { get; set; } = "0";

        [JsonPropertyName("target_offset")]
        public string TargetOffset { get; set; } = "0";
    }
}
