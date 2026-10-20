@echo off
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
echo.
echo Done. Run dist\BedrockLauncher.exe (keep the web folder next to it).
pause
