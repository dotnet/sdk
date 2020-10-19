# Overview

## What do templates look like

Templates are simply a project that you like, plus one extra file (`.template.config\template.json`). Here's a template for a console application.

**Layout on disk**
```
/
    .template.config/
        template.json
    MyTemplate/
        Properties/
            AssemblyInfo.cs
        MyTemplate.csproj
        Program.cs
```

**/MyTemplate/Program.cs**
``` C#
using System;

namespace MyTemplate
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello world!");
        }
    }
}
```

**/MyTemplate/Properties/AssemblyInfo.cs**
``` C#
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("MyTemplate")]
[assembly: AssemblyTrademark("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("dc46e9be-12b0-43c5-ac94-5c7019d59196")]
```

**/MyTemplate/MyTemplate.csproj**
``` XML
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="14.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{dc46e9be-12b0-43c5-ac94-5c7019d59196}</ProjectGuid>
    <OutputType>Exe</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>MyTemplate</RootNamespace>
    <AssemblyName>MyTemplate</AssemblyName>
    <TargetFrameworkVersion>v4.5.2</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Xml.Linq" />
    <Reference Include="System.Data.DataSetExtensions" />
    <Reference Include="Microsoft.CSharp" />
    <Reference Include="System.Data" />
    <Reference Include="System.Net.Http" />
    <Reference Include="System.Xml" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
  <ItemGroup>
    <None Include="App.config" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
       Other similar extension points exist, see Microsoft.Common.targets.
  <Target Name="BeforeBuild">
  </Target>
  <Target Name="AfterBuild">
  </Target>
  -->
</Project>
```

**/.template.config/template.json**
``` JSON
{
  "author": "Your Name",
  "classifications": [ "Custom Templates" ],
  "name": "My first template",
  "identity": "MyName.MyTemplate.CSharp",
  "shortName": "helloworld",
  "guids": [ "dc46e9be-12b0-43c5-ac94-5c7019d59196" ],
  "sourceName": "MyTemplate"
}
```

## Benefits

* Code format not modified therefore exisitng editors work great for template content
* You can still build, run, debug and test your template just like any other project
* No injection of special templating tokens into the project is required

# Getting Started

## Content Authoring

The purpose of the "runnable projects" way of creating templates is that there's nothing special to do to create a template from a project unless there are some options that can be specified 
(beyond the name of the templated content and some GUIDs to regenerate).

## Configuration

Here's a complicated example of a configuration, we'll go over what each piece means below

``` JSON
{
  "$schema": "http://json.schemastore.org/template",
  "identity": "Contoso.Console.CSharp",
  "groupIdentity": "Contoso.Console",
  "author": "Contoso",
  "classifications": ["Common", "Console"],
  "name": "Contoso console template",
  "shortName": "contcon",
  "tags": {
    "language": "C#"
  },
  "sourceName": "MyTemplate",

  "primaryOutputs": [
	{
		"path": "newproject.csproj"
	},
	{
		"path": "newtextfile.txt",
		"condition": "(OperatingSystemKind == \"Linux\")"
	}
  ],
  "sources": [
    {
      "source": "./src/",
      "target": "./src/",
      "copyOnly": "**/*.txt",
      "exclude": "**/*.reg",
      "include": "**/*",
      "rename": {
        "file1": "file2"
      },
      "modifiers": [
        {
          "condition": "(IndividualAuth)",
          "exclude": "individual_auth.filelist"
        }
      ]
    },
    {
      "source": "./test/",
      "target": "./test/",
      "condition": "TestProject"
    }
  ],
  "guids": [
    "98048C9C-BF28-46BA-A98E-63767EE5E3A8",
    "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"
  ],
  "symbols": {
    "TestProject": {
      "type": "parameter",
      "dataType": "bool",
      "defaultValue": "false"
    }
  },
  "SpecialCustomOperations": {
    "**/*.json": {
      "variableFormat": {
        "sources": [
          {
            "name": "environment",
            "format": "{0}_env" 
          },
          { 
            "name": "user", 
            "format": "{0}_usr" 
          }
        ],
        "fallback": "{0}_default",
        "expand": true
      },
      "flagPrefix": "//",
      "operations": [
        {
          "type":  "include",
          "configuration": {
            "start": "json-start",
            "end": "json-end"
          }
        },
        {
          "type": "include",
          "configuration": {
            "start": "json-start-2",
            "end": "json-end-2"
          }
        }
      ]
    },
    "**/*.asp": {
      "flagPrefix": "<!--",
      "operations": [
        {
          "type": "conditionals",
          "configuration": {
            "if": "if",
            "else": "else",
            "elseif": "elseif",
            "endif": "endif",
            "evaluator": "C++"
          }
        }
      ]
    }
  },
  "postActions": [
    {
      "condition": "(!skipRestore)",
      "description": "Restore NuGet packages required by this project.",
      "manualInstructions": [
        { "text": "Run 'dotnet restore'" }
      ],
      "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
      "continueOnError": true
    }
  ]
}
```

### identity (optional)
A unique name for this template

### author (required)
The author of the template

### classifications (required)
Zero or more characteristics of the template that a user might search for it by

### name (required)
The name for the template that users should see

### groupIdentity (optional)
The ID of the group this template belongs to. When combined with the `tags` section, this allows multiple templates to be displayed as one, with the the decision for which one to use being presented as a choice in each one of the pivot categories (keys).

### tags (optional)
See `groupIdentity`

### shortName (required)
A default shorthand for selecting the template (applies to environments where the template name is specified by the user - not selected via a GUI)

### sourceName (optional)
The name in the source tree to replace with the name the user specifies

### placeholderFilename (optional)
A filename that will be completely ignored except to indicate that its containing directory should be copied. This allows creation of empty directory in the created template, by having a corresponding source directory containing just the placeholder file. Completely empty directories are ignored.
* If not specified, a default value of `"-.-"` is used.

### primaryOutputs (optional)
A list of important output paths created during template generation. These paths need to be added to the newly created project at the end of template creation. 

### primaryOutputs.path (optional)
One instance of a primary output path.

### primaryOutputs.condition (optional)
If the condition evaluates to true, the corresponding path will be included in the output list. If false, the path is ignored. If no condition is provided for a path, the condition is defaulted to true.

### sources (optional)
The set of mappings in the template content to user directories
* If not specified, an implicit source is created with `"source": "./"` and `"target": "./"`, all other properties in the source are set to their defaults

### sources.source (optional)
The path in the template content (relative to the directory containing the `.template.config` folder) that should be processed
* Default: `./`

### sources.target (optional)
The path (relative to the directory the user has specified) that content should be written to
* Default: `./`

### sources.include (optional)
The set of globbing patterns indicating the content to process in the path referred to by `sources.source`
* Default: `[ "**/*" ]`

### sources.exclude (optional)
The set of globbing patterns indicating the content that was included by `sources.include` that should not be processed
* Default: `[ "**/[Bb]in/**", "**/[Oo]bj/**", ".template.config/**/*", "**/*.filelist", "**/*.user", "**/*.lock.json" ]`

### sources.copyOnly (optional)
The set of globbing patterns indicating the content that was included by `sources.include`, that hasn't been excluded by `sources.exclude` that should be placed in the user's directory without modification
* Default: `[ "**/node_modules/**/*" ]`

### sources.rename (optional)
The set of explicit renames to perform. Each key is a path to a file in the source, each value is a path to the target location - only the values will be evaluated with the information the user supplies
* Default: None

### sources.condition (optional)
A Boolean-evaluable condition to indicate if the sources configuration should be included or ignored. If the condition evaluates to true or is not provided, the sources config will be used for creating the template. If it evaluates to false, the sources config will be ignored.

### sources.modifiers (optional)
A list of additional source information which gets added to the top-level source information, based on evaluation the corresponding source.modifiers.condition.

### sources.modifiers.condition (optional)
A Boolean-evaluable condition to indicate if the sources.modifiers instance should be included or ignored. If the condition evaluates to true or is not provided, the sources.modifiers instance will be used for creating the template. If it evaluates to false, the sources.modifiers config will be ignored.

