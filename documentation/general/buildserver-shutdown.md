# Shutting down our build servers

## Motivation

The .NET build contains a number of long lived server processes: msbuild nodes, razor and roslyn. A requirement of the .NET SDK is for customers to be able to reliably shut down all servers created during any build. This is important for shutting down a build operation and ensuring that no files remain locked on disk.

This challenging because there is no easy answer to discovering which processes are build servers:

- These are primarily .NET SDK process which means they all share the same process name: `dotnet`
- These processes do not have a strict parent / child relationship with build commands. Several, like roslyn, live past the msbuild nodes that created them.
- The ones inside the .NET SDK can be overridden by installing custom NuPkg.

Each build server has taken a different approach to implementing shutdown. Each of these approaches has its own weakness and it's left us with a rather disjoint experience in shutdown.

- Razor uses a PID file that is subject to PID recycling. It also doesn't differentiate between servers created in the current SDK vs. any other SDK.
- Roslyn looks for common install locations and tries to send a shutdown command. This falls apart when a custom NuPkg is installed or doing shutdown across SDK versions.

To solve this we're going to implement a uniform approach to shutdown for all of our custom build servers using named pipes.

## Overview

The .NET SDK will be responsible for creating a new environment variable called `$DOTNET_HOST_SERVER_PATH`. This will point to a directory on disk that is restricted to the current user (`chmod 700`). On startup a build server will take the following actions:

The build server will use a pipe named `$DOTNET_HOST_SERVER_PATH/<pid>.pipe` where `<pid>` is the PID of the server process. On startup if the named pipe exists it will be deleted as it's an indication of a previous build server that did not shut down gracefully. The server will then create the named pipe and begin listening for input. If any input is received then it will shut down the server process. During normal shut down the server will close the named pipe and delete the file.

If there are any issues creating the named pipe on start up the server will log an appropriate error to the command line as a warning but continue starting up. This is not a fatal issue for build.

The `dotnet build-server shutdown` command will be implemented by iterating over every file in `$DOTNET_HOST_SERVER_PATH`, connecting to it, sending the value `1` and waiting for the corresponding PID to terminate. If no process with the PID exists then the command will not attempt to connect to the named pipe. At the conclusion of the command all files in the directory will be deleted.

The use of the `$DOTNET_HOST_SERVER_PATH` also allows us to transparently apply different policies around server shutdown. For example by default this should be partitioned by .NET SDK major versions. For example .NET SDK 8 and 9 would produce distinct paths like `/home/jaredpar/.dotnet/server/8` and `/home/jaredpar/.dotnet/server/9`. Then `build-servershutdown` could easily be tweaked to shutdown the current .NET SDK or all servers.
