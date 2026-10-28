# Overview

Templates Testing Tooling is an engine for snapshot testing of templates instantiations - simplifying automated detection of regressions. It is surfaced via [API](#api) and [CLI tool](#cli).

Tooling currently uses [Verify](https://github.com/VerifyTests/Verify) as an underlying engine to manage the snapshots generation and verification. The API surface of Verify is however intentionally hidden and not accessible. The way how snapshots are being created, managed and diffed is unchanged - so to learn more about underlying diffing functionality refer to [DiffEngine](https://github.com/VerifyTests/DiffEngine) documentation.

Engine creates a serialized version of outputs of tested scenario and compares those with stored snapshot (if any) and reports success or any found discrepancies. Snapshot created during the first run can be used as a baseline for future runs of the test scenario.

Serialized snapshots are not mandatory - custom verification callback can be injected via [`CustomDirectoryVerifier`](#CustomDirectoryVerifier) which will not create nor validate any snapshot files, instead validation is expected to be performed by the injected custom logic.

## Naming of snapshots

All snapshots are stored in a single folder that is by default named `Snapshots` and is located in a folder with test code (in case of API usage) or current folder (in case of CLI) usage. Location of the snapshots directory can be optionaly specified in API via [SnapshotsDirectory](#SnapshotsDirectory) parameter or in CLI via [-d|--snapshots-directory](#--snapshots-directory) option.

All the files consisting a single snapshot of single scenario are stored in a single folder that is named with following convention: 

```
<caller_method>.<template_name>.<user_passe_scenario_name>.<encoded_input_args>.<scenario_differentiator>.<received|verified>
```

 - `<caller_method>` - not used in CLI. Added by default in API. Can be opted out via [`DoNotPrependCallerMethodNameToScenarioName`](#DoNotPrependCallerMethodNameToScenarioName)

 - `<template_name>` - used by default. Can be opted out in API via [`DoNotPrependTemplateNameToScenarioName`](#DoNotPrependTemplateNameToScenarioName)

  - `<user_passe_scenario_name>` - not used by default. Can be opted in in API via specifying [`ScenarioName`](#ScenarioName)

   - `<encoded_input_args>` - used by default. Can be opted out in API via [`DoNotAppendTemplateArgsToScenarioName`](#DoNotAppendTemplateArgsToScenarioName)

  - `<scenario_differentiator>` - not used by default. Can be opted in in CLI via [`--unique-for`](#--unique-for) and in API via [`UniqueFor`](#UniqueFor)

  - `<received|verified>` - suffix, depending on whether the snapshot is a basline for test scenario (then the suffix is '.verified') or created by engine as unexpected/regressing the check (then the suffix is '.received').


## Layout of snapshot files

All files within snapshot has identical filenames and relative folder structure as files in the generated template output.

Snapshot folder has the following layout:

```
<Snapshot_folder_name.received|verified>
  |--<template_name>
  |   |--<file_1 from generated template>
  |   |--<file_2 from generated template>
  |   |-- ... // layout is respecting folder structure of generated template
  |
  |--<std-streams> // present if opted in into verifying command output
      |--stderr.txt
      |--stdout.txt
```

# API

## Setup

1. Add the package reference to `Microsoft.TemplateEngine.Authoring.TemplateVerifier`:
  ```dotnetcli
  dotnet add package Microsoft.TemplateEngine.Authoring.TemplateVerifier
  ```
2. Start using the `TemplateVerifier` in your tests:
  ```cs
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;

namespace MyTests

[TestClass]
public class MyTestClass
{
    [TestMethod]
    public async Task MyTemplate_InstantiationTest()
    {
        TemplateVerifierOptions options = new(templateName: templateShortName)
        {
            TemplateSpecificArgs = new[] { "--arg1", "true" },
        };
        
        VerificationEngine engine = new(_logger);
        await engine.Execute(options).ConfigureAwait(false);
    }
}
  ```

## VerificationEngine

Type to be used to execute the test scenarios. Can be constructed by passing `Microsoft.Extensions.Logging.ILogger` or `Microsoft.Extensions.Logging.ILoggerFactory` (the latter one leads to usage of separate log instances for the `dotnet new` process execution and for the actual verification engine).

### Test scenario execution

```cs
async Task Execute(IOptions<TemplateVerifierOptions> optionsAccessor)
```

Successful run is indicated by `Task` that doesn't throw after awaiting. Failed scenario or snapshot expectation is indicated by `TemplateVerificationException` on the returned `Task`.

### Configuration

Configuration of the scenario run is done via `TemplateVerifierOptions` type.

- **`string TemplateName`** - The name of the template to be verified. Can be already installed template or a template within local path specified with `TemplatePath`

- **`string? TemplatePath`** - Applicable to local (not installed) template. The path to template.json file or containing directory.

- **`IEnumerable<string>? TemplateSpecificArgs`** - Instantiation arguments and options for the template.

- **<a name="SnapshotsDirectory"></a>`string? SnapshotsDirectory`** - Custom location of directory with snapshots. Defaults to `Snapshots`.

- **`bool VerifyCommandOutput`** - If set to true - 'dotnet new' command standard output and error contents will be verified along with the produced template files.

- **`bool IsCommandExpectedToFail`** - If set to true - 'dotnet new' command is expected to return nonzero return code. Otherwise a zero exit code and no error output is expected.

- **`bool DisableDiffTool`** - If set to true - the diff tool won't be automatically started by the `TemplateVerifier` on verification failures.

- **`bool DisableDefaultVerificationExcludePatterns`** - If set to true - all template output files will be verified, unless `VerificationExcludePatterns` are specified. Otherwise a default exclusions (binaries and object files).

- **`IEnumerable<string>? VerificationExcludePatterns`** - Set of patterns defining files to be excluded from verification. [Globs](https://en.wikipedia.org/wiki/Glob_(programming)) patterns are recognized and honored.

- **`IEnumerable<string>? VerificationIncludePatterns`** - Set of patterns defining files to be included into verification (unless excluded by `VerificationExcludePatterns`). By default all files are included (unless excluded). [Globs](https://en.wikipedia.org/wiki/Glob_(programming)) patterns are recognized and honored.

- **`string? OutputDirectory`** - Target directory to output the generated template. If explicitly specified, it won't be cleaned up upon successful run of test scenario.

- **`string? SettingsDirectory`** - Settings directory for template engine (in memory location used if not specified).

- **<a name="UniqueFor"></a>`UniqueForOption? UniqueFor`** - Indicating which scenarios should be differentiated. Differentiation performed by adjusting the snapshots directory name (see )

  Currently supported values:
    - Architecture
    - OsPlatform
    - Runtime
    - RuntimeAndVersion
    - TargetFramework
    - TargetFrameworkAndVersion

- **`string? DotnetExecutablePath`** - Path to custom dotnet executable (e.g. x-copy install scenario).

- **`IReadOnlyDictionary<string, string>? Environment`** - Custom environment variable collection to be passed to execution of dotnet commands.

  Custom environment variables can as well be added via fluent API `WithCustomEnvironment` and `WithEnvironmentVariable`. Sample usage:

  ```cs
  options.WithCustomEnvironment(
	new Dictionary<string, string>() 
	  { 
		  { name1, value1 }, 
		  { name2, value2 },
		  { name3, value3 },
	  }
	);
  ```


- **<a name="ScenarioName"></a>`string? ScenarioName`** - Custom scenario name; if specified it will be used as part of verification snapshot name.

- **<a name="DoNotAppendTemplateArgsToScenarioName"></a>`bool DoNotAppendTemplateArgsToScenarioName`** - `true`, if the instantiation args should not be appended to verification snapshot name.

- **<a name="DoNotPrependTemplateNameToScenarioName"></a>`bool DoNotPrependTemplateNameToScenarioName`** - `true`, if the template name should not be prepended to verification snapshot name.

- **<a name="DoNotPrependCallerMethodNameToScenarioName"></a></a>`bool DoNotPrependCallerMethodNameToScenarioName`** - `true`, if the caller method name should not be prepended to verification snapshot name.

- **`string? StandardOutputFileExtension`** - Extension of autogeneratedfiles with stdout and stderr content. Defaults to `.txt`.

- **`ScrubbersDefinition? CustomScrubbers`** - Delegates that perform custom scrubbing of template output contents before verifications.

  Scrubbers can as well be added via fluent API `WithCustomScrubbers`. Sample usage:
  ```cs
  options.WithCustomScrubbers(
	ScrubbersDefinition.Empty
		.AddScrubber(sb => sb.Replace("bb", "xx"), "cs")
		.AddScrubber(sb => sb.Replace("cc", "yy"), "csproj")
		// scrubber applicable to all files
		.AddScrubber(sb => sb.Replace("aa", "zz"))
		// supports multiple scrubbers per extension
		.AddScrubber(sb => sb.Replace("cc", "**"), "cs")
		// callback scrubber based on content filename or/and location
		.AddScrubber((path, content) =>
		{
			if (Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase))
			{
				content.Replace("MyTemplate", "%TEMPLATE_NAME%");
			}
		}));
  ```

- **<a name="CustomDirectoryVerifier"></a>`VerifyDirectory? CustomDirectoryVerifier`** - Delegate that performs custom verification of template output contents.

  Custom verifier can as well be defined via fluent API `WithCustomDirectoryVerifier`. Sample usage:

  ```cs
  options.WithCustomDirectoryVerifier(
	async (contentDirectory, contentFetcher) =>
	{
		await foreach (var (filePath, scrubbedContent) in contentFetcher.Value)
		{
			// Asserting on scrubbed file content
		}
	});
  ```



# CLI

## Setup

1. If you haven't setup your tools manifest file yet - set it up by running:
  ```dotnetcli
  dotnet new tool-manifest
  ```
2. Install the templeting authoring toolset:
  ```dotnetcli
  dotnet tool install Microsoft.TemplateEngine.Authoring.CLI
  ```

## Synopsis

```dotnetcli
dotnet dotnet-template-authoring verify <template-short-name>  [--template-args <template-args>] [-p|--template-path <template-path>] [-o|--output <output>] [-d|--snapshots-directory <snapshots-directory>] [--scenario-name <scenario-name>] [--disable-diff-tool] [--disable-default-exclude-patterns] [--exclude-pattern <exclude-pattern>] [--include-pattern <include-pattern>] [--verify-std] [--fail-expected] [--unique-for <Architecture|OsPlatform|Runtime|RuntimeAndVersion|TargetFramework|TargetFrameworkAndVersion>]
    [-h|--help]
```

## Description

The `dotnet dotnet-template-authoring verify` command instantiates the specified while passing specified arguments, collects the resulting files (while applying filtering based on default and explicit exclude and include patterns) compares them to stored snapshots and reports the result.

If there are differences or snapashot files are missing altogether (first run), the set of files is created within a folder with a `.received` suffix, that can be used to prepare proper snapshot files (those should be placed in folder with `.verified` suffix).



## Arguments
- **`<template-short-name>`**

  Name of the template to be verified. Can be already installed template or a template within local path specified with `-p|--template-path option`.

## Options

- **`--template-args <template-args>`**

  Template specific arguments - all joined into single enquoted string. Any needed quotations of actual arguments has to be escaped.


- **`-p|--template-path <template-path>`**

  Specifies the path to the directory with template to be verified.

- **`-o|-output<output>`**

  Specifies the path to target directory to output the generated template to.

- **<a name="--snapshots-directory"></a>`-d|--snapshots-directory <snapshots-directory>`**

  Specifies path to the directory with snapshot files.

- **`--scenario-name <scenario-name>`**

  Specifies optional scenario name to be used in the snapshot folder name.

- **`--disable-diff-tool`**

  If set to true - the diff tool won't be automatically started by the Verifier on verification failures.

- **`--disable-default-exclude-patterns`**

  If set to true - all template output files will be verified, unless --exclude-pattern option is used.

- **`--exclude-pattern <exclude-pattern>`**

  Specifies pattern(s) defining files to be excluded from verification.

- **`--include-pattern <include-pattern>`**

  Specifies pattern(s) defining files to be included to verification (all files are included if not specified).

- **`--verify-std`**

  If set to true - 'dotnet new' command standard output and error contents will be verified along with the produced template files.

- **`--fail-expected`**

  If set to true - 'dotnet new' command is expected to return non-zero return code.

- **<a name="--unique-for"></a>`--unique-for <Architecture|OsPlatform|Runtime|RuntimeAndVersion|TargetFramework|TargetFrameworkAndVersion>`**

  Sets the Verifier expectations directory naming convention, by indicating which scenarios should be differentiated.



  ## Examples

- Get the help for `verify` command
  
  ```dotnetcli
  dotnet dotnet-template-authoring verify --help
  ```

- Run the test of instantiation scenario for already installed template:
 
  ```dotnetcli
  dotnet dotnet-template-authoring verify my-template-name --template-args "--arg1 value1 --arg2 \"multi word value\""
  ```
    
- Run the test of instantiation scenario for an installed template with verification of standard output and standard error streams of `dotnet new` process, while disabling automatic opening of a diff tool for snapshots:
 
  ```dotnetcli
  dotnet dotnet-template-authoring verify console --verify-std --disable-diff-tool
  ```
    