### sources.modifiers.include (optional)
Include configuration specific to this sources.modifiers instance, contingent on the corresponding sources.modifiers.condition. See sources.include for more info.

### sources.modifiers.exclude (optional)
Exclude configuration specific to this sources.modifiers instance, contingent on the corresponding sources.modifiers.condition. See sources.exclude for more info.

### sources.modifiers.copyOnly (optional)
CopyOnly configuration specific to this sources.modifiers instance, contingent on the corresponding sources.modifiers.condition. See sources.copyonly for more info.

### guids (optional)
A list of guids which appear in the template source and should be replaced in the template output. For each guid listed, a replacement guid is generated, and replaces all occurrences of the source guid in the output.

### symbols (optional)
The `symbols` section defines variables and their values, the values may be the defined in terms of other symbols. See more information on symbols in [this](https://github.com/dotnet/templating/wiki/Reference-for-template.json#symbols) article.

### CustomOperations (optional) and SpecialCustomOperations (optional)
This configuration allows the template author to define custom actions for the template creation process. CustomOperations are scoped globally unless overridden by a more restrictive custom configuration.
SpecialCustomOperations are scoped to the files matched by their fileglob pattern.

Customizations allowed include:
* Variable formats
* Flag definitions
* Modification operations

There can only be one instance of the CustomOperations section, which will look like this:
``` JSON
  "CustomOperations": {
    // configuration details
  }
```

But since there can be many instances of the SpecialCustomOperations, each applicable to their fileglob patterns, the SpecialCustomOperations configuration section will look like this:

``` JSON
  "SpecialCustomOperations": {
    "<fileglob1>": {
      // configuration details
    },
    "<fileglob2>": {
      // configuration details
    },
  }
```

The `configuration details` options are identical in both situations, they only differ in the scopes they're applied in. The below sections are labelled CustomOperations.<Item>, but are equally applicable to SpecialCustomOperations.<fileglob>.<Item>


### CustomOperations.VariableFormat (optional)
Defines the format of variable names, and general processing infomation.
Need help documenting this!!!

### CustomOperations.FlagPrefix (optional)
Overrides the default prefix to indicate a flag option in a file being processed.

### CustomOperations.Operations (optional)
Defines custom operations for processing data in the files within the scope of the configuration. These operations are applied in addition to other operations setup by default, except in the case of the Conditional operation. If a custom conditional operation is defined, the default conditional operation(s) are ignored (in this scope).

Operations configurations are specified with an array named `operations`, whose entries are as follows: 

### CustomOperations.Operations.Type (required for each operation)
Indicates the type of operation being configured. The operations currently available to configure are:
* balancednesting
* conditional
* flag
* include
* region
* replacement

### CustomOperations.Operations.Condition (optional)
A boolean expression whose result determines whether or not to use this custom operation. If this is not provided, the operation is used.

### CustomOperations.Operations.Configuration (required for each operation)
The details of the operation configuration. Each type of operation has its own configuration options, as detailed below:

#### operation type = balancednesting - configuration:
This operation type is designed to be used in conjunction with a conditional operation, to help maintain the proper commenting on conditional, and the text they may optionally emit. See the section on configuring a custom conditional operation for more details.

#### operation type = conditional - configuration:
Conditional operations allow sections of template files to be optionally included during template creation.

The customizability of this operation facilitates preprocessing on many file formats for which there is no standard preprocessor syntax defined, by defining comment-based symbols to represent the preprocessor directives. 
By using comments recognized by the file formats, the unprocessed file content is valid even when it contains the customized preprocessor directives (since they're commented out as needed).

Conditional processing is based on the 4 conditional keywords:
* if
* elseif
* else
* endif

Other than the endif keyword, each keyword type can be defined in up to 2 flavors, "basic" and "actionable". The basic flavor indicates that if the condition is true, the contents should be emitted verbatim. The "actionable" flavor indicates that other actions should be taken on the content of the conditional - usually the other action is uncommenting.

Here is an example of defining a custom conditional operation (this is the actual predefined C-style line comments conditional configuration, which is the default for .json files)
``` JSON
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
```

Because there are actionable keywords, there is an "actions" field provided, which referes to other action(s) to activate when an actionable token is encountered. The other actions remain active until any other token in the conditional config is encountered. In this case, the other actions jobs are to remove comments, and to reduce consecutive double comments to a single comment. 

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

Note that the Id's in the replacement configurations correspond to the actions in the conditional configuration. This is what ties them together - it causes the replacements to be activated / deactivated when the actionable tokens are in / out of scope.

Let's look at a specific example of a template file being processed by this configuration, and see what happens:

``` JSON
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
#### 1) No appropriate conditional processing. In this case, the entire block above will be copied to the created template as-is. This includes all the commenting, i.e.:
``` JSON
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
... which means only the default content under the 'if' will be interpreted in the created template.

#### 2) A is true: Because the conditional token '//#if' was specified in the conditional configuration as an 'if' token (as opposed to an 'actionableIf' token), the 'actions' operations are not activated, so no commenting will change. Thus, the lines between the '//#if' and the '//#elseif' is the only part emitted to the created template, i.e.:
``` JSON
		// comment related to the 'if' content
		default content // also appropriate if A is true
```

#### 3) B is true (A is false): The elseif predicate is true, so only content under the elseif will be emitted to the created template. But '////#elseif' is an actionable token, so the 'actions' operations get activated, resulting in this text to be output:
``` JSON
		// comment related to the 'elseif' content
		content for when B is true and A is false
```
Note the comment changes from the original text - they're a results of the other 'actions'.

#### 4) Both A & B are false, so the else should happen. Note that '////#else' is an actionable token, resulting in this text to be output:
``` JSON
		// comment related to the 'else' content
		 content for when both A & B are false
```

Predefined conditional exist for each of these file / comment formats, and do not need custom configuration:
* C without comments
* C with line based comments, i.e. // comment to end of line
* C with block comments, i.e. /* Comment */
* Xml
* Razor
* "Rem" line comments, which are used in windows .bat & .cmd files


### postActions (optional)
Defines an ordered list of actions to perform after template generation. The post action information is provided to the creation broker, to act on as appropriate.

See the **[Post Action Registry](https://github.com/dotnet/templating/wiki/Post-Action-Registry)** for existing post actions.

### postActions.description (optional)
A human-readable description of the action.

### postActions.actionId (required)
A guid uniquely defining the action. The value must correspond to a post-action known by the broker.

### postActions.continueOnError (optional)
If this action fails, the value of continueOnError indicates whether to attempt the next action, or stop processing the post actions. Should be set to true when subsequent actions rely on the success of the current action.

### postActions.args (optional)
A list of key-value pairs to use when performing the action. The specific parameters required / allowed are defined by the action itself.

### postActions.manualInstructionInfo (required)
An ordered list of possible instructions to display if the action cannot be performed. Each element in the list must contain a key named "text", whose value contains the instructions. Each element may also optionally provide a key named "condition" - a Boolean evaluate-able string. The first instruction whose condition is false or blank will be considered valid, all others are ignored. 

### postActions.configFile (optional)
Additional configuration for the associated post action. The structure & content will vary based on the post action.

## Packaging
Currently, the template should be packed with [nuget.exe](https://dist.nuget.org/win-x86-commandline/latest/nuget.exe) (not [dotnet-pack](https://docs.microsoft.com/en-us/dotnet/articles/core/tools/dotnet-pack)).

The whole contents of the project folder, together with the `.template.config\template.json` file, needs to be placed into a folder named `content`. Besides the `content` folder there needs to be a [.nuspec file](https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package) created. A `packageTypes\packageType` element with the value `Template` should be present in that file.

Both the `content` folder and the `.nuspec` file should reside in the same file system location.

The package can then be [created](https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package#creating-the-package) using the `nuget pack <your_nuspec_file>.nuspec` command.