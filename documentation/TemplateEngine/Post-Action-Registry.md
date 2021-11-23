# Existing Post Actions

| Description | ActionID |
|:-----|----------|
| [Restore NuGet packages](#restore-nuget-packages) | `210D431B-A78B-4D2F-B762-4ED3E3EA9025` |
| [Run script](#run-script) | `3A7C4B45-1F5D-4A30-959A-51B88E82B5D2` |
| [Open a file in the editor](#open-a-file-in-the-editor) | `84C0DA21-51C8-4541-9940-6CA19AF04EE6` |
| [Add a reference to a project file](#add-a-reference-to-a-project-file) | `B17581D1-C5C9-4489-8F0A-004BE667B814` |
| [Add projects to a solution file](#add-projects-to-a-solution-file) | `D396686C-DE0E-4DE6-906D-291CD29FC5DE` |
| [Change file permissions (Unix/OS X)](#change-file-permissions) | `CB9A6CF3-4F5C-4860-B9D2-03A574959774` |
| [Display manual instructions](#display-manual-instructions) | `AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C` |

# Base configuration
Each post action has set of standard properties as well as custom properties defined by certain post action.
The standard properties are listed below.
 - **Configuration** :
   - `actionId` (string): Action ID.
   - `condition` (string) (optional): A C++ style boolean expression defining if post action should be run. This expression may use any symbols that have been defined.
   - `description` (string) (optional): A human-readable description of the action.
   - `configFile` (string) (optional): Additional configuration for the associated post action. The structure & content will vary based on the post action.
   - `continueOnError` (bool) (optional): If this action fails, the value of continueOnError indicates whether to process the next action, or stop processing the post actions. Should be set to true when subsequent actions rely on the result of the current action. The default value is false.
   - `manualInstructions` (array) (optional): An ordered list of possible instructions to display if the action cannot be performed. Each element in the list must contain a key named "text", whose value contains the instructions. Each element may also optionally provide a key named "condition" - a boolean expression. The first instruction with blank condition is considered a default. If true conditions are present, the last one of them will be considered valid, all other ignored. It is recommended not to have more than one true condition at the time.

# Restore NuGet packages

Used to restore NuGet packages after project create.

 - **Action ID** : `210D431B-A78B-4D2F-B762-4ED3E3EA9025`
 - **Specific Configuration** :
    - `args`:
      - `files` (string|array) (optional):
        - `string`: A semicolon delimited list of files that should be restored. If specified, the primary outputs will be ignored for processing. If not specified, matching primary outputs are restored.
        - `array`: An array of files that should be restored. If specified, the primary outputs will be ignored for processing. If not specified, matching primary outputs are restored.
 - **Supported in**:
   - `dotnet new3`
   - `dotnet new` (2.0.0 or higher)
 - **Ignored in**:
   - `Visual Studio` - Visual Studio restores all projects automatically, so post action will be be ignored.

Note: when using `files` argument it should contain the path to the file in source template definition, and ignore all the path and filename changes that can happen when instantiating template. For more details, see [the article](https://github.com/dotnet/templating/wiki/Using-Primary-Outputs-for-Post-Actions).

### Example

Restores the project mentioned in primary outputs:

```
"primaryOutputs": [
    {
      "path": "MyTestProject.csproj"        
    }
],
"postActions": [{
  "condition": "(!skipRestore)",
  "description": "Restore NuGet packages required by this project.",
  "manualInstructions": [{
    "text": "Run 'dotnet restore'"
  }],
  "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
  "continueOnError": true
}]
```

Restores the files mentioned in `files` argument. The primary outputs will be ignored.

```
"primaryOutputs": [
    {
      "path": "Primary/Output/PrimaryOutput.csproj"        // will not be restored
    }
],
"postActions": [{
  "condition": "(!skipRestore)",
  "description": "Restore NuGet packages required by this project.",
  "manualInstructions": [{
    "text": "Run 'dotnet restore'"
  }],
  "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
  "continueOnError": true,
  "args": {
    "files": ["Client/Client.csproj", "Server/Server.csproj"]
  }
}]
```

# Run script

Used to run a script after create.

 - **Action ID** : `3A7C4B45-1F5D-4A30-959A-51B88E82B5D2`
 - **Specific Configuration** : There are three required properties that must be specified.
   - `args`
     - `executable` (string): The executable to launch.
     - `args` (string): The arguments to pass to the executable.
     - `redirectStandardOutput` (bool) (optional): Whether or not to redirect stdout for the process (prevents output from being displayed if true). The default value is true.
     - `redirectStandardError` (bool) (optional): Defines whether or not the stderr should be redirected. If the output is redirected, it prevents it from being displayed. The default value is true. Available since .NET SDK 6.0.100.
   - `manualInstructions` (required)
 - **Supported in**:
   - `dotnet new3`
   - `dotnet new` (2.0.0 or higher)

The working directory for the launched executable is set to the root of the output template content.

### Example

```
"postActions": [{
  "actionId": "3A7C4B45-1F5D-4A30-959A-51B88E82B5D2",
  "args": {
    "executable": "setup.cmd",
    "args": "",
    "redirectStandardOutput": false,
    "redirectStandardError": false
  },
  "manualInstructions": [{
     "text": "Run 'setup.cmd'"
  }],
  "continueOnError": false,
  "description ": "setups the project by calling setup.cmd"
}]
```

# Open a file in the editor

Opens a file in the editor. For command line cases this post action will be ignored.

 - **Action ID** : `84C0DA21-51C8-4541-9940-6CA19AF04EE6`
 - **Specific Configuration** :
   - `files` (string): A semicolon delimited list of indexes to the primary outputs.
     Note: If primary outputs are conditional, multiple post actions with the same
     conditions as the primary outputs might be necessary.
 - **Supported in**:
   - since `Visual Studio 2017.3 Preview 1`

### Example

```
"primaryOutputs": [
  { "path": "Company.ClassLibrary1.csproj" },
  {
    "condition": "(HostIdentifier != \"dotnetcli\")",
    "path": "Class1.cs"
  }
],
"postActions": [
  {
    "condition": "(HostIdentifier != \"dotnetcli\")",
    "description": "Opens Class1.cs in the editor",
    "manualInstructions": [ ],
    "actionId": "84C0DA21-51C8-4541-9940-6CA19AF04EE6",
    "args": {
      "files": "1"
    },
    "continueOnError": true
  }
]
```

# Add a reference to a project file
 - **Action ID** : `B17581D1-C5C9-4489-8F0A-004BE667B814`
 - **Specific Configuration** :
   - `args`
     - `targetFiles` (string|array) (optional):
        - `string`: A semicolon delimited list of files that should be processed.  If not specified, the project file in output directory or its closest parent directory will be used.
        - `array`: An array of files that should be processed. If not specified, the project file in output directory or its closest parent directory will be used.
     - `referenceType` (string): Either "package" or "project".
     - `reference` (string): The package ID or relative path of the project to add the reference to.
     - `projectFileExtensions` (string) (optional): A semicolon delimited list of literal file extensions to use when searching for the project to add the reference to. If not specified, `*.*proj` mask is used when searching.
     - `version` (string) (optional) (package referenceType only): The version of the package to install.
 - **Supported in**:
   - `dotnet new3`
   - `dotnet new` (2.0.0 or higher)

Note: when using `targetFiles` argument it should contain the path to the file in source template definition, and ignore all the path and filename changes that can happen when instantiating template. For more details, see [the article](https://github.com/dotnet/templating/wiki/Using-Primary-Outputs-for-Post-Actions).

### Example

```
"postActions": [{
  "Description": "Adding Reference to Microsoft.NET.Sdk.Functions NuGet package",
  "ActionId": "B17581D1-C5C9-4489-8F0A-004BE667B814",
  "ContinueOnError": "false",
  "ManualInstructions": [{
    "Text": "Manually add the reference to Microsoft.NET.Sdk.Functions to your project file"
  }],
  "args": {
    "referenceType": "package",
    "reference": "Microsoft.NET.Sdk.Functions",
    "version": "1.0.0",
    "projectFileExtensions": ".csproj"
  }
}]
```

# Add project(s) to a solution file

 - **Action ID** : `D396686C-DE0E-4DE6-906D-291CD29FC5DE`
 - **Specific Configuration** :
   - `args`:
     - `projectFiles` (string|array) (optional): 
        - `string`: A semicolon delimited list of files that should be added to solution. If not specified, primary outputs will be used instead.
        - `array`: An array of files that should be added to solution. If not specified, primary outputs will be used instead.
     - `primaryOutputIndexes` (string) (optional): A semicolon delimited list of indexes to the primary outputs. If not specified, all primary outputs will be added. Note: If primary outputs are conditional, multiple post actions with the same conditions as the primary outputs might be necessary.
     - `solutionFolder` (string) (optional) (supported in 5.0.200 or higher): the destination solution folder path to add the projects to. 
 - **Supported in**:
   - `dotnet new3`
   - `dotnet new` (2.0.0 or higher)
 - **Ignored in**:
   - `Visual Studio` - the user indicates where to add project explicitly, so post action defined in the template will be ignored.

Note: when using `projectFiles` argument it should contain the path to the file in source template definition, and ignore all the path and filename changes that can happen when instantiating template. For more details, see [the article](https://github.com/dotnet/templating/wiki/Using-Primary-Outputs-for-Post-Actions).


### Example

Adds `MyTestProject.csproj` to solution in output directory or its closest parent directory.

```
"primaryOutputs": [
    {
      "path": "MyTestProject.csproj"        
    }
],
"postActions": [{
  "description": "Add projects to solution",
  "manualInstructions": [ { "text": "Add generated project to solution manually." } ],
  "args": {
    "solutionFolder": "src"
  },
  "actionId": "D396686C-DE0E-4DE6-906D-291CD29FC5DE",
  "continueOnError": true
}]
```

Adds `MyTestProject.csproj` to solution in output directory or its closest parent directory (using `projectFiles` argument).

```
"postActions": [{
  "description": "Add projects to solution",
  "manualInstructions": [ { "text": "Add generated project to solution manually." } ],
  "args": {
    "solutionFolder": "src",
    "projectFiles": ["MyTestProject.csproj"]
  },
  "actionId": "D396686C-DE0E-4DE6-906D-291CD29FC5DE",
  "continueOnError": true
}]
```


# Change file permissions

Unix / OS X only (runs the Unix `chmod` command).

 - **Action ID** : `CB9A6CF3-4F5C-4860-B9D2-03A574959774`
 - **Specific Configuration** :
    - `args`: The permissions to set (see examples). Usually this will contain a glob like `{ "+x": "*.sh" }` or a list of filenames like `{ "+x": ["script1", "script2"] }`.
 - **Supported in**:
   - `dotnet new3`
   - `dotnet new` (2.0.0 or higher)

### Example

```
"postActions": [{
  "condition": "(OS != \"Windows_NT\")",
  "description": "Make scripts executable",
  "manualInstructions": [{
    "text": "Run 'chmod +x *.sh'"
  }],
  "actionId": "cb9a6cf3-4f5c-4860-b9d2-03a574959774",
  "args": {
    "+x": "*.sh"
  },
  "continueOnError": true
}]
```

or

```
"postActions": [{
  "condition": "(OS != \"Windows_NT\")",
  "description": "Make scripts executable",
  "manualInstructions": [ { "text": "Run 'chmod +x *.sh somethingelse'" }  ],
  "actionId": "cb9a6cf3-4f5c-4860-b9d2-03a574959774",
  "args": {
    "+x": [
      "*.sh",
      "somethingelse"
    ]
  },
  "continueOnError": true
}]
```

# Display manual instructions

Prints out the manual instructions after instantiating template in format:
```
Description: <description defined in post action>
Manual instructions: <manual instructions defined in post action>
Actual command: <executable> <args>
```

Command is printed only if defined in post action arguments.

 - **Action ID** : `AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C`
 - **Specific Configuration** :
    - `args`: 
     - `executable` (string) (optional): command to run
     - `args` (string) (optional): arguments to use
 - **Supported in**:
   - `dotnet new3`
   - `dotnet new` (2.0.0 or higher)

### Example

```
"postActions": [{
  "description": "Manual actions required",
  "manualInstructions": [{
    "text": "Run the following command"
  }],
  "actionId": "AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C",
  "args": {
    "executable": "setup.cmd",
    "args": "<your project name>"
  },
  "continueOnError": true
}]
```
