using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.YtdlpThemeSongs.ScheduledTasks;

public class DownloadThemeSongsTask : IScheduledTask
{
    private readonly ILogger<ThemeSongsManager> _logger;
    private readonly ThemeSongsManager _themeSongsManager;

    public DownloadThemeSongsTask(
        ILibraryManager libraryManager,
        IMediaEncoder mediaEncoder,
        ILogger<ThemeSongsManager> logger)
    {
        _logger = logger;
        _themeSongsManager = new ThemeSongsManager(libraryManager, mediaEncoder, logger);
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting theme song download task");
        await _themeSongsManager.DownloadAllThemeSongsAsync(progress, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Theme song download task completed");
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(24).Ticks
        };
    }

    public string Name => "Download Theme Songs";
    public string Key => "DownloadThemeSongs";
    public string Description => "Downloads theme songs for TV series, seasons, and movies using yt-dlp";
    public string Category => "Theme Songs";
}
