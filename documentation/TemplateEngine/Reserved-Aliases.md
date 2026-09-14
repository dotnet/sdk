### Reference for dotnetcli.host.json
In `dotnet new` the parameter symbols are exposed as additional template options that can be passed to the command. Generally, `dotnet new` uses the name of parameter symbol to generate the long and the short alias for the option:
- The long alias is a parameter symbol name with `--` prefix.
- The short alias is a parameter symbol name first character with `-` prefix.

Note that the aliases are case sensitive.

With `dotnetcli.host.json` file we can change long alias and short alias of the template option and its visibility. `dotnetcli.host.json` is located at the same directory as template.json file. It has the following configuration:
- `symbolsInfo`  
It has a collection of the name of specified parameter symbol in template.json with corresponding long or short aliases to override original ones and the visibility of the template option.  
   - `longName` - overrides the long alias for the parameter symbol.
   - `shortName` - overrides the short alias for the parameter symbol. Empty means short alias should not be used.
   - `isHidden` - `true` if the parameter should be hidden in `dotnet new`.
- `usageExamples` - not used; see [#3262](https://github.com/dotnet/templating/issues/3262) for more details.

In case when option assignment results in duplicate options, `--param:` prefix is used for the long alias and short alias is generated from the beginning of the symbol's name till the one which can form a unique short name with `-` prefix or `-p:` prefix is used.

##### Examples
##### Example 1
Overrides original long alias `--TargetFrameworkOverride` with `--targetframework` and overrides short alias `-T` with `-tf`.
```
{
  "$schema": "http://json.schemastore.org/dotnetcli.host",
  "symbolInfo": {
    "TargetFrameworkOverride": {
      "longName": "targetframework",
      "shortName": "tf"
    }
  }
}
```
Part of the output of `dotnet new <template shortName> --help` looks like:
```
Template options:
  -tf, --targetframework <net7.0|net6.0|...>  Overrides the target framework.
```

##### Example 2
Parameter `TargetFrameworkOverride` is hidden from `dotnet new` help information.
```
{
  "$schema": "http://json.schemastore.org/dotnetcli.host",
  "symbolInfo": {
    "TargetFrameworkOverride": {
      "isHidden": "true"
    }
  }
}
```

##### Example 3
If short name is not specified, short name is the first character of overridden long name. Long alias is `--targetframework` and short alias is `-t`.
```
{
  "$schema": "http://json.schemastore.org/dotnetcli.host",
  "symbolInfo": {
    "TargetFrameworkOverride": {
      "longName": "targetframework"
    }
  }
}
```

##### Example 4
If short name is empty, there is no short alias for the template option. Long alias is `--targetframework`.
```
{
  "$schema": "http://json.schemastore.org/dotnetcli.host",
  "symbolInfo": {
    "TargetFrameworkOverride": {
      "longName": "targetframework",
      "shortName": ""
    }
  }
}
```
Part of the output of `dotnet new <template shortName> --help` looks like:
```
Template options:
  --targetframework <net7.0|net6.0|...>  Overrides the target framework.
```

##### Example 5
If long/short alias from overridden long/short name is reserved alias, long name will be prefixed with `--param:` and short name will be prefixed with `-p:` instead. Here long alias is `--param:package` and short alias is `-p:i`. See [Reserved Aliases](#reserved-aliases).
```
{
  "$schema": "http://json.schemastore.org/dotnetcli.host",
  "symbolInfo": {
    "pack": {
      "longName": "package",
      "shortName": "i"
    }
  }
}
```

##### Example 6
Assuming `template.json` has the symbols below.
```
  "symbols": {
    "TargetFrameworkOverride": {
      "type": "parameter",
      "description": "Overrides the target framework.",
      "dataType": "string",
      "defaultValue": "net7.0"
    },
    "targetframework": {
      "type": "parameter",
      "description": "The target framework for the project.",
      "dataType": "string",
      "defaultValue": "net6.0"
    }
  }
```
With the following `dotnetcli.host.json`, long alias and short alias of symbol `TargetFrameworkOverride` is `--targetframework` and `-t`. For symbol `targetframework`, since aliases `--targetframework` `-t` are already taken its long alias is prefixed with `--param:` to be `--param:targetframework` and short alias is `-ta`.
```
{
  "$schema": "http://json.schemastore.org/dotnetcli.host",
  "symbolInfo": {
    "TargetFrameworkOverride": {
      "longName": "targetframework"
    }
  }
}
```
Part of the output of `dotnet new <template shortName> --help` looks like:
```
Template options:
  -t, --targetframework <net7.0>  Overrides the target framework.
  -ta, --param:targetframework <net6.0>  The target framework for the project.
```

### Reserved Aliases
The following are [reserved aliases](#list-of-reserved-aliases) in `dotnet new`. They are applied to .NET SDK 7+.

If long alias is reserved alias, long name will be prefixed with `--param:` instead.  
If short alias is reserved alias, short name will be prefixed with `-p:` instead.  
##### Example
    "symbols": {
        "package": {
           "description": "A sample symbol for --package is reserved.",
           "type": "parameter",
           "defaultValue": "This is the default value for package."
        },
         "u": {
            "description": "A sample symbol for -u is reserved.",
            "type": "parameter",
            "defaultValue": "This is the default value for u."
         }
    }
When instantiating the template, for symbol `package` the template option is **`--param:package`** , and `-p` for short.
For symbol `u` the template option is `--u`, and **`-p:u`** for short.

#### List of reserved aliases
|Alias|Description|
|-|-|
|create|Instantiates a template with given short name.|
|install<br/>--install<br/>-i|Installs a template package.|
|uninstall<br/>--uninstall<br/>-u|Uninstalls a template package.|
|update<br/>--update-check|Checks the currently installed template packages for update, and install the updates.|
|search<br/>--search|Searches for the templates on NuGet.org.|
|list<br/>--list<br/>-l|Lists templates containing the specified template name. If no name is specified, lists all templates.|
|alias|Creates or displays defined aliases.|
|-o<br/>--output|Location to place the generated output.|
|-n<br/>--name|The name for the output being created. If no name is specified, the name of the output directory is used.|
|--dry-run|Displays a summary of what would happen if the given command line were run if it would result in a template creation.|
|--force|Forces content to be generated even if it would change existing files.|
|--no-update-check|Disables checking for the template package updates when instantiating a template.|
|--type|Specifies the template type to instantiate.|
|--author|Filters the templates based on the template author.|
|--baseline|Filters the templates based on baseline defined in the template.|
|--language<br/>-lang|Filters templates based on language.|
|--tag|Filters the templates based on the tag.|
|--package|Filters the templates based on NuGet package ID.|
|--interactive|Allows the command to stop and wait for user input or action (for example to complete authentication).|
|--add-source<br/>--nuget-source|Specifies a NuGet source to use during install.|
|--columns-all|Display all columns in the output.|
|--columns|Specifies the columns to display in the output. |
|--debug:custom-hive|Sets custom settings location.|
|--debug:ephemeral-hive<br/>--debug:virtual-hive|If specified, the settings will be not preserved on file system.|
|--debug:attach|Allows to pause execution in order to attach to the process for debug purposes.|
|--debug:reinit|If specified, resets the settings prior to command execution.|
|--debug:rebuild-cache<br/>--debug:rebuildcache|If specified, removes the template cache prior to command execution.|
|--debug:show-config<br/>--debug:showconfig|If specified, shows the template engine config prior to command execution.|
|--project|The project that should be used for context evaluation.|
|--debug:disable-sdk-templates|If present, prevents templates bundled in the SDK from being presented.|
|--debug:disable-project-context|Disables evaluating project context using MSBuild.|
|--update-apply|Checks the currently installed template packages for updates.|
|--alias<br/>-a|Creates an alias for instantiate command with a certain set of arguments.|
|--show-alias|Displays defined aliases.|
|-h<br/>/h<br/>--help<br/>-?<br/>/?|Show command line help.|