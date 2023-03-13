## Overview

This page describes the available properties of `template.json` configuration file.

The templates defined using `template.json`are still the "runnable projects" A "runnable project" is a project that can be
executed as a normal project can. Instead of updating your source files to be tokenized you define replacements, and other processing, mostly in an external file,
the `template.json` file. This requires that a few changes have to be done on the fly during the preparation of the package, and in the `template.json` we define all the operations needed to create a well working package.

**Benefits of "runnable project" templates**
* Code format is not modified, therefore existing editors work great for template content
* You can still build, run, debug and test your template just like any other project
* No injection of special templating tokens into the project is required (though possible)

**Typical layout on disk**
```
  .template.config/
      template.json
  MyTemplate/
      MyTemplate.csproj
      Program.cs
```

The templates can be packaged to a NuGet package for further distribution.
A tutorial on how to create the template package can be find [here](https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-template-package).


|Category|Description|  
|------|------|     
|[Template Definition](#template-definition)|Metadata of the template|     
|[Content Manipulation](#content-manipulation)|Configuration of content changes in source files|
|[Output Management](#output-management)|Configuration of the final output|

### Template Definition
|Name|Description|Mandatory|
|---|---|---|
|`identity`|A unique name for this template|yes|
|`author`|The author of the template|no|
|<a name="classifications"></a>`classifications`| Zero or more characteristics of the template which are shown in the tabular output of `dotnet new list` and `dotnet new search` as "Tags". It is possible to filter the templates based on them using `--tag` option. In Visual Studio those items are shown in New Project Dialog in the available list of templates together with template name, description and language. Common classifications are: `Library`, `Test`, `Web` etc. |no|
|`name`|The name for the template. This is displayed as the template name when using `dotnet new` and Visual Studio.|yes|
|`groupIdentity`|The ID of the group this template belongs to. This allows multiple templates to be displayed as one, with the the decision for which one to use based on the template options. |no|
|`tags`|You can use tags to improve the metadata of your project. Well-known tags are: `language` and `type`. To specify the template language, use the tag `language`. To specify the template type, use the tag `type`. Supported types are: `project`, `item`, `solution`.|no|
|`shortName`|A name for selecting the template in CLI environment. This is the name shown as `Short Name` in `dotnet new` list of templates, and is the name to use to run this template. It is possible to specify multiple short names for the single template, and then any of them can be used to instantiate the template. The first element of the array is considered as the primary and first choice in scenarios where only single value is allowed (for example in tab completion of `dotnet new`). Be mindful when selecting the short name and using synonyms to avoid conflicts with other templates - the template short name should be unique. In case multiple template with the same short name are installed at the same time, the user won't be able to use any of them until one of the packages is uninstalled making the short name unique again. This doesn't apply for the templates that are part of the same group (have same `groupIdentity`). |yes|
|`postActions`|Enables actions to be performed after the project is created. See [the article](Post-Action-Registry.md) on post actions for more details.|no|
|`constraints`|[Template constraints](#template-constraints)||no|

#### Template constraints

Available since .NET SDK 7.0.100.
The template may define the constraints all of which must be met in order for the template to be used. 
In case constraints are not met, the template will be installed, however will not be visible by default. 
For the details on available constraints, refer to [the article](Constraints.md).
The constraints are defined under `constraints` property (top level in template.json). `constraints` contains objects (constraint definition). Each constraint should have a unique name, `type` and optional arguments (`args`). Argument syntax depends on the constraint implementation.

```json
"constraints": {
  "linux-only": {
    "type": "os",
    "args": "Linux"
  },
  "sdk-only": {
    "type": "host",
    "args": [
      {
        "hostname": "dotnetcli",
        "version": "[6.0.100, )"
      }
    ]
  }
}
```

### Content Manipulation
|Name|Description|Default|Mandatory|
|---|---|---|---|
|`sources`|The set of mappings in the template content to user directories. It's defined as any array of [Source](#source-definition) |If not specified, an implicit source is created with `"source": "./"` and `"target": "./"`, all other properties in the source are set to their defaults|no|
|`guids`|[Guids](#guids)||no|
|`symbols`|[Symbols](#symbols)||no|
|`forms`|[Value forms](Value-Forms.md)||no|

#### Source Definition
|Name|Description|Default|Mandatory|
|---|---|---|---|
|`source`|The path in the template content (relative to the directory containing the .template.config folder) that should be processed|`./`|no|
|`target`|The path (relative to the directory the user has specified) that content should be written to|`./`|no|
|`include`|The set of globing patterns indicating the content to process in the path referred to by `source`|[ "**/*" ]|no|
|`exclude` |The set of globing patterns indicating the content that was included by sources.include that should not be processed|`[ "**/[Bb]in/**", "**/[Oo]bj/**", ".template.config/**/*", "**/*.filelist", "**/*.user", "**/*.lock.json" ]`|no|
|`copyOnly` |The set of globing patterns indicating the content that was included by `include`, that hasn't been excluded by sources.exclude that should be placed in the user's directory without modification|`[ "**/node_modules/**/*" ]`|no|
|`rename`|The set of explicit renames to perform. Each key is a path to a file in the source, each value is a path to the target location - only the values will be evaluated with the information the user supplies||no|
|`condition`|A boolean condition to indicate if the sources configuration should be included or ignored. If the condition evaluates to `true` or is not provided, the sources config will be used for creating the template. If it evaluates to `false`, the sources config will be ignored||no|
|`modifiers`|A list of additional source information which gets added to the top-level source information, based on evaluation the corresponding `source.modifiers.condition`. Is defined as an array of [Modifier](#modifier-definition)||no|

#### Modifier Definition
|Name|Description|Mandatory|
|---|---|---|
|`condition`|A boolean condition to indicate if the `sources.modifiers` instance should be included or ignored. If the condition evaluates to `true` or is not provided, the `sources.modifiers` instance will be used for creating the template. If it evaluates to false, the `sources.modifiers` config will be ignored.|yes|
|`include`|Include configuration specific to this `sources.modifiers` instance, contingent on the corresponding `sources.modifiers.condition`. See `sources.include` for more info.|no|
|`exclude`|Exclude configuration specific to this `sources.modifiers` instance, contingent on the corresponding `sources.modifiers.condition`. See `sources.exclude` for more info|no|
|`copyOnly`|CopyOnly configuration specific to this `sources.modifiers` instance, contingent on the corresponding `sources.modifiers.condition`. See `sources.copyOnly` for more info|no|

### Guids
An optional list of guids which appear in the template source and should be replaced in the template output. For each guid listed, a replacement guid is generated, and replaces all occurrences of the source guid in the output. Matching of the guids in the source template works independently on the format and casing of the guids in the `template.json` file and source files. Format and casing from template source is preserved in the output (casing and format of the guid in `Guids` section of `template.json` doesn't influence matching or output generation).

##### Example
Sample template.json snippet:
```json
  "guids": [
    "98048C9C-BF28-46BA-A98E-63767EE5E3A8",
    "c7ab42cf938548c08b8784349ab5e04b"
  ],
```

Sample template file content:
```console
[n]: 98048c9cbf2846baa98e63767ee5e3a8
[d]: 98048c9c-bf28-46ba-a98e-63767ee5e3a8
[b]: {98048c9c-bf28-46ba-a98e-63767ee5e3a8}
[p]: (98048c9c-bf28-46ba-a98e-63767ee5e3a8)
[x]: {0x98048c9c,0xbf28,0x46ba,{0xa9,0x8e,0x63,0x76,0x7e,0xe5,0xe3,0xa8}}
[N]: 98048C9CBF2846BAA98E63767EE5E3A8
[D]: 98048C9C-BF28-46BA-A98E-63767EE5E3A8
[B]: {98048C9C-BF28-46BA-A98E-63767EE5E3A8}
[P]: (98048C9C-BF28-46BA-A98E-63767EE5E3A8)
[X]: {0X98048C9C,0XBF28,0X46BA,{0XA9,0X8E,0X63,0X76,0X7E,0XE5,0XE3,0XA8}}

[n]: c7ab42cf938548c08b8784349ab5e04b
[d]: c7ab42cf-9385-48c0-8b87-84349ab5e04b
[b]: {c7ab42cf-9385-48c0-8b87-84349ab5e04b}
[p]: (c7ab42cf-9385-48c0-8b87-84349ab5e04b)
[x]: {0xc7ab42cf,0x9385,0x48c0,{0x8b,0x87,0x84,0x34,0x9a,0xb5,0xe0,0x4b}}
[N]: C7AB42CF938548C08B8784349AB5E04B
[D]: C7AB42CF-9385-48C0-8B87-84349AB5E04B
[B]: {C7AB42CF-9385-48C0-8B87-84349AB5E04B}
[P]: (C7AB42CF-9385-48C0-8B87-84349AB5E04B)
[X]: {0XC7AB42CF,0X9385,0X48C0,{0X8B,0X87,0X84,0X34,0X9A,0XB5,0XE0,0X4B}}
```

Output content after template instantiation:
```console
[n]: a6d920ff125841318f5e07df108f5a4a
[d]: a6d920ff-1258-4131-8f5e-07df108f5a4a
[b]: {a6d920ff-1258-4131-8f5e-07df108f5a4a}
[p]: (a6d920ff-1258-4131-8f5e-07df108f5a4a)
[x]: {0xa6d920ff,0x1258,0x4131,{0x8f,0x5e,0x07,0xdf,0x10,0x8f,0x5a,0x4a}}
[N]: A6D920FF125841318F5E07DF108F5A4A
[D]: A6D920FF-1258-4131-8F5E-07DF108F5A4A
[B]: {A6D920FF-1258-4131-8F5E-07DF108F5A4A}
[P]: (A6D920FF-1258-4131-8F5E-07DF108F5A4A)
[X]: {0XA6D920FF,0X1258,0X4131,{0X8F,0X5E,0X07,0XDF,0X10,0X8F,0X5A,0X4A}}

[n]: 2879773c9a5241f78fa5ac318214f54d
[d]: 2879773c-9a52-41f7-8fa5-ac318214f54d
[b]: {2879773c-9a52-41f7-8fa5-ac318214f54d}
[p]: (2879773c-9a52-41f7-8fa5-ac318214f54d)
[x]: {0x2879773c,0x9a52,0x41f7,{0x8f,0xa5,0xac,0x31,0x82,0x14,0xf5,0x4d}}
[N]: 2879773C9A5241F78FA5AC318214F54D
[D]: 2879773C-9A52-41F7-8FA5-AC318214F54D
[B]: {2879773C-9A52-41F7-8FA5-AC318214F54D}
[P]: (2879773C-9A52-41F7-8FA5-AC318214F54D)
[X]: {0X2879773C,0X9A52,0X41F7,{0X8F,0XA5,0XAC,0X31,0X82,0X14,0XF5,0X4D}}
```

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

|Name|Description|Mandatory|
|---|---|---|
|`type`|`parameter`| yes |
|`dataType`|	Supported values: <br />- `bool`: boolean type, possible values: `true`/`false`. <br />- `choice`: enumeration, possible values are defined in `choices` property.<br />- `float`: double-precision floating format number. Accepts any value that can be parsed by `double.TryParse()`.<br />- `int`/`integer`: 64-bit signed integer. Accepts any value that can be parsed by `long.TryParse()`.<br />- `hex`: hex number. Accepts any value that can be parsed by `long.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long convertedHex)`.<br />- `text`/`string`: string type.<br />- `<any other>`: treated as string.| no, default - `string` |
|<a name="defaultValue"></a>`defaultValue`|The value assigned to the symbol if no parameter is provided by the user or host. The default is *not applied* if [`isRequired`](#isRequired) configuration is set to `true` for a parameter (or is set to condition that evaluates to `true`), as that is an indication that user specified value is required.  | no |
|`defaultIfOptionWithoutValue`|The value assigned to the symbol if explicit `null` parameter value is provided by the user or host.|no |
|<a name="replaces"></a>`replaces`|The text to be replaced by the symbol value in the template files content|	no | 
|`fileRename`|The portion of template filenames to be replaced by the symbol value.| 	no | 
|`description`|Human readable text describing the meaning of the symbol. This has no effect on template generation.|	no | 
|<a name="isRequired"></a>`isRequired`|Optional. Indicates if the user supplied value is required or not. Might be a fixed boolean value or a condition string that evaluates based on passed parameters - more details in [Conditions documentation](Conditions.md#conditional-parameters).<br/>If set to `true` (or condition that evaluates to `true`) and user has not specified the value on input, validation error occurs - even if [`defaultValue`](#defaultValue) is present.|	no | 
|<a name="isEnabled"></a>`isEnabled`| Optional condition indicating whether parameter should be processed. Might be a fixed boolean value or a condition string that evaluates based on passed parameters - more details in [Conditions documentation](Conditions.md#conditional-parameters).|	no | 
|`choices`|Applicable only when `datatype=choice.`<br />List of available choices. Contains array of the elements: <br />- `choice`: possible value of the symbol.<br />- `description`: human readable text describing the meaning of the choice. This has no effect on template generation. <br /> If not provided, there are no valid choices for the symbol, so it can never be assigned a value.| yes, for `dataType` = `choice` | 
|`allowMultipleValues`|Applicable only when `datatype=choice`.<br /> Enables ability to specify multiple values for single symbol.| no | 
|<a id="enableQuotelessLiterals"></a>`enableQuotelessLiterals`|Applicable only when `datatype=choice`. <br /> Enables ability to specify choice literals in conditions without quotation.|no |
|`onlyIf`| Defines the conditions that should be fulfilled in order that replacement happens. |no |
|`forms`|Defines the set of transforms that can be referenced by symbol definitions. Forms allow the specification of a "replaces"/"replacement" pair to also apply to other ways the "replaces" value may have been specified in the source by specifying a transform from the original value of "replaces" in configuration to the one that may be found in the source. [Details](Value-Forms.md)|no |

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
 
<a id="choice-sample"></a>Choice optional parameter with 3 possible values:
```json
  "symbols": {
    "Framework": {
      "type": "parameter",
      "description": "The target framework for the project.",
      "datatype": "choice",
      "enableQuotelessLiterals": true,
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

#### Multi-choice symbols specifics
Multi-choice symbols have similar behavior and usage scenarios as [C# Flag enums](https://docs.microsoft.com/en-us/dotnet/api/system.flagsattribute) - they express a range of possible values (not a single value - unlike the plain [choice symbol](#choice-sample))

##### Example definition of multi-choice symbol:

```json
"symbols": 
{
  "Platform": {
    "type": "parameter",
    "description": "The target platform for the project.",
    "datatype": "choice",
    "allowMultipleValues": true,  // multi-choice indicator
    "choices": [
      {
        "choice": "Windows",
        "description": "Windows Desktop"
      },
      {
        "choice": "MacOS",
        "description": "Macintosh computers"
      },
      {
        "choice": "iOS",
        "description": "iOS mobile"
      },
      {
        "choice": "android",
        "description": "android mobile"
      },
      {
        "choice": "nix",
        "description": "Linux distributions"
      }
    ],
    "defaultValue": "MacOS|iOS"
  }
}
```

There are some specifics in behavior of multi-choice symbols that are worth noting:

* Condition evaluation - closer described in [Conditions document](Conditions.md#multichoice-literals).
* [`Switch` symbol](Available-Symbols-Generators.md#switch) evaluation - conditions are evaluated by identical evaluator as preprocessing conditions (previous bullet point). 
* Argument passing and tab completion on CLI - User can specify multiple options via repeating the argument switch for each option:  
  `dotnet new MyTemplate --MyParameter value1 --MyParameter value2`

  or by passing multiple tokens to single option switch:

  `dotnet new MyTemplate --MyParameter value1 value2`  

  Tab completion works identically as for standard choice symbol

* Default values specification and API usage - Currently multiple values can be specified within single string separated by `|` or `,` characters. Escaping of those characters within values is currently not supported.
* Using multi-choice value as a string in template content - this can be achieved via leveraging the [`join` symbol](Available-Symbols-Generators.md#multichoice-join-sample). For simplicity it is as well possible to specify replacement for choice symbol with multiple values - in such case the values will be rendered into single string separated by `|` sign.



#### Derived symbol

A symbol that defines transformation of another symbol.  The value of this symbol is derived from the value of another symbol by the application of form defined in `valueTransform`.

|Name|Description|Mandatory|
|---|---|---|
|`type`|`derived`| yes |
|`valueSource`|The name of the other symbol which value will be used to derive this value.| yes |
|`valueTransform`|The name of the value form to apply to the source value.| yes |
|`replaces`|The text to be replaced by the symbol value in the template files content| no |	 
|`fileRename`|The portion of template filenames to be replaced by the symbol value.| no | 
|`description`|Human readable text describing the meaning of the symbol. This has no effect on template generation.| no | 

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

A symbol which value gets computed by a built-in symbol value generator. [Details](Available-Symbols-Generators.md)

|Name|Description|Mandatory|
|---|---|---|
|`type`|`generated`| yes |
|`generator`|Generator to use: See [this article](Available-Symbols-Generators.md) for more details.| yes |
|`parameters`|The parameters for generator. See [description](Available-Symbols-Generators.md) for each generator for details.| depends on generator |
|`replaces`|The text to be replaced by the symbol value in the template files content| no |	 
|`fileRename`|(supported in 5.0.200 or higher) The portion of template filenames to be replaced by the symbol value.| no | 
|`dataType`|	Supported values: <br />- `bool`: boolean type, possible values: `true`/`false`. <br />- `float`: double-precision floating format number. Accepts any value that can be parsed by `double.TryParse()`.<br />- `int`/`integer`: 64-bit signed integer. Accepts any value that can be parsed by `long.TryParse()`.<br />- `hex`: hex number. Accepts any value that can be parsed by `long.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long convertedHex)`.<br />- `text`/`string`: string type.<br />- `<any other>`: treated as string.| no, default: `string` |

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

|Name|Description|Mandatory|
|---|---|---|
|`type`|`computed`| yes |
|`value`| Boolean expression to be computed.| yes |
|`evaluator`|Language to be used for evaluation of expression: <br />- `C++2` <br />- `C++`<br />- `MSBUILD`<br />- `VB`| no, default: `C++2` |

Please note that math operations are not supported in expressions at this point.

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
See [the article](Binding-and-project-context-evaluation.md#bind-symbols).


### Output Management
|Name|Description|Default|Mandatory|
|---|---|---|---|
|`sourceName`| The text in the source content to replace with the name the user specifies. The value to be replaced with can be given using the `-n` `--name` options while running a template. The template engine will look for any occurrence of the `sourceName` present in the config file and replace it in file names and file contents. If no name is specified by the host, the current directory is used. The value of the `sourceName` is available in built-in `name` symbol and can be used as the source for creating other symbols and condition expressions. Due to implicit forms applied to `sourceName` it is important to select the replacement that doesn't cause the conflicts. See more details about `sourceName` in [the article](Naming-and-default-value-forms.md)||no|
|`preferDefaultName`| Boolean value that decides which name will be used if no `--name` is specified during creation. If `false` it will use the fallback name (output folder name), if `true` it will use the template's `defaultName`. If no `defaultName` and no fallback name is specified and `preferDefaultName` is set to `true`, the template creation will not succeed with a `TemplateIssueDetected` exception. | If not specified, `false` is used. | no
|`preferNameDirectory`| Boolean value, indicates whether to create a directory for the template if name is specified but an output directory is not set (instead of creating the content directly in the current directory).|If not specified, `false` is used.|no|
|`placeholderFilename`|A filename that will be completely ignored, used to indicate that its containing directory should be copied. This allows creation of empty directory in the created template, by having a corresponding source directory containing just the placeholder file. By default, empty directories are ignored.|If not specified, a default value of `"-.-"` is used.|no|
|`primaryOutputs`|A list of template files for further processing by the host (including post-actions). The path should contain the relative path to the file prior to the symbol based renaming that may happen during template generation. It is defined as an array of [Primary Outputs](#primary-output-definition)||no|

#### Primary Output Definition
Primary outputs define the list of template files for further processing, usually post actions. 
|Name|Description|Mandatory|
|---|---|---|
|`path`|should contain the relative path to the file after the template is instantiated.|yes|
|`condition`|if the condition evaluates to `true`, the corresponding path will be added to primary outputs, if `false`, the path is ignored. If no condition is provided for a path, the condition defaults to `true`.|no|

For more information on primary outputs, refer to [the article](Using-Primary-Outputs-for-Post-Actions.md).

### Global custom operations and special custom operations
This advanced configuration allows the template author to define custom actions for the template creation process. Global custom operations are scoped globally unless overridden by a more restrictive custom configuration.
Special custom operations are scoped to the files matched by their glob pattern.

Customizations allowed include:
* Flag definitions
* Modification operations

There can only be one instance of the (global) `customOperations` section, as follows:
```JSON
  "customOperations": {
    // configuration details
  }
```

But since there can be many instances of the SpecialCustomOperations, each applicable to their glob patterns, the `specialCustomOperations` configuration section is defined as follows:

```JSON
  "specialCustomOperations": {
    "<fileglob1>": {
      // configuration details
    },
    "<fileglob2>": {
      // configuration details
    },
  }
```

The configuration details options are identical in both situations, they only differ in the scopes they're applied in. The below sections are labelled `customOperations.<item>`, but are equally applicable to `specialCustomOperations.<fileglob>.<Item>`

|Name|Description|Mandatory|
|---|---|---|
|`flagPrefix`|Overrides the default prefix to indicate a flag option in a file being processed.|no|
|`operations`|An array that defines custom operations for processing data in the files within the scope of the configuration. These operations are applied in addition to other operations setup by default, except in the case of the conditional operation. If a custom conditional operation is defined, the default conditional operation(s) are ignored (in this scope).|no|
|`operations.type`|Indicates the type of operation being configured. The operations currently available to configure are: <br /> - `balancedNesting`<br /> - `conditional` <br /> - `flag` <br /> - `include`<br /> - `region` <br /> - `replacement`|yes|
|`operations.condition`|A boolean expression whose result determines whether or not to use this custom operation. If this is not provided, the operation is used.|no|
|`operations.configuration`|The details of the operation configuration. Each type of operation has its own configuration options, as detailed below.|yes|

SpecialCustomOperations can be used for adding conditions for handling files with extensions not listed in [Conditional processing and comment syntax](../docs/Conditional-processing-and-comment-syntax.md#introduction).

```JSON
{
  "SpecialCustomOperations": {
    "**.axml": {
      "operations": [
        {
          "type": "conditional",
          "configuration": {
            "endif": [ "#endif", "<!--#endif" ],
            "actionableIf": [ "<!--#if" ],
            "actionableElse": [ "#else", "<!--#else" ],
            "actionableElseif": [ "#elseif", "<!--#elseif", "#elif", "<!--#elif" ],
            "trim": true,
            "wholeLine": true,
            "evaluator": "C++"
          }
        }
      ]
    }
  }
}
```

#### Balanced nesting
This operation type is designed to be used in conjunction with a conditional operation, to help maintain the proper commenting on conditional, and the text they may optionally emit. See the section on configuring a custom conditional operation for more details.

#### Conditional
Conditional operations allow sections of template files to be optionally included during template creation.

The customization of this operation facilitates preprocessing on many file formats for which there is no standard preprocessor syntax defined, by defining comment-based symbols to represent the preprocessor directives. 
By using comments recognized by the file formats, the unprocessed file content is valid even when it contains the customized preprocessor directives (since they're commented out as needed).

Conditional processing is based on the 4 conditional keywords:
* `if`
* `elseif`
* `else`
* `endif`

Other than the `endif` keyword, each keyword type can be defined in up to 2 flavors, `basic` and `actionable`. The basic flavor indicates that if the condition is `true`, the contents should be emitted verbatim. The `actionable` flavor indicates that other actions should be taken on the content of the conditional - usually the other action is uncommenting.

Here is an example of defining a custom conditional operation (this is the actual predefined C-style line comments conditional configuration, which is the default for .json files)
``` JSON
{
  "type": "conditional",
  "configuration": {
    "if": [ "//#if" ],
    "else": [ "//#else" ],
    "elseif": [ "//#elseif" ],
    "endif": [ "//#endif" ],
    "actionableIf": [ "////#if" ],
    "actionableElse": [ "////#else" ],
    "actionableElseif": [ "////#elseif" ],
    "actions": [ "cStyleUncomment", "cStyleReduceComment" ],
    "trim": true,
    "wholeLine": true,
    "evaluator": "C++"
  },
}
```

Because there are actionable keywords, there is an "actions" field provided, which refers to other action(s) to activate when an actionable token is encountered. The other actions remain active until any other token in the conditional config is encountered. In this case, the other actions jobs are to remove comments, and to reduce consecutive double comments to a single comment. 

These other actions are defined as:
``` JSON
{
  "type": "replacement",
  "configuration": {
    "original": "//",
    "replacement": "",
    "id": "cStyleUncomment",
  },
},
{
  "type": "replacement",
  "configuration": {
    "original": "////",
    "replacement": "//",
    "id": "cStyleReduceComment",
  },
}
```

Note that the `id` in the replacement configurations correspond to the actions in the conditional configuration. This is what ties them together - it causes the replacements to be activated / deactivated when the actionable tokens are in / out of scope.

Let's look at a specific example of a template file being processed by this configuration, and see what happens:

```
...
//#if (A)
  // comment related to the 'if' content
  default content // also appropriate if A is true
////#elseif (B)
  //// comment related to the 'elseif' content
  //content for when B is true and A is false
////#else
  // comment related to the 'else' content
  // content for when both A & B are false
//#endif
...
```

There are 4 possible scenarios for when this appears in a template file.

1) No appropriate conditional processing. In this case, the entire block above will be copied to the created template as-is. This includes all the commenting, i.e.:
```
//#if (A)
  // comment related to the 'if' content
  default content // also appropriate if A is true
////#elseif (B)
  //// comment related to the 'elseif' content
  //content for when B is true and A is false
////#else
  //// comment related to the 'else' content
  // content for when both A & B are false
//#endif
```
which means only the default content under the 'if' will be interpreted in the created template.

2) A is `true`

Because the conditional token `//#if` was specified in the conditional configuration as an `if` token (as opposed to an `actionableIf` token), the `actions` operations are not activated, so no commenting will change. Thus, the lines between the `//#if` and the `/#elseif` is the only part emitted to the created template, i.e.:
```
  // comment related to the 'if' content
  default content // also appropriate if A is true
```

3) B is true (A is false)

The `elseif` predicate is `true`, so only content under the elseif will be emitted to the created template. But `////#elseif` is an actionable token, so the `actions` operations get activated, resulting in this text to be output:
```
  // comment related to the 'elseif' content
  content for when B is true and A is false
```
Note the comment changes from the original text - they're a results of the other 'actions'.

4) Both A & B are false
 
 The else should happen. Note that `////#else` is an actionable token, resulting in this text to be output:
```
  // comment related to the 'else' content
    content for when both A & B are false
```

For more details, refer to [Conditional processing](Conditional-processing-and-comment-syntax.md) article.
