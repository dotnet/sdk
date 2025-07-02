# dnx shim

To enable a root `dnx` command, we put a dnx shim beside the `dotnet` executable.  The shim invokes `dotnet dnx` and forwards any other command-line arguments that were passed in.

On non-Windows operating systems, the shim is a shell script.  We could do the same thing on Windows with a `dnx.cmd` script:

```
@echo off
"%~dp0dotnet.exe" dnx %*
```

However, using this method, if you press CTRL+C to try to exit out of a running tool, the Windows command interpreter will ask you if you want to terminate the batch job:

> Terminate batch job (Y/N)

That's not an ideal experience.  There doesn't seem to be a good way to avoid this on Windows using a batch/cmd file.  The closest you can do is something like `start "" /b /wait "%~dp0dotnet.exe" dnx %*`.  However, this detaches the launched tool process from recieving the `CTRL_C_EVENT` messages, so CTRL+C would not necessarily interrupt the process in the same way.

So on Windows we create a dnx.exe shim which behaves similarly to the script, but without the CTRL+C handling issue.