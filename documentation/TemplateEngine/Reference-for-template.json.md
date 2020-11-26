## Overview

This page describes the options that are available when creating `template.json` files.

When using the Template Engine the design is to allow you to create a template which is still a "runnable project" A "runnable project" is a project that can be
executed as a normal project can. Instead of updating your source files to be tokenized you define replacements, and other processing, mostly in an external file,
the `template.json` file. This requires that a few changes have to be done on the fly during the preparation of the package, and in the `template.json` we define all the operations needed to create a well working package.

|Category|Description|  
|------|------|     
|[Package Definition](#package-definition)|Metadata of the package|     
|[Content Manipulation](#content-manipulation)|Configuration of content changes in source files|
|[Output Management](#output-management)|Configuration of the final output|

### Package Definition
|Name|Description|
|---|---|
|identity|A unique name for this template|
|author|The author of the template|
|classifications|Zero or more characteristics of the template which may be used in search. In this field you define the values shown as Tags in `dotnet new`|
|name|The name for the template. This is displayed as the template name when using `dotnet new`|
|groupIdentity|The ID of the group this template belongs to. When combined with the `tags`section, this allows multiple templates to be displayed as one, with the the decision for which one to use being presented as a choice in each one of the pivot categories (keys) |
|tags|You can use tags to improve the metadata of your project. To specify the project language you have to add the tag `language`, and if you want to make your project searchable via the `dotnet new --type` command you have to use the tag `type`|
|shortName|A default shorthand for selecting the template (applies to environments where the template name is specified by the user - not selected via a GUI). this is the name shown as Short Name in `dotnet new` list of templates, and is the name to use command list to run this template |
|postActions|Enables actions to be performed after the project is created. See https://github.com/dotnet/templating/wiki/Post-Action-Registry|

### Content Manipulation
|Name|Description|Default|
|---|---|---|
|sources|The set of mappings in the template content to user directories. It's defined as any array of [Source](#source-definition) |If not specified, an implicit source is created with `"source": "./"` and `"target": "./"`, all other properties in the source are set to their defaults|
|guids|A list of guids which appear in the template source and should be replaced in the template output. For each guid listed, a replacement guid is generated, and replaces all occurrences of the source guid in the output|
|symbols|[See Below](#symbols)|

#### Source Definition
|Name|Description|Default|
|---|---|---|
|source|The path in the template content (relative to the directory containing the .template.config folder) that should be processed|`./`|
|target|The path (relative to the directory the user has specified) that content should be written to|`./`|
|include|The set of globbing patterns indicating the content to process in the path referred to by `source`|[ "**/*" ]|
|exclude |The set of globbing patterns indicating the content that was included by sources.include that should not be processed|`[ "**/[Bb]in/**", "**/[Oo]bj/**", ".template.config/**/*", "**/*.filelist", "**/*.user", "**/*.lock.json" ]`|
|copyOnly |The set of globbing patterns indicating the content that was included by `include`, that hasn't been excluded by sources.exclude that should be placed in the user's directory without modification|`[ "**/node_modules/**/*" ]`|
|rename|The set of explicit renames to perform. Each key is a path to a file in the source, each value is a path to the target location - only the values will be evaluated with the information the user supplies||
|condition|A Boolean-evaluable condition to indicate if the sources configuration should be included or ignored. If the condition evaluates to `true` or is not provided, the sources config will be used for creating the template. If it evaluates to `false`, the sources config will be ignored||
|modifiers|A list of additional source information which gets added to the top-level source information, based on evaluation the corresponding source.modifiers.condition. Is defined as an array of [Modifier](#modifier-definition)||

#### Modifier Definition
|Name|Description|
|---|---|
|condition|A Boolean-evaluable condition to indicate if the sources.modifiers instance should be included or ignored. If the condition evaluates to true or is not provided, the sources.modifiers instance will be used for creating the template. If it evaluates to false, the sources.modifiers config will be ignored.|
|include|Include configuration specific to this sources.modifiers instance, contingent on the corresponding sources.modifiers.condition. See sources.include for more info.|
|exclude|Exclude configuration specific to this sources.modifiers instance, contingent on the corresponding sources.modifiers.condition. See sources.exclude for more info|
|copyOnly|CopyOnly configuration specific to this sources.modifiers instance, contingent on the corresponding sources.modifiers.condition. See sources.copyonly for more info|

### Symbols 
The symbols section defines variables and their values, the values may be the defined in terms of other symbols. When a defined symbol name is encountered anywhere in the template definition, it is replaced by the value defined in this configuration. The symbols configuration is a collection of key-value pairs. The keys are the symbol names, and the value contains key-value-pair configuration information on how to assign the symbol a value.
The supported symbol types are:
- Parameter
- Derived
- Computed
- Generated
- Bind

There are 3 places from which a symbol can acquire its value:
- Template configuration (this).
- Host provided values, which override template configuration values. For example, the host may provide the operating system kind as "Linux", overriding a template default value of "Windows".
- User provided values, which override host & template values. The way these are provided to the template generation broker is specific to the broker. This may be via additional configuration files, command line parameters, inputs to a UI, etc.

#### Parameter symbol

A symbol for which the config provides literal and/or default values.

|Name|Description|
|---|---|
|`type`|`parameter`|
|`dataType`|	Supported values: <br />- `bool`: boolean type, possible values: `true`/`false`. <br />- `choice`: enumeration, possible values are defined in `choices` property.<br />- `float`: double-precision floating format number. Accepts any value that can be parsed by `double.TryParse()`.<br />- `int`/`integer`: 64-bit signed integer. Accepts any value that can be parsed by `long.TryParse()`.<br />- `hex`: hex number. Accepts any value that can be parsed by `long.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long convertedHex)`.<br />- `text`/`string`: string type.<br />- `<any other>`: treated as string.
|`defaultValue`|The value assigned to the symbol if no parameter is provided by the user or host.|
|`binding`|The name of the host property to take the value from.|	
|`replaces`|The text to be replaced by the symbol value in the template files content|	 
|`fileRename`|The portion of template filenames to be replaced by the symbol value.| 
|`description`|Human readable text describing the meaning of the symbol. This has no effect on template generation.|
|`isRequired`|Indicates if the parameter is required or not.|
|`choices`|List of available choices. Applicable only when `datatype=choice.` Contains array of the elements: <br />- `choice`: possible value of the symbol.<br />- `description`: human readable text describing the meaning of the choice. This has no effect on template generation. <br /> If not provided, there are no valid choices for the symbol, so it can never be assigned a value.|
|`onlyIf`| |
|`forms`|Defines the set of transforms that can be referenced by symbol definitions. Forms allow the specification of a "replaces"/"replacement" pair to also apply to other ways the "replaces" value may have been specified in the source by specifying a transform from the original value of "replaces" in configuration to the one that may be found in the source. [Details](https://github.com/dotnet/templating/wiki/Runnable-Project-Templates---Value-Forms)|

##### Examples
Boolean optional parameter with default value `false`:
```json
  "symbols": {
    "TestProject": {
      "type": "parameter",
      "dataType": "bool",
      "defaultValue": "false"
    }
  },
```
 
Choice optional parameter with 3 possible values:
```json
  "symbols": {
    "Framework": {
      "type": "parameter",
      "description": "The target framework for the project.",
      "datatype": "choice",
      "choices": [
        {
          "choice": "netcoreapp3.1",
          "description": "Target netcoreapp3.1"
        },
        {
          "choice": "netstandard2.1",
          "description": "Target netstandard2.1"
        },
        {
          "choice": "netstandard2.0",
          "description": "Target netstandard2.0"
        }
      ],
      "replaces": "netstandard2.0",
      "defaultValue": "netstandard2.0"
    }
}
```
 
String optional parameter, replaces TargetFrameworkOverride:
```json
 "symbols": {
    "TargetFrameworkOverride": {
      "type": "parameter",
      "description": "Overrides the target framework",
      "replaces": "TargetFrameworkOverride",
      "datatype": "string",
      "defaultValue": ""
    }
}
```

#### Derived symbol

A symbol that defines transformation of another symbol.  The value of this symbol is derived from the value of another symbol by the application of form defined in `valueTransform`.

|Name|Description|
|---|---|
|`type`|`derived`|
|`valueSource`|The name of the other symbol whose value will be used to derive this value.|
|`valueTransform`|The name of the value form to apply to the source value.|
|`replaces`|The text to be replaced by the symbol value in the template files content|	 
|`fileRename`|The portion of template filenames to be replaced by the symbol value.| 
|`description`|Human readable text describing the meaning of the symbol. This has no effect on template generation.|

##### Examples
Renames `Application1` file to value of `name` symbol after last dot
```json
{
...
  "symbols": {
    "app1Rename": {
      "type": "derived",  
      "valueSource": "name",  
      "valueTransform": "ValueAfterLastDot",  
      "fileRename": "Application1",  
      "description": "A value derived from the 'name' param, used to rename Application1.cs"
    }
  }
...
  "forms": {
    "ValueAfterLastDot": {
      "identifier": "replace",
      "pattern": "^.*\\.(?=[^\\.]+$)",        // regex to match everything up to and including the final "."
      "replacement": ""  // replace it with empty string
    }
  }
...
}
```

#### Generated symbol

A symbol whose value gets computed by a built-in symbol value generator. [Details](https://github.com/dotnet/templating/wiki/Reference-for-available-macros)

|Name|Description|
|---|---|
|`type`|`generated`|
|`generator`|Generator to use:<br />- [casing](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#casing) - enables changing the casing of a string.<br />- [coalesce](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#coalesce) - behaves like the C# ?? operator.<br />- [constant](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#constant) - constant value.<br />- [evaluate](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#evaluate) - evaluates a code expression (using C style syntax).<br />- [port](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#port) - generates a port number that can be used by web projects.<br />- [guid](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#guid) creates a new guid.<br />- [now](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#now) - get the current date/time.<br />- [random](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#random) - generate random integer value.<br />- [regex](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#regex) - processes a regular expression.<br />- [switch](https://github.com/dotnet/templating/wiki/Reference-for-available-macros#switch) - behaves like a C# switch statement.|
|`parameters`|The parameters for generator. See [description](https://github.com/dotnet/templating/wiki/Reference-for-available-macros) for each generator for details.|
|`description`|Human readable text describing the meaning of the symbol. This has no effect on template generation.|
|`replaces`|The text to be replaced by the symbol value in the template files content|	 
|`fileRename`|(supported in 5.0.200 or higher) The portion of template filenames to be replaced by the symbol value.| 

##### Example
 
- `myconstant`: replaces `1234` with `5001`
- `ownername`: replaces `John Smith (a)` with the value of the `ownername` parameter
- `nameUpper`: 
    - replaces `John Smith (U)` with the value of the `ownername` parameter in upper case in template files content
    - replaces `author_uc` with the value of the `ownername` parameter in upper case in filenames
- `nameLower`: 
    - replaces `John Smith (l)` with the value of the `ownername` parameter in lower case in template files content
    - replaces `author_lc` with the value of the `ownername` parameter in lower case in filenames
 
```json
"symbols":{
  "myconstant": {
    "type": "generated",
    "generator": "constant",
    "parameters": {
      "value":"5001"
    },
    "replaces":"1234"
  },
  "ownername":{
    "type": "parameter",
    "datatype":"text",
    "replaces": "John Smith (a)",
    "defaultValue": "John Doe"
  },
  "nameUpper":{
    "type": "generated",
    "generator": "casing",
    "parameters": {
      "source":"ownername",
      "toLower": false
    },
    "replaces":"John Smith (U)",
    "fileRename": "author_uc"
  },
  "nameLower":{
    "type": "generated",
    "generator": "casing",
    "parameters": {
      "source":"ownername",
      "toLower": true
    },
    "replaces":"John Smith (l)",
    "fileRename": "author_lc"
  }
}
```

#### Computed symbol

A symbol for which the config provides a Boolean predicate whose evaluation result is the computed symbol result.

|Name|Description|
|---|---|
|`type`|`computed`|
|`value`| Boolean expression to be computed.|
|`evaluator`|Language to be used for evaluation of expression: <br />- `C++2` - default<br />- `C++`<br />- `MSBUILD`<br />- `VB`|

##### Example

Values of `OrganizationalAuth`, `WindowsAuth`, `MultiOrgAuth`, `SingleOrgAuth`, `IndividualAuth`, `NoAuth`, `RequiresHttps` symbols are computed based on `auth` symbol.

```json
  "symbols": {
    "auth": {
      "type": "parameter",
      "datatype": "choice",
      "choices": [
        {
          "choice": "None",
          "description": "No authentication"
        },
        {
          "choice": "Individual",
          "description": "Individual authentication"
        },
        {
          "choice": "SingleOrg",
          "description": "Organizational authentication for a single tenant"
        },
        {
          "choice": "MultiOrg",
          "description": "Organizational authentication for multiple tenants"
        },
        {
          "choice": "Windows",
          "description": "Windows authentication"
        }
      ],
      "defaultValue": "Individual",
      "description": "The type of authentication to use"
    },
    "OrganizationalAuth": {
      "type": "computed",
      "value": "(auth == \"SingleOrg\" || auth == \"MultiOrg\")"
    },
    "WindowsAuth": {
      "type": "computed",
      "value": "(auth == \"Windows\")"
    },
    "MultiOrgAuth": {
      "type": "computed",
      "value": "(auth == \"MultiOrg\")"
    },
    "SingleOrgAuth": {
      "type": "computed",
      "value": "(auth == \"SingleOrg\")"
    },
    "IndividualAuth": {
      "type": "computed",
      "value": "(auth == \"Individual\")"
    },
    "NoAuth": {
      "type": "computed",
      "value": "(!(IndividualAuth || MultiOrgAuth || SingleOrgAuth || WindowsAuth))"
    },
    "RequiresHttps": {
      "type": "computed",
      "value": "(OrganizationalAuth)"
    }
  }
```

#### Bind symbol

|Name|Description|
|---|---|
|`type`|`bind`|
|`binding`|The name of the host property to take the value from.|
|`replaces`|The text to be replaced by the symbol value in the template files content|	 

 
##### Example  

```json
   "HostIdentifier": {
      "type": "bind",
      "binding": "HostIdentifier"
    }
```  

### Output Management
|Name|Description|Default|
|---|---|--|
|sourceName| The name in the source tree to replace with the name the user specifies. The value to be replaced with can be given using the `-n` `--name` options while running a template. The template engine will look for any occurrence of the name present in the config file and replace it in file names and file contents. If no name is specified by the host, the current directory is used. The value of the `sourceName` is available in built-in `name` symbol and can be used as the source for creating other symbols and condition expressions. || 
|preferNameDirectory| Boolean value, indicates whether to create a directory for the template if name is specified but an output directory is not set (instead of creating the content directly in the current directory).|If not specified, `false` is used.| 
|placeholderFilename|A filename that will be completely ignored expect to indicate that its containing directory should be copied. This allows creation of empty directory in the created template, by having a corresponding source directory containing just the placeholder file. Completely empty directories are ignored.|If not specified, a default value of `"-.-"` is used.|
|primaryOutputs|A list of template files that are used in post actions. The path should contain the relative path to the file prior to the renaming that may happen during template generation. It's defined as an array of [Primary Output](#primary-output-definition)||

#### Primary Output Definition
|Name|Description|
|---|---|
|path|Contains the relative path to the file in the template definition.|
|condition|If the condition evaluates to true, the corresponding path will be included in the output list. If false, the path is ignored. If no condition is provided for a path, the condition is defaulted to true.|
