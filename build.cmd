@echo off
setlocal

REM The .NET SDK was installed inside Claude's packaged app storage,
REM so a normal terminal can't see it. Point at it directly, plus Rust.
set "CLAUDE_LOCAL=%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local"

REM --- locate dotnet ---
set "DOTNET_DIR="
if exist "%CLAUDE_LOCAL%\Microsoft\dotnet\dotnet.exe" set "DOTNET_DIR=%CLAUDE_LOCAL%\Microsoft\dotnet"
if not defined DOTNET_DIR if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "DOTNET_DIR=%LOCALAPPDATA%\Microsoft\dotnet"
if not defined DOTNET_DIR if exist "%USERPROFILE%\.dotnet\dotnet.exe" set "DOTNET_DIR=%USERPROFILE%\.dotnet"
if not defined DOTNET_DIR if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET_DIR=%ProgramFiles%\dotnet"

REM --- locate cargo (Rust) ---
set "CARGO_DIR="
if exist "%USERPROFILE%\.cargo\bin\cargo.exe" set "CARGO_DIR=%USERPROFILE%\.cargo\bin"
if not defined CARGO_DIR if exist "%CLAUDE_LOCAL%\.cargo\bin\cargo.exe" set "CARGO_DIR=%CLAUDE_LOCAL%\.cargo\bin"

if not defined DOTNET_DIR (
  echo Could not find dotnet.exe. Run:  where /r C:\ dotnet.exe
  exit /b 1
)

echo Using .NET at:  %DOTNET_DIR%
if defined CARGO_DIR (echo Using cargo at: %CARGO_DIR%) else (echo NOTE: cargo not found in common spots, relying on existing PATH)

set "DOTNET_ROOT=%DOTNET_DIR%"
set "PATH=%DOTNET_DIR%;%CARGO_DIR%;%PATH%"

cd /d "%~dp0"
call npm run tauri build
