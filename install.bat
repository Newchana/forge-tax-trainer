@echo off
REM Re-apply the ForgeCheat patch (also use this after a game update).
setlocal
set GAMEDIR=%~1
if "%GAMEDIR%"=="" set GAMEDIR=E:\ForgeTax v26.10.12

cd /d "%~dp0"
echo Building mod...
dotnet build src\ForgeCheat\ForgeCheat.csproj -c Release -v q --nologo || goto :fail
echo Building patcher...
dotnet build src\Patcher\Patcher.csproj -c Release -v q --nologo || goto :fail

echo Patching "%GAMEDIR%" ...
dotnet src\Patcher\bin\Release\net8.0\ForgeTaxPatcher.dll "%GAMEDIR%" || goto :fail
echo.
echo Done. Launch the game and press F8.
pause
exit /b 0

:fail
echo.
echo FAILED. Check that the .NET SDK is installed and the game path is correct.
pause
exit /b 1
