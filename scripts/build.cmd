@echo off

set name=Rebus.Wire
set version=%1
set reporoot=%~dp0\..

if "%version%"=="" (
  echo Please remember to specify which version to build as an argument.
  goto exit_fail
)

set msbuild=%ProgramFiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe

if not exist "%msbuild%" (
  echo Could not find MSBuild here:
  echo.
  echo   "%msbuild%"
  echo.
  goto exit_fail
)

set sln=%reporoot%\%name%.sln

if not exist "%sln%" (
  echo Could not find SLN to build here:
  echo.
  echo    "%sln%"
  echo.
  goto exit_fail
)

set nuget=%reporoot%\tools\NuGet\NuGet.exe

if not exist "%nuget%" (
  echo Could not find NuGet here:
  echo.
  echo    "%nuget%"
  echo.
  goto exit_fail
)

set destination=%reporoot%\deploy

if exist "%destination%" (
  rd "%destination%" /s/q
)

mkdir "%destination%"

"%msbuild%" "%sln%" /p:Configuration=Release /t:rebuild

"%nuget%" pack "%name%\%name%.nuspec" -OutputDirectory "%destination%" -Version %version%

goto exit




:exit_fail

exit /b 1



:exit