#  Constraints

| Constraint | `type` |
|:-----|----------|
| [Operating system](#operating-system) | `os` |
| [Running template engine host](#template-engine-host) | `host` |

The feature is available since .NET SDK 7.0.100.
The template may define the constraints all of which must be met in order for the template to be used.  In case constraints are not met, the template will be installed, however will not be visible or used by default. 


# Base configuration
The constraints are defined under `constraints` property in `template.json`. `constraints` contains objects (constraint definition). Each constraint should have a unique name, and the following properties:
- `type`: (string) - constraint type (mandatory)
- `args`: (string, array, object) - constraint arguments - depend on actual constraint implementation. May be optional.

## Operating system

Restrict the template instantiation to certain operating system.

**Configuration:**

 - `type`: `os`
 - `args`: (string, array) - list of supported operating systems. Possible values are: Windows, Unix, OSX.

**Supported in:**
   - all hosts (by default). 3rd party host may explicitly disable the constraint.


### Examples

```json
   "constraints": {
       "unix-only": {
           "type": "os",
           "args": "Unix"
       },
   }
```  
```json
   "constraints": {
       "unix-and-osx": {
           "type": "os",
           "args": [ "Unix", "OSX" ]
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