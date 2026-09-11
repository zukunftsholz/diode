@echo off
setlocal
title diode builder
set "DIR=%~dp0"
set "SRC=%DIR%src\diode.cs"
set "PROJDIR=%DIR%build48"
set "ICO=%PROJDIR%\diode.ico"
set "MANIFEST=%PROJDIR%\app.manifest"
set "OUT=%DIR%diode.exe"

echo ============================================
echo  diode - build script
echo ============================================
echo.

if not exist "%SRC%" (
  echo [ERROR] src\diode.cs not found.
  goto fail
)

set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if exist "%CSC%" (
  echo [1/2] Compiling with .NET Framework C# compiler...
  set "MANIFEST_ARG="
  if exist "%MANIFEST%" set "MANIFEST_ARG=/win32manifest:"%MANIFEST%""
  "%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
    /out:"%OUT%" /win32icon:"%ICO%" %MANIFEST_ARG% ^
    /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll ^
    "%SRC%"
  if errorlevel 1 goto fail
  goto ok
)

echo [!] Built-in compiler not found, trying .NET SDK...
where dotnet >nul 2>nul
if errorlevel 1 goto nocompiler
echo [1/2] Building with dotnet SDK...
pushd "%PROJDIR%"
dotnet build diode.csproj -c Release
set "RC=%ERRORLEVEL%"
popd
if not "%RC%"=="0" goto fail
if not exist "%PROJDIR%\bin\Release\diode.exe" goto fail
copy /y "%PROJDIR%\bin\Release\diode.exe" "%OUT%" >nul
goto ok

:ok
echo [2/2] Build succeeded.
echo.
echo      %OUT%
echo.
echo Starting diode - look for the icon in the system tray.
echo Left click the tray icon: toggle darkest / brightest.
echo.
start "" "%OUT%"
echo Done. This window will close in 5 seconds...
timeout /t 5 >nul
endlocal
exit /b 0

:nocompiler
echo [ERROR] No C# compiler available on this machine.
goto fail

:fail
echo.
echo BUILD FAILED - see messages above.
echo.
pause
endlocal
exit /b 1
