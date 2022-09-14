# Binding and project context evaluation

## Overview

Since .NET SDK 7.0.100 template engine supports binding of symbols to various external sources, including MSBuild properties of the current project.
The feature is available for both `dotnet new` and Visual Studio.

## `bind` symbols

The symbol binds value from external sources. 
By default, the following sources are available:
- host parameters - parameters defined at certain host. For .NET SDK the following parameters are defined: `HostIdentifier: dotnetcli`, `GlobalJsonExists: true/false`.  Binding syntax is `host:<param name>`, example: `host:HostIdentifier`. 
- environment variables - allows to bind environment allowed. Binding syntax is `env:<environment variable name>`, example: `env:MYENVVAR`.

It is also possible to bind the parameter without the prefix as the fallback behavior: `HostIdentifier`, `MYENVVAR`.

The priority of the sources are as the following:
- host parameters: 100
- environment variables: 0

The higher value indicates higher priority.


|Name|Description|
|---|---|
|`type`|`bind`|
|`binding`| Mandatory. The name of the source and parameter in the source to take the value from. The syntax follows: `<source prefix>:<parameter name>`.|
|`replaces`|The text to be replaced by the symbol value in the template files content.|
|`fileRename`|The portion of template filenames to be replaced by the symbol value.| 	 
|`defaultValue`|The value assigned to the symbol if no value was provided from external source(s).|

 
##### Example  

```json
"symbols": {
   "HostIdentifier": {
      "type": "bind",
      "binding": "host:HostIdentifier"
    }
}
```  

### Binding to MSBuild properties

It is possible to bind symbols to MSBuild properties of the current project. The prefix to be used: `msbuild` and it is mandatory.
Commonly used with item templates to get the information about the project it is added to.

`dotnet new` attempts to find the closest project file using following rules:
- the project in current directory or `--output` directory (matching `*.*proj` extension)
- if not found, the parent of above and so on
In case of ambiguity, the path to the project can be specified using `--project` option.

Once project is located, its MSBuild properties are evaluated. The project should be restored, otherwise evaluation fails.
Only .NET [SDK-style projects](https://docs.microsoft.com/en-us/dotnet/core/project-sdk/overview) are supported.

It is recommended to configure `defaultValue` for `bind` symbol that will be used in case evaluation fails. 
If applicable, it is also recommended to use [project capability constraint](https://github.com/dotnet/templating/wiki/Constraints#Project-capabilities) to define the projects that the template can be added to.

Example - binds `DefaultNamespace` symbol to `RootNamespace` of the project:
```json
  "symbols": {
    "DefaultNamespace": {
      "type": "bind",
      "binding": "msbuild:RootNamespace",
      "replaces": "%NAMESPACE%",
      "defaultValue": "TestNamespace"
    }
  },
  "constraints": {
    "csharp-only": {
      "type": "project-capability",
      "args": "CSharp + TestContainer" // only allowed in C# test project
    }
  }
```


## Visual Studio specifics

Visual Studio supports binding to host parameters, environment variables and MSBuild properties.
In addition to that, there is additional `context` source supporting:
- `context:cratesolutiondirectory` - indicates if a solution directory is to be created as a result of project creation (Place solution and project in same directory is UNCHECKED in NPD).
- `context:isexclusive` - is the template instantiation is a result of a new project being created (true) vs adding to an existing solution (false).
- `context:solutionname` - the name of the solution, which may be different from the project name.

Visual Studio also provides a way to bind to "namespace" via host parameters source:
```
  "type": "bind"
  "binding": "namespace"
```

or 

```
  "type": "bind"
  "binding": "host:namespace"
```