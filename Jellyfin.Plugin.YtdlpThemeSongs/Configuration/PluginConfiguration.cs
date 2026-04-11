using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.YtdlpThemeSongs.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        // set default options here
        TvSeriesSearchQuery = "{title} TV series official theme song";
        TvSeasonSearchQuery = "{seriesTitle} Season {seasonNumber} theme song";
        MovieSearchQuery = "{title} {year} official movie theme song";
        YtDlpPath = string.Empty;
    }

    public string TvSeriesSearchQuery { get; set; }
    public string TvSeasonSearchQuery { get; set; }
    public string MovieSearchQuery { get; set; }
    public string YtDlpPath { get; set; }
}
