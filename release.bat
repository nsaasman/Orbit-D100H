@echo off
setlocal
set "releaseExit=1"

rem pushd also supports starting this script from a UNC network share.
pushd "%~dp0"
if errorlevel 1 (
    echo FOUT: De projectmap kan niet worden geopend.
    goto finish
)

where dotnet >nul 2>&1
if errorlevel 1 (
    echo FOUT: Installeer de .NET 10 SDK om deze release te bouwen.
    goto cleanup
)

echo Release bouwen...
dotnet build "OrbitD100H.slnx" -c Release
if errorlevel 1 goto failed

echo Automatische controles uitvoeren...
dotnet "src\OrbitD100H.Probe\bin\Release\net10.0-windows\Orbit D100H Probe.dll" --self-test
if errorlevel 1 goto failed

echo Zelfstandige Windows x64-app publiceren...
dotnet publish "src\OrbitD100H.App\OrbitD100H.App.csproj" -c Release -r win-x64 --self-contained true -o "artifacts\Orbit-D100H-win-x64"
if errorlevel 1 goto failed

echo.
echo Orbit D100H release gereed: %~dp0artifacts\Orbit-D100H-win-x64\Orbit D100H.exe
echo Distribueer de volledige Orbit D100H-map Orbit-D100H-win-x64, inclusief runtime.
set "releaseExit=0"
goto cleanup

:failed
set "releaseExit=%errorlevel%"
echo.
echo FOUT: Release afgebroken. Zie de melding hierboven.
echo Bij vergrendelde bestanden: sluit Orbit D100H via Afsluiten in het systeemvak en probeer opnieuw.

:cleanup
popd

:finish
if /I not "%~1"=="/nopause" pause
exit /b %releaseExit%
