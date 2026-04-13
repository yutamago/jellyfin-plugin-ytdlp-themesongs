using System;
using System.IO;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.YtdlpThemeSongs.Api
{
    public record RandomItemDto(string Id, string DisplayName, string Query);

    public record DeleteResultDto(int DeletedCount);

    /// <summary>
    /// The Theme Songs api controller.
    /// </summary>
    [ApiController]
    [Route("ThemeSongs")]
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
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> DownloadAllRequest()
        {
            _logger.LogInformation("Downloading all theme songs");
            await _themeSongsManager.DownloadAllThemeSongsAsync(null!, CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("Theme song download completed");
            return NoContent();
        }

        /// <summary>
        /// Returns a random library item of the given type with a pre-built search query.
        /// </summary>
        /// <param name="type">Item type: "series", "season", or "movie".</param>
        /// <response code="200">A random item was found.</response>
        /// <response code="404">No items of the requested type exist in the library.</response>
        [HttpGet("RandomItem")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(RandomItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<RandomItemDto> GetRandomItem([FromQuery] string type)
        {
            RandomItemResult? result = type switch
            {
                "series" => _themeSongsManager.GetRandomSeries(),
                "season" => _themeSongsManager.GetRandomSeason(),
                "movie"  => _themeSongsManager.GetRandomMovie(),
                _        => null,
            };

            if (result == null)
            {
                return NotFound();
            }

            return Ok(new RandomItemDto(result.Id, result.DisplayName, result.Query));
        }

        /// <summary>
        /// Downloads a theme song for a single item and streams log events via Server-Sent Events.
        /// </summary>
        /// <param name="itemId">Jellyfin item GUID.</param>
        /// <param name="itemType">Item type: "series", "season", or "movie".</param>
        /// <param name="query">Search query to pass to yt-dlp.</param>
        /// <param name="cancellationToken">Cancellation token (triggered on client disconnect).</param>
        [HttpGet("DownloadDebug")]
        public async Task DownloadDebugAsync(
            [FromQuery] string itemId,
            [FromQuery] string itemType,
            [FromQuery] string query,
            CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            async Task SendEventAsync(string eventType, string jsonData)
            {
                await Response.WriteAsync(
                    $"event: {eventType}\ndata: {jsonData}\n\n",
                    cancellationToken).ConfigureAwait(false);
                await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            LogCallbackAsync logCallback = async (msg, ct) =>
                await SendEventAsync("log", JsonSerializer.Serialize(msg)).ConfigureAwait(false);

            try
            {
                await SendEventAsync("log", JsonSerializer.Serialize("Starting debug download...")).ConfigureAwait(false);
                await _themeSongsManager.DownloadThemeSongForItemAsync(
                    itemId, itemType, query, logCallback, cancellationToken).ConfigureAwait(false);
                await SendEventAsync("done", JsonSerializer.Serialize(new { itemId, itemType })).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected — normal, no action needed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug download failed for item {ItemId}", itemId);
                try
                {
                    await SendEventAsync("error", JsonSerializer.Serialize(ex.Message)).ConfigureAwait(false);
                }
                catch
                {
                    // Response may already be closed
                }
            }
        }

        /// <summary>
        /// Serves the theme.mp3 file for a given library item.
        /// </summary>
        /// <param name="itemId">Jellyfin item GUID.</param>
        /// <param name="itemType">Item type: "series", "season", or "movie".</param>
        /// <response code="200">The theme.mp3 audio file.</response>
        /// <response code="404">No theme.mp3 exists for this item.</response>
        [HttpGet("ThemeAudio")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult GetThemeAudio([FromQuery] string itemId, [FromQuery] string itemType)
        {
            var themePath = _themeSongsManager.GetThemeFilePath(itemId, itemType);
            if (themePath == null || !System.IO.File.Exists(themePath))
            {
                return NotFound();
            }

            return PhysicalFile(themePath, "audio/mpeg");
        }

        /// <summary>
        /// Deletes all theme.mp3 files from the library.
        /// </summary>
        /// <response code="200">Returns the number of deleted files.</response>
        [HttpDelete("DeleteAll")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(DeleteResultDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<DeleteResultDto>> DeleteAllThemeSongsAsync()
        {
            var count = await _themeSongsManager.DeleteAllThemeSongsAsync(CancellationToken.None).ConfigureAwait(false);
            return Ok(new DeleteResultDto(count));
        }
    }
}
