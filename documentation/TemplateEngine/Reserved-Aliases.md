The following are [reserved aliases](#reserved-aliases)  in template engine. They are applied to .NET SDK 7+.

If reserved alias is used as the name of Parameter symbol, it will be prefixed with `param:` or `-p:` as template option when instantiating the template.
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
For symbol `u` the template option is `--u`, but **`-p:u`** for short.

#### Reserved Aliases
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