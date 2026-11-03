@echo off
REM Remove the ForgeCheat patch and restore the original game assembly.
setlocal
set GAMEDIR=%~1
if "%GAMEDIR%"=="" set GAMEDIR=E:\ForgeTax v26.10.12

set MANAGED=%GAMEDIR%\ForgeTax_Data\Managed

if exist "%MANAGED%\Assembly-CSharp.dll.orig" (
    copy /y "%MANAGED%\Assembly-CSharp.dll.orig" "%MANAGED%\Assembly-CSharp.dll"
    echo Restored original Assembly-CSharp.dll
) else (
    echo No backup found - nothing to restore.
)

if exist "%MANAGED%\ForgeCheat.dll" del /q "%MANAGED%\ForgeCheat.dll"
if exist "%GAMEDIR%\ForgeCheat.cfg" del /q "%GAMEDIR%\ForgeCheat.cfg"
echo Uninstalled.
pause
