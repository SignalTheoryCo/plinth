# Launch Plinth in dev mode.
#
# The .NET SDK on this machine is user-scoped (installed with
# dotnet-install.ps1 to %LOCALAPPDATA%\Microsoft\dotnet) and is not on the
# system PATH, so this script wires it up for the session before starting
# Tauri. If you later add that folder to your user PATH yourself, plain
# `npm run tauri dev` works too.

$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:USERPROFILE\.cargo\bin;$env:PATH"

Set-Location $PSScriptRoot
npm run tauri dev
