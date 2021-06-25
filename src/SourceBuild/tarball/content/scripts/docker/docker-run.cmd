
@echo off
setlocal

set SCRIPT_ROOT=%~dp0
set REPO_ROOT=%SCRIPT_ROOT%..\..\

:arg_loop
set SET_DOCKERFILE=
set SET_DOCKERIMAGE=
if /I "%1" equ "-d" (set SET_DOCKERFILE=1)
if /I "%1" equ "--dockerfile" (set SET_DOCKERFILE=1)
if "%SET_DOCKERFILE%" == "1" (
  echo "1: %1 2: %2"
  set DOCKER_FILE=%2
  shift /1
  shift /1
  goto :arg_loop
)
if /I "%1" equ "-i" (set SET_DOCKERIMAGE=1)
if /I "%1" equ "--image" (set SET_DOCKERIMAGE=1)
if "%SET_DOCKERIMAGE%" == "1" (
  set DOCKER_IMAGE=%2
  shift /1
  shift /1
  goto :arg_loop
)

if "%DOCKER_FILE%" == "" (
    echo Missing required parameter --dockerfile [docker file dir]
    exit /b 1
)
if "%DOCKER_IMAGE%" == "" (
    echo Missing required parameter --image [image name]
    exit /b 1
)

if EXIST "%DOCKER_FILE%\Dockerfile" (
    docker build -q -f %DOCKER_FILE%\Dockerfile -t %DOCKER_IMAGE% %DOCKER_FILE%
) else (
    echo Error: %DOCKER_FILE%\Dockerfile does not exist
    exit /b 1
)

docker run -i -t --rm --init -v %REPO_ROOT%:/code -t -w /code %DOCKER_IMAGE% /bin/sh
endlocal

