[README.md](https://github.com/user-attachments/files/33736323/README.md)
# Bedrock Launcher

A customizable launcher for Minecraft Bedrock Edition on Windows, styled after Ore UI. It starts the game and can load DLL mods you choose.

## Features

- Ore UI-inspired interface with bevelled buttons and a moving panorama
- Play tab with installation selection and launch progress
- Installations tab to add, select and remove installations
- Mods tab to add DLLs, switch each on or off, and remove them
- Customize tab for player name, accent color, background theme, corner style, font, UI size and panorama
- Settings save automatically

## Requirements

- Windows 10 or 11 (64-bit)
- Minecraft Bedrock Edition installed from the Microsoft Store
- Microsoft Edge WebView2 Runtime (already included with Windows 11 and most updated Windows 10 systems)

## Download

1. Open the **Actions** tab of this repository.
2. Click the latest successful run (green tick).
3. Download the **BedrockLauncher** artifact under Artifacts.
4. Unzip it and run `BedrockLauncher.exe`. Keep the `web` folder next to the exe.

Windows SmartScreen may warn about the app because it is unsigned. Click **More info**, then **Run anyway**.

## Build it yourself

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download).
2. Run `build.bat`.
3. The result is in the `dist` folder.

## Using mods

1. Open the **Mods** tab and click **Add DLL**.
2. Pick the DLL file. Use official downloads only, since a DLL runs with full access to your PC.
3. Switch on the ones you want, then press **Play**.

The launcher starts Minecraft if needed, waits for it to load, then loads each enabled DLL. If the game crashes, check that the DLL matches your Bedrock version. If it loads too early, raise the wait time in `Program.cs` (currently 5000 ms).

## Project layout

| Path | Purpose |
| --- | --- |
| `Program.cs` | Window, message handling, game launch and DLL loading |
| `web/index.html` | The launcher interface |
| `BedrockLauncher.csproj` | Project settings |
| `build.bat` | One-click local build |
| `.github/workflows/build.yml` | Automatic build on GitHub |

## Notes

- Many multiplayer servers ban modified clients. Check the rules before joining.
- This project is not affiliated with Mojang or Microsoft. Minecraft is a trademark of Mojang Studios.
- Use at your own risk.
