# Launch Plinth in dev mode. Convenience wrapper around `npm run tauri dev`
# that also works in shells opened before the .NET SDK was added to PATH.

$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:USERPROFILE\.cargo\bin;$env:PATH"

Set-Location $PSScriptRoot
npm run tauri dev
