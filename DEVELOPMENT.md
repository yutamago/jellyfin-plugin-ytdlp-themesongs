# Development & Debugging

This document covers how to build, run, and debug the Theme Songs plugin locally.

## Building

```shell
dotnet build Jellyfin.Plugin.YtdlpThemeSongs.sln /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
```

The `GenerateFullPaths` flag ensures file names in error output contain full paths. `NoSummary` prevents duplicate errors appearing in IDE problem panels.

## Running the Plugin

1. Build the plugin (see above).
2. Create the plugin directory if it does not exist:
   ```
   ~/.local/share/jellyfin/plugins/Theme Songs/
   ```
3. Copy the compiled `.dll` into that directory.
4. Set the working directory of the process you are debugging to the Jellyfin Server's working directory.
5. Start the Jellyfin server.

## Debugging on Visual Studio

Visual Studio can attach to a running Jellyfin process, or launch it directly:

1. Right-click the solution → **Add** → **Existing Project…**
2. Locate and open `Jellyfin.exe` from your Jellyfin installation folder. It will appear in the solution as a project.
3. Right-click that project → **Set as Startup Project**.
4. Right-click that project → **Properties** → set **Attach** to **No**.

From now on, pressing Start in Visual Studio will launch Jellyfin with the debugger attached. Build and copy the plugin dll beforehand (see above).

## Debugging on Visual Studio Code

VS Code can automate the full build-copy-launch sequence. The steps below assume you have already built the Jellyfin server at least once.

### 1. `settings.json`

Create `.vscode/settings.json` with paths specific to your machine:

```jsonc
{
    // Directory of the cloned jellyfin server project (must be built once first)
    "jellyfinDir": "${workspaceFolder}/../jellyfin/Jellyfin.Server",
    // Directory of the cloned jellyfin-web project (must be built once first)
    "jellyfinWebDir": "${workspaceFolder}/../jellyfin-web",
    // Root data directory for a running Jellyfin instance
    // On Windows this defaults to %LOCALAPPDATA%/jellyfin
    "jellyfinDataDir": "${env:LOCALAPPDATA}/jellyfin",
    // Plugin assembly name
    "pluginName": "Jellyfin.Plugin.YtdlpThemeSongs"
}
```

### 2. `launch.json`

Create `.vscode/launch.json`:

```jsonc
{
    "version": "0.2.0",
    "configurations": [
        {
            "type": "coreclr",
            "name": "Launch",
            "request": "launch",
            "preLaunchTask": "build-and-copy",
            "program": "${config:jellyfinDir}/bin/Debug/net8.0/jellyfin.dll",
            "args": [
                "--webdir",
                "${config:jellyfinWebDir}/dist/"
            ],
            "cwd": "${config:jellyfinDir}"
        }
    ]
}
```

### 3. `tasks.json`

Create `.vscode/tasks.json`:

```jsonc
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "build-and-copy",
            "dependsOrder": "sequence",
            "dependsOn": ["build", "make-plugin-dir", "copy-dll"]
        },
        {
            "label": "build",
            "command": "dotnet",
            "type": "shell",
            "args": [
                "publish",
                "${workspaceFolder}/${config:pluginName}.sln",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary"
            ],
            "group": "build",
            "presentation": { "reveal": "silent" },
            "problemMatcher": "$msCompile"
        },
        {
            "label": "make-plugin-dir",
            "type": "shell",
            "command": "mkdir",
            "args": [
                "-Force",
                "-Path",
                "${config:jellyfinDataDir}/plugins/${config:pluginName}/"
            ]
        },
        {
            "label": "copy-dll",
            "type": "shell",
            "command": "cp",
            "args": [
                "./${config:pluginName}/bin/Debug/net8.0/publish/*",
                "${config:jellyfinDataDir}/plugins/${config:pluginName}/"
            ]
        }
    ]
}
```

Running the **Launch** configuration will build the plugin, copy the dll to the plugin directory, and start Jellyfin with the debugger attached.

## Licensing

This plugin is licensed under the **GPLv3**. Because the plugin links against Jellyfin's NuGet packages (also GPLv3), any compiled binary distribution must remain under the GPLv3 or a compatible permissive license. Proprietary or source-unavailable distributions are not permitted.
