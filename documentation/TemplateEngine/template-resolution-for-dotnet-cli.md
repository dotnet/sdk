# Template resolution for dotnet CLI

## Template resolution details

In dotnet new CLI, we use template groups in listings instead of individual
templates.

Template group is set of templates with same `groupIdentity` defined in
template.json. In case group identity is not set, the template is treated as
a template group on its own.

`dotnet new list` and `dotnet new search` show the list of template groups, not
all the templates.

Templates in a template group:
-   can have different languages (all the languages are shown in ‘Language’
    column in table listing)
-   can have different short names: in this case the short names can be equally used
    as synonyms in command input and will be shown in comma separated list in
    table listing.
-   are assumed to have the same type, name, author, and tags (classification). When
    showing the group, the information from the template with highest
    ‘precedence’ value will be shown; however, the template group will be
    considered as match if at least one template from the group is a match. For
    help and instantiating only the templates which match the given type and
    baseline will be considered.

When resolving the template to be shown/used:
-   first the template group is evaluated based on short name or name match.
    Name match/partial matches is only applicable for `list` and `search`.
-   when evaluating the template group, the supported languages are considered:
    if the template group has multiple languages defined and the user didn’t
    specify a language to use the default language (C\#) will be used. Otherwise
    the input results in error.
-   then the other matches of the group are evaluated based on the other
    template info filters (type, author, tag, baseline) and template specific
    parameters.
-   Starting with .NET SDK 7.0.100, the `list` command might not show all the templates installed on the machine. It takes the result of template constraints into account, and the templates that can't be used won't be shown. To force show all the templates, use the `--ignore-constraints` option.

In order to instantiate (create) the template, the command input should resolve
single template from single template group:
-   in case multiple template groups are resolved, it results in error.
-   in case of multiple templates within single template group are resolved, the
    precedence value from template.json will be used to determine the template
    to run. If the precedence value is not given or is the same for multiple
    templates, it results in error.

In order to show help for a template, the command input should resolve to a
single template group (based on short name). If the resolved group
contains multiple matching templates, they should have same language. In case of multiple templates,
the template parameters will be combined when displaying the help; the template
information will be taken from highest precedence template.

The command input for search and list may resolve multiple templates/template
groups: output is done in tabular format, where row corresponds to template
group.

## Instantiate template (dotnet new \<short name\>)

Argument (name) matching: exact short name
*Example:* `dotnet new console`

### Template info filters
-   Language
    -   If the user specified the language via `--language` option, the value should 
    exactly match the template language.
    -   If the template group has multiple languages defined, the default language
        is preferred
    -   If the template group has single language defined, this language is used

    -   *Example:* `dotnet new console --language F#`

-   Type
    -   Exact match on `--type` option
    -   *Example:* `dotnet new console --type project`

-   Baseline (hidden)
    -   Exact match on `--baseline` option
    -   *Example:* `dotnet new classlib --baseline standard`

### Template parameter filters

-   Exact match on parameter name
-   Exact match on parameter value
-   All required parameters should be specified.

-   *Example:* `dotnet new console --framework net7.0 --langVersion 9.0`

#### Error cases

**Invalid template parameter value (choice)**

```
> dotnet new console --framework invalid
Error: Invalid option(s):
--framework invalid
   'invalid' is not a valid value for --framework. The possible values are:
      net6.0          - Target net6.0
      net7.0          - Target net7.0
      netcoreapp3.1   - Target netcoreapp3.1

For more information, run:
   dotnet new console -h

For details on the exit code, refer to https://aka.ms/templating-exit-codes#127
```

**Invalid template parameter**

```
> dotnet new console --invalid
Error: Invalid option(s):
--invalid
   '--invalid' is not a valid option

For more information, run:
   dotnet new console -h

For details on the exit code, refer to https://aka.ms/templating-exit-codes#127
```

**Invalid language**

```
> dotnet new console --language invalid
No templates found matching: 'console', --language='invalid'.
Allowed values for '--language' option are: 'C#', 'F#', 'VB'.


For details on the exit code, refer to https://aka.ms/templating-exit-codes#103
```

**Combination of invalid parameters:**
```
> dotnet new console --language invalid --invalidParam
No templates found matching: 'console', --language='invalid'.
Allowed values for '--language' option are: 'C#', 'F#', 'VB'.


For details on the exit code, refer to https://aka.ms/templating-exit-codes#103
```

## List the templates (dotnet new list)

Argument (name) matching: exact or partial short name or name, optional.

*Example:* `dotnet new list`  
*Example:* `dotnet new list con`

### Template info filters

-   Language
    -   If user specified the language via `--language` option, only the template
        groups that match the language will be shown (exact match)
    -   The default language is not considered

    -   *Example:* `dotnet new list --language F#`
    -   *Example:* `dotnet new list con --language F#`

-   Type
    -   If user specified the type via `--type` option, only the template groups
        that match the type will be shown (exact match)

    -   *Example:* `dotnet new list --type project`
    -   *Example:* `dotnet new list con --type project`

-   Baseline (hidden)
    -   If user specified the baseline via `--baseline` option, only the template
        groups that match the baseline will be shown (exact match)

    -   *Example:* `dotnet new list --baseline standard`
    -   *Example:* `dotnet new list con --baseline standard`

-   Author
    -   If user specified the author via `--author` option, only the template
        groups that match the author will be shown (exact or partial match)

    -   *Example:* `dotnet new list --author Microsoft`
    -   *Example:* `dotnet new list con --author soft`

-   Tags
    -   If user specified the tag via `--tag` option, only the template groups
        that match the tag will be shown (exact match on single tag)

    -   *Example:* `dotnet new list --tag Common`
    -   *Example:* `dotnet new list con --tag Common`

### Template parameter filters

This feature is currently unsupported (support was removed with moving o a new command line parser) - but it's being [tracked](https://github.com/dotnet/templating/issues/4061)

### Error cases

#### No argument

**No match on language:**

```
dotnet new list --language invalid
No templates found matching: --language='invalid'.
73 template(s) partially matched, but failed on --language='invalid'.

To search for the templates on NuGet.org, run:
   dotnet new search [<template-name>]


For details on the exit code, refer to https://aka.ms/templating-exit-codes#103
```

### With argument

**No match on language:**

```
dotnet new list con --language invalid
No templates found matching: 'con', --language='invalid'.
17 template(s) partially matched, but failed on --language='invalid'.

To search for the templates on NuGet.org, run:
   dotnet new search con


For details on the exit code, refer to https://aka.ms/templating-exit-codes#103
```

## Search for the templates (dotnet new search)

Argument (name) matching: exact or partial (substring) short name or name. Optional if
filters applied.

*Example:* `dotnet new search con`

### Template info filters:

-   Language
    -   If user specified the language via `--language` option, only the template
        groups that match the language will be shown (exact match)
    -   The default language is not considered

    -   *Example:* `dotnet new search --language F#`
    -   *Example:* `dotnet new search con --language F#`

-   Type
    -   If user specified the type via `--type` option, only the template groups
        that match the type will be shown (exact match)

    -   *Example:* `dotnet new search --type project`
    -   *Example:* `dotnet new search con --type project`

-   Baseline (hidden)
    -   If user specified the baseline via `--baseline` option, only the template
        groups that match the baseline will be shown (exact match)

    -   *Example:* `dotnet new search --baseline standard`
    -   *Example:* `dotnet new search con --baseline standard`

-   Author
    -   If user specified the author via `--author` option, only the template
        groups that match the author will be shown (exact or partial match)

    -   *Example:* `dotnet new search --author Microsoft`
    -   *Example:* `dotnet new search con --author soft`

-   Tags
    -   If user specified the tag via `--tag` option, only the template groups
        that match the tag will be shown (exact match on single tag)

    -   *Example:* `dotnet new search --tag Common`
    -   *Example:* `dotnet new search con --tag Common`

-   Package name
    -   If user specified the package name via `--package` option, only the
        template groups that match the package name will be shown (exact or
        partial)

    -   *Example:* `dotnet new search --package Micro`
    -   *Example:* `dotnet new search con --package Micro`

### Template parameter filters

This feature is currently unsupported (support was removed with moving o a new command line parser) - but it's being [tracked](https://github.com/dotnet/templating/issues/4061)

### Error cases

#### No argument

**No match**
```
> dotnet new search --language invalid
Searching for the templates...
Matches from template source: NuGet.org
No templates found matching: --language='invalid'.


For details on the exit code, refer to https://aka.ms/templating-exit-codes#103
```


#### With argument
```
> dotnet new search con --language invalid
Searching for the templates...
Matches from template source: NuGet.org
No templates found matching: 'con', --language='invalid'.


For details on the exit code, refer to https://aka.ms/templating-exit-codes#103
```

## Template help (dotnet new \<short name\> [--help|-h])

Argument (name) matching: exact short name

*Example:* `dotnet new console -h`  
*Example:* `dotnet new console --help`

### Template info filters

-   Language

    -   If user specified the language via `--language` option, should be exact
        match
    -   If the template group has multiple languages defined, default language
        is preferred
    -   If the template group has single language defined, this language is used
    -   *Example:* `dotnet new console -h --language F#`

-   Type

    -   Exact match on `--type` option
    -   *Example:* `dotnet new console -h --type project`

-   Baseline (hidden)

    -   Exact match on `--baseline` option
    -   *Example:* `dotnet new classlib -h --baseline standard`*`
    -   Applying baseline will also show parameters applied by baseline

```
> dotnet new classlib -h
Class Library (C#)
Author: Microsoft
Description: A project for creating a class library that targets .NET or .NET Standard

Usage:
  dotnet new classlib [options] [template options]

Options:
  -n, --name <name>       The name for the output being created. If no name is specified, the name of the output
                          directory is used.
  -o, --output <output>   Location to place the generated output.
  --dry-run               Displays a summary of what would happen if the given command line were run if it would result
                          in a template creation.
  --force                 Forces content to be generated even if it would change existing files.
  --no-update-check       Disables checking for the template package updates when instantiating a template.
  --project <project>     The project that should be used for context evaluation.
  -lang, --language <C#>  Specifies the template language to instantiate.
  --type <project>        Specifies the template type to instantiate.

Template options:
  -f, --framework <choice>     The target framework for the project.
                               Type: choice
                                 net7.0          Target net7.0
                                 netstandard2.1  Target netstandard2.1
                                 netstandard2.0  Target netstandard2.0
                                 net6.0          Target net6.0
                                 netcoreapp3.1   Target netcoreapp3.1
                               Default: net7.0
  --langVersion <langVersion>  Sets the LangVersion property in the created project file
                               Type: text
  --no-restore                 If specified, skips the automatic restore of the project on create.
                               Type: bool
                               Default: false

To see help for other template languages (F#, VB), use --language option:
   dotnet new classlib -h --language F#
```

