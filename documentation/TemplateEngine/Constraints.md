#  Constraints

| Constraint | `type` |
|:-----|----------|
| [Operating system](#operating-system) | `os` |
| [Running template engine host](#template-engine-host) | `host` |
| [Installed workloads](#installed-workloads) | `workload` |
| [Current SDK version](#current-sdk-version) | `sdk-version` |
| [Project capabilities](#project-capabilities) | `project-capability` |

The feature is available since .NET SDK 7.0.100.
The template may define constraints, all of which must be met in order for the template to be usable.  In case constraints are not met, the template will be installed, however will not be visible nor used by default. 


# Base configuration
The constraints are defined under `constraints` top-level property in `template.json`. `constraints` contains objects (constraint definition). Each constraint should have a unique name, and the following properties:
- `type`: (string) - constraint type (mandatory)
- `args`: (string, array, object) - constraint arguments - depend on actual constraint implementation. May be optional.

Example `template.json`:
```json
}
  // other template elements

  "constraints": {
    "windows-only": {    // Custom name - not validated
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
 - `args`: (string, array) - list of supported operating systems. Possible values are: `Windows`, `Linux`, `OSX`.

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

  To see the list of installed workloads run [`dotnet workload list`](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-list). To see the list of available workloads run [`dotnet workload search`](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-search)

**Supported in**:
   - .NET SDK CLI (`dotnet new`)
   - Visual Studio 17.4

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

Only the currently active SDK (queryable via `dotnet --version`, changeable by the [`global.json`](https://docs.microsoft.com/en-us/dotnet/core/tools/global-json)) is being considered. Other available SDKs (queryable via `dotnet --list-sdks`) are checked for possible match and result is reported in the evaluation output with possible remedy steps - the form of reporting is dependent on the templating host.

**Configuration:**

- `type`: `sdk-version`
- `args` (string, array). List of versions supported by the template. Syntax and match evaluation of versions are identical as in the [Template engine host](#template-engine-host) constraint.

**Supported in**:
   - .NET SDK CLI (`dotnet new`)
   - Visual Studio 17.4

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
      "type": "sdk-version",
      "args": "7.*.*-*"
   },
}
```
## Project capabilities
Defines [project capabilities](https://github.com/microsoft/VSProjectSystem/blob/master/doc/overview/about_project_capabilities.md) that the template requires.
Commonly used with item templates to define the certain projects it is applicable to. 
`dotnet new` attempts to find the closest project file using following rules:
- The project in current directory or `--output` directory (matching `*.*proj` extension).
- If not found, the parent of above and so on.
- The path to the project can be explicitly specified using `--project` instantiation option. This path takes precedence - so it can be used in case of ambiguity.

Once project is located, its project capabilities are evaluated. The project should be restored, otherwise evaluation fails.
Only .NET [SDK-style projects](https://docs.microsoft.com/en-us/dotnet/core/project-sdk/overview) are supported.

**Configuration:**

- `type`: `project-capability`
- `args`: (string) project capability [expression](https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.vsprojectcapabilityexpressionmatcher) that should be satisfied by the project.

**Supported in**:
   - .NET SDK CLI (`dotnet new`)
   - Visual Studio 17.4 Preview 3

### Examples

```json
"constraints": {
   "CSharp": {
      "type": "project-capability",
      "args": "CSharp",
   },
}
```  
```json
"constraints": {
   "CSharpTest": {
      "type": "project-capability",
      "args": "CSharp & TestContainer",
   },
}
``` 