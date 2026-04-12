<h1 align="center">Jellyfin yt-dlp Theme Songs Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org">Jellyfin Project</a></h3>

<p align="center">
Automatically download theme songs for your TV show, season, and movie library using <a href="https://github.com/yt-dlp/yt-dlp">yt-dlp</a>.
</p>

---

## What It Does

This plugin searches YouTube for theme songs and downloads them for every TV series, season, and movie in your Jellyfin library. Downloads are saved as `theme.mp3` alongside the media and are automatically picked up by Jellyfin's built-in theme song playback feature.

Downloads are processed with FFmpeg: leading silence is removed, audio is loudness-normalized, and SponsorBlock segments are stripped automatically.

The plugin skips any directory that already contains a `theme.mp3`, so existing files are never overwritten.

## Prerequisites

The following external tools must be available on the system running Jellyfin:

- **[yt-dlp](https://github.com/yt-dlp/yt-dlp)** — used to search YouTube and download audio
- **[FFmpeg](https://ffmpeg.org/)** — used for audio post-processing

Both tools must be installed and accessible. yt-dlp is auto-detected from common system paths and the `PATH` environment variable, or you can specify the path manually in the plugin settings.

## Installation

### From the Jellyfin Repository (recommended)

1. In Jellyfin, go to **Dashboard** → **Plugins** → **Repositories**.
2. Click **Add** and paste the manifest URL:
   ```
   https://yutamago.github.io/jellyfin-plugin-ytdlp-themesongs/manifest.json
   ```
3. Go to **Catalog** and search for **Theme Songs**.
4. Click it and press **Install**.
5. Restart Jellyfin.

### From a `.zip` File

1. Download the latest `.zip` release from the [Releases page](https://github.com/yutamago/jellyfin-plugin-themesongs/releases).
2. Extract the archive and place the `.dll` file in a folder named `Theme Songs` inside your Jellyfin plugins directory:
   - Default on Linux: `~/.local/share/jellyfin/plugins/ytdlp Theme Songs/`
   - Default on Windows: `%LOCALAPPDATA%\jellyfin\plugins\ytdlp Theme Songs\`
   - Inside a portable install: `<jellyfin_dir>/plugins/ytdlp Theme Songs/`
3. Restart Jellyfin.

## Configuration

Go to **Dashboard** → **Plugins** → **Theme Songs** to open the settings page.

### Search Query Templates

The plugin builds a YouTube search query for each item using a configurable template. Customize these to improve search accuracy for your library.

| Setting | Default | Available Placeholders |
|---|---|---|
| **TV Series Search Query** | `{title} TV series official theme song` | `{title}`, `{year}` |
| **TV Season Search Query** | `{seriesTitle} Season {seasonNumber} theme song` | `{seriesTitle}`, `{seasonNumber}`, `{year}` |
| **Movie Search Query** | `{title} {year} official movie theme song` | `{title}`, `{year}` |

### yt-dlp Path

Leave this field blank to let the plugin auto-detect yt-dlp from your system's `PATH` and common install locations. Set it to the absolute path of the yt-dlp binary if auto-detection fails.

## Usage

### Automatic (Scheduled Task)

The plugin registers a scheduled task called **Download Theme Songs** that runs every 24 hours. It will automatically download missing theme songs across your entire library.

You can manage this task under **Dashboard** → **Scheduled Tasks**.

### Manual Download

To trigger a download immediately, open the plugin configuration page (**Dashboard** → **Plugins** → **Theme Songs**) and click the download button.

### Enabling Theme Song Playback

For Jellyfin to play theme songs, the feature must be enabled per user:

1. Go to your **User Settings** → **Display**.
2. Enable **Theme Songs**.

---

For development and debugging instructions, see [DEVELOPMENT.md](DEVELOPMENT.md).
