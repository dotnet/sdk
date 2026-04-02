# `dotnet new` exit codes and their meaning

Exit codes are chosen to conform to existing standards or standardization attempts and well known exit code. See [Related resources](#related) for more details 

| Exit&nbsp;Code | Reason |
|:-----|----------|
| 0 | Success |
| [70](#70) | Unexpected internal software issue. |
| [73](#73) | Can't create output file. |
| [100](#100) | Instantiation Failed - Processing issues. |
| [101](#101) | Invalid template or template package. |
| [102](#102) | Missing required option(s) and/or argument(s) for the command. |
| [103](#103) | The template or the template package was not found. |
| [104](#104) | PostAction operation was cancelled. |
| [105](#105) | Instantiation Failed - Post action failed. |
| [106](#106) | Template/Package management operation Failed. |
| [107 - 113](#107) | Reserved. |
| [127](#127) | Unrecognized option(s) and/or argument(s) for a command. |
| [130](#130) | Command terminated by user. |


To enable verbose logging in order to troubleshoot issue(s), set the `DOTNET_CLI_CONTEXT_VERBOSE` environment variable to `true`

_PowerShell:_
```PowerShell
$env:DOTNET_CLI_CONTEXT_VERBOSE = 'true'
```

_Cmd:_
```cmd
set DOTNET_CLI_CONTEXT_VERBOSE=true
```

## <a name="70"></a>70 - Unexpected internal software issue

Unexpected result or issue. [File a bug](https://github.com/dotnet/templating/issues/new?title=Unexpected%20Internal%20Software%20Issue%20(EX_SOFTWARE)) if you encounter this exit code.

This is a semi-standardized exit code (see [EX_SOFTWARE in /usr/include/sysexits.h](https://github.com/openbsd/src/blob/master/include/sysexits.h#L107))


## <a name="73"></a>73 - Can't create output file.

The operation was cancelled due to detection of an attempt to perform destructive changes to existing files. This can happen if you are attempting to instantiate template into the same folder where it was previously instantiated under same target name (specified via `--name` option or defaults to the target directory name)

_Example:_
```console
> dotnet new console

The template "Console App" was created successfully.

Processing post-creation actions...
Running 'dotnet restore' on C:\tmp\tmp.csproj...
  Determining projects to restore...
  Restored C:\tmp\tmp.csproj (in 47 ms).
Restore succeeded.

> dotnet new console

Creating this template will make changes to existing files:
  Overwrite   ./tmp.csproj
  Overwrite   ./Program.cs

Rerun the command and pass --force to accept and create.

For details on current exit code please visit https://aka.ms/templating-exit-codes#73
```

Destructive changes can be forced by passing `--force` option.

This is a semi-standardized exit code (see [EX_CANTCREAT in /usr/include/sysexits.h](https://github.com/openbsd/src/blob/master/include/sysexits.h#L110))


## <a name="100"></a>100 - Instantiation Failed - Processing issues

The template instantiation failed due to error(s). Caused by environment (failure to read/write template(s) or cache). 

## <a name="101"></a>101 - Invalid template or template package

_Reserved for future usage - described behavior is yet not implemented. [Feature is tracked](https://github.com/dotnet/templating/issues/4801)_

Caused by erroneous template(s) (incomplete conditions, symbols or macros etc.). Exact error reason will be output to stderr.

_Examples:_

Missing mandatory properties in template.json
```json
{
    "author": "John Doe",
    "name": "name",
}
```


## <a name="102"></a>102 - Missing required option(s) and/or argument(s) for the command

_Reserved for future usage - described behavior is only partially implemented. Some cases that should fall under this exit code are now leading to code [127](#127) [Issue is tracked](https://github.com/dotnet/templating/issues/4806)_

The exit code is used when one or more required options or/and arguments used for the command were not passed. Applicable to `search` command with not enough information as well.

Applicable as well if template option [marked as required](Reference-for-template.json.md#isrequired) was not passed during the template instantiation.

_Examples:_
```console
> dotnet new my-template
Mandatory option '--MyMandatoryParam' is missing for the template 'My Template'.

For details on current exit code please visit https://aka.ms/templating-exit-codes#102
```

```console
> dotnet new search
Search failed: not enough information specified for search.
To search for templates, specify partial template name or use one of the supported filters: '--author', '--baseline', '--language', '--type', '--tag', '--package'.
Examples:
   dotnet new search web
   dotnet new search --author Microsoft
   dotnet new search web --language C#

For details on current exit code please visit https://aka.ms/templating-exit-codes#102
```


## <a name="103"></a>103 - The template or the template package was not found

Applicable to instantiation, listing, remote sources searching and installation.

_Examples:_
```console
> dotnet new xyz
No templates found matching: 'xyz'.

To list installed templates, run:
   dotnet new list
To search for the templates on NuGet.org, run:
   dotnet new search xyz

For details on current exit code please visit https://aka.ms/templating-exit-codes#103
```

```console
> dotnet new list xyz
No templates found matching: 'xyz'.

To search for the templates on NuGet.org, run:
   dotnet new search xyz

For details on current exit code please visit https://aka.ms/templating-exit-codes#103
```

```console
> dotnet new search xyz
Searching for the templates...
Matches from template source: NuGet.org
No templates found matching: 'xyz'.

For details on current exit code please visit https://aka.ms/templating-exit-codes#103
```

```console
> dotnet new install foobarbaz
The following template packages will be installed:
   foobarbaz

foobarbaz could not be installed, no NuGet feeds are configured or they are invalid.

For details on current exit code please visit https://aka.ms/templating-exit-codes#103
```

## <a name="104"></a>104 - Post action operation was cancelled

Applicable to a case when user aborts run-script post action.


## <a name="105"></a>105 - Instantiation Failed - Post action failed

Applicable to a case when post action fails - unless it is configured to [continue on errors](Post-Action-Registry.md#continueOnError).

## <a name="106"></a>106 - Template/Package management operation failed

The exit code is used for errors during templates installation, uninstallation or updates.
Failure to download packages, read/write templates or cache, erroneous or corrupted template, or an attempt to install same package multiple times.

_Example:_
```console
>dotnet nuget disable source nuget.org
Package source with Name: nuget.org disabled successfully.

> dotnet new install webapi2
The following template packages will be installed:
   webapi2

Error: No NuGet sources are defined or enabled.
webapi2 could not be installed, the package does not exist.

For details on current exit code please visit https://aka.ms/templating-exit-codes#106
```

## <a name="107"></a><a name="108"></a><a name="109"></a><a name="110"></a><a name="111"></a><a name="112"></a><a name="113"></a>107 - 113

Reserved for future use.

[File a bug](https://github.com/dotnet/templating/issues/new?title=Unexpected%20Exit%20Code) if you encounter any of these exit codes.


## <a name="127"></a>127 - Unrecognized option(s) and/or argument(s) for a command

The exit code is used when one or more options or/and arguments used in the command not recognized or invalid. 

Usually a mismatch in type of the specified template option or unrecognized choice value. 

_Examples:_

```console
> dotnet new console --framework xyz
Error: Invalid option(s):
--framework xyz
   'xyz' is not a valid value for --framework. The possible values are:
      net6.0   - Target net6.0
      net7.0   - Target net7.0

For details on current exit code please visit https://aka.ms/templating-exit-codes#127
```

```console
dotnet new update --smth
Unrecognized command or argument '--smth'



Description:
Checks the currently installed template packages for update, and install the updates.



Usage:
dotnet new update [options]



Options:
--interactive Allows the command to stop and wait for user input or action (for
example to complete authentication).
--add-source, --nuget-source <nuget-source> Specifies a NuGet source to use during install.
--check-only, --dry-run Only check for updates and display the template packages to be updated
without applying update.
-?, -h, --help Show command line help.

For details on current exit code please visit https://aka.ms/templating-exit-codes#127
```

This is a semi-standardized exit code (see [127 - "command not found" in 'The Linux Documentation Project'](https://tldp.org/LDP/abs/html/exitcodes.html))


## <a name="130"></a>130 - Command terminated by user.

_Reserved for future usage - described behavior is yet not implemented. [Feature is tracked](https://github.com/dotnet/templating/issues/4799)_

The exit code is used if command is terminated after user non-forceful termination request (e.g. `Ctrl-C`, `Ctrl-Break`).

This is a semi-standardized exit code (see [130 - Script terminated by Control-C in 'The Linux Documentation Project'](https://tldp.org/LDP/abs/html/exitcodes.html))

<BR/>
<BR/>
<BR/>

### Related Resources
* [`BSD sysexit.h`](https://github.com/openbsd/src/blob/master/include/sysexits.h)
* [`Special exit codes - The Linux Documentation Project`](https://tldp.org/LDP/abs/html/exitcodes.html)
