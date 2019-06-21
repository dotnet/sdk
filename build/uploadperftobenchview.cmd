@echo on
setlocal EnableDelayedExpansion

REM The intent of this script is upload produced performance results to BenchView in a CI context.
REM    There is no support for running this script in a dev environment.

if "%perfWorkingDirectory%" == "" (
    echo EnvVar perfWorkingDirectory should be set; exiting...
    exit /b %1)
if "%configuration%" == "" (
    echo EnvVar configuration should be set; exiting...
    exit /b 1)
if "%architecture%" == "" (
    echo EnvVar architecture should be set; exiting...
    exit /b 1)
if "%OS%" == "" (
    echo EnvVar OS should be set; exiting...
    exit /b 1)
if "%TestFullMSBuild%" == "" (
    set TestFullMSBuild=false
    )
if /I not "%runType%" == "private" if /I not "%runType%" == "rolling" (
    echo EnvVar runType should be set; exiting...
    exit /b 1)
if /I "%runType%" == "private" if "%BenchviewCommitName%" == "" (
    echo EnvVar BenchviewCommitName should be set; exiting...
    exit /b 1)
if /I "%runType%" == "rolling" if "%GIT_COMMIT%" == "" (
    echo EnvVar GIT_COMMIT should be set; exiting...
    exit /b 1)
if "%GIT_BRANCH%" == "" (
    echo EnvVar GIT_BRANCH should be set; exiting...
    exit /b 1)
if not exist %perfWorkingDirectory%\nul ( 
    echo $perfWorkingDirectory does not exist; exiting...
    exit 1)

set pythonCmd=py
if exist "C:\Python35\python.exe" set pythonCmd=C:\Python35\python.exe

REM Do this here to remove the origin but at the front of the branch name as this is a problem for BenchView
if "%GIT_BRANCH:~0,7%" == "origin/" (set GIT_BRANCH_WITHOUT_ORIGIN=%GIT_BRANCH:origin/=%) else (set GIT_BRANCH_WITHOUT_ORIGIN=%GIT_BRANCH%)

set benchViewName=SDK perf %OS% %architecture% %configuration% TestFullMSBuild-%TestFullMSBuild% %runType% %GIT_BRANCH_WITHOUT_ORIGIN%
if /I "%runType%" == "private" (set benchViewName=%benchViewName% %BenchviewCommitName%)
if /I "%runType%" == "rolling" (set benchViewName=%benchViewName% %GIT_COMMIT%)
echo BenchViewName: "%benchViewName%"

echo Creating: "%perfWorkingDirectory%\submission.json"
%HELIX_CORRELATION_PAYLOAD%\.dotnet\dotnet.exe run^
 --project %HELIX_CORRELATION_PAYLOAD%\src\Tests\PerformanceTestsResultGenerator\PerformanceTestsResultGenerator.csproj^
 /p:configuration=%configuration%^
 /p:NUGET_PACKAGES=%HELIX_CORRELATION_PAYLOAD%\.packages --^
 --output "%perfWorkingDirectory%\submission.json"^
 --repository-root "%HELIX_CORRELATION_PAYLOAD%"^
 --sas "%PERF_COMMAND_UPLOAD_TOKEN%"

echo Uploading: "%perfWorkingDirectory%\submission.json"
REM TODO upload it use Azcopy

exit /b %ErrorLevel%
