using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.YtdlpThemeSongs.Api
{
    /// <summary>
    /// The Theme Songs api controller.
    /// </summary>
    [ApiController]
    [Route("ThemeSongs")]
    [Produces(MediaTypeNames.Application.Json)]
    public class ThemeSongsController : ControllerBase
    {
        private readonly ThemeSongsManager _themeSongsManager;
        private readonly ILogger<ThemeSongsManager> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ThemeSongsController"/>.
        /// </summary>
        public ThemeSongsController(
            ILibraryManager libraryManager,
            IMediaEncoder mediaEncoder,
            ILogger<ThemeSongsManager> logger)
        {
            _themeSongsManager = new ThemeSongsManager(libraryManager, mediaEncoder, logger);
            _logger = logger;
        }

        /// <summary>
        /// Downloads theme songs for all TV series, seasons, and movies.
        /// </summary>
        /// <response code="204">Theme song download started successfully.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("DownloadAll")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> DownloadAllRequest()
        {
            _logger.LogInformation("Downloading all theme songs");
            await _themeSongsManager.DownloadAllThemeSongsAsync(null!, CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("Theme song download completed");
            return NoContent();
        }
    }
}
