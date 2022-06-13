#  Constraints

| Constraint | `type` |
|:-----|----------|
| [Operating system](#operating-system) | `os` |
| [Running template engine host](#template-engine-host) | `host` |
| [Installed workloads](#installed-workloads) | `workload` |
| [Current SDK version](#current-sdk-version) | `sdk-version` |

The feature is available since .NET SDK 7.0.100.
The template may define the constraints all of which must be met in order for the template to be used.  In case constraints are not met, the template will be installed, however will not be visible or used by default. 


# Base configuration
The constraints are defined under `constraints` top-level property in `template.json`. `constraints` contains objects (constraint definition). Each constraint should have a unique name, and the following properties:
- `type`: (string) - constraint type (mandatory)
- `args`: (string, array, object) - constraint arguments - depend on actual constraint implementation. May be optional.

Example `template.json`:
```json
}
  // other template elements

  "constraints": {
    "windows-only": {    // Custom name - not validate
      "type": "os",      // Type of the constraint - used to match to proper Constraint component to evaluate the constraint
      "args": "Windows"  // Arguments passed to the evaluating constraint component
    }
  }
}
```

## Operating system

Restrict the template instantiation to certain operating system.

**Configuration:**

 - `type`: `os`
 - `args`: (string, array) - list of supported operating systems. Possible values are: Windows, Linux, OSX.

**Supported in:**
   - all hosts (by default). 3rd party host may explicitly disable the constraint.


### Examples

```json
   "constraints": {
       "linux-only": {
           "type": "os",
           "args": "Linux"
       },
   }
```  
```json
   "constraints": {
       "linux-and-osx": {
           "type": "os",
           "args": [ "Linux", "OSX" ]
       },
   }
```  

## Template engine host
Restrict template to be run only in certain application and its version.
The following host identifiers are available:
- `dotnetcli` - .NET SDK
- `vs` - Visual Studio
- `vs-mac` - Visual Studio for Mac
- `ide` - may refer to both Visual Studio and Visual Studio for Mac
- `dotnetcli-preview` - `dotnet new3` command (used for debugging testing)

3rd party applications may define other host identifiers.

**Configuration:**

- `type`: `host`
- `args` (array). Mandatory. Array elements can have following properties:
  - `hostname`: (string) - the host identifier (see above)
  - `version`: (string, optional) - supported version, or version range. If not specified, all the versions are supported.
 
**Supported in**:
   - all hosts (by default). 3rd party host may explicitly disable the constraint.

The version and version range syntax is explained [here](https://docs.microsoft.com/en-us/nuget/concepts/package-versioning).

The parsing is done in following order:
- exact version syntax (`1.0.0`)
- floating version syntax (`1.*`)
- version range syntax (`(1.0,)`)

### Examples

Supported on .NET SDK 6.
```json
   "constraints": {
       "sdk-only": {
           "type": "host",
           "args": [
               {
                    "hostname": "dotnetcli",
                    "version": "6.0.*"
               }
           ]
       },
   }
```  

Supported from .NET SDK 6.
```json
   "constraints": {
       "sdk-only": {
           "type": "host",
           "args": [
               {
                    "hostname": "dotnetcli",
                    "version": "[6.0,)"
               }
           ]
       },
   }
```

Supported in .NET SDK and Visual Studio
```json
   "constraints": {
       "sdk-only": {
           "type": "host",
           "args": [
               {
                    "hostname": "dotnetcli"
               },
               {
                    "hostname": "vs"
               },
           ]
       },
   }
```

## Installed Workloads
Defines applicability of a template in the dotnet runtime with specific [workloads](https://github.com/dotnet/designs/blob/main/accepted/2020/workloads/workloads.md) installed.
All the installed (queryable via `dotnet workload list`) as well as [extended](https://github.com/dotnet/designs/blob/main/accepted/2020/workloads/workload-manifest.md#workload-composition) workloads are inspected during evaluating of this constraint.

**Configuration:**

- `type`: `workload`
- `args` (string, array). Mandatory. List of names of supported workloads (running host need to have at least one of the requested workloads installed).

  To see the list of instaled workloads run [`dotnet workload list`](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-list). To see the list of available workloads run [`dotnet workload search`](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-search)

**Supported in**:
   - .NET SDK CLI (`dotnet new`)

### Examples

```json
   "constraints": {
       "android-runtime": {
           "type": "workload",
           "args": [ "microsoft-net-runtime-android", "microsoft-net-runtime-android-aot" ]
       },
   }
```  
```json
   "constraints": {
       "web-assembly": {
           "type": "workload",
           "args": "wasm-tools"
       },
   }
```

## Current SDK Version
Defines .NET SDK version(s) the template can be used on.

Only the currently active SDK (queryable via `dotnet --version`, changeable by the [`global.json`](https://docs.microsoft.com/en-us/dotnet/core/tools/global-json)) is being considered. Other available SDKs (queryable via `dotnet --list-sdks`) are checked for possible match and result is reported in the evaluation output wuth possible remedy steps - the form of reporting is dependend on the templating host.

**Configuration:**

- `type`: `sdk-version`
- `args` (string, array). List of versions supported by the template. Syntax and match evaluation of versions are identical as in the [Template engine host](#template-engine-host) constraint.

**Supported in**:
   - .NET SDK CLI (`dotnet new`)

### Examples

```json
   "constraints": {
       "LTS ": {
           "type": "sdk-version",
           "args": [ "6.0.*", "3.1.*" ]
       },
   }
```  
```json
   "current-with-previews": {
       "web-assembly": {
           "type": "sdk-varsion",
           "args": "7.*.*-*"
       },
   }
```