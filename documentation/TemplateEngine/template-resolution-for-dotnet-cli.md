# Template resolution for dotnet CLI

## Template resolution details

In dotnet new CLI, we use template groups in listings instead of individual
templates.

Template group is set of templates with same `groupIdentity` defined in
template.json. In case group identity is not set, the template is treated as
a template group on its own.

`dotnet new --list` and `dotnet new --search` show the list of template groups, not
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
    Name match/partial matches is only applicable for `--list` and `--search`.
-   when evaluating the template group, the supported languages are considered:
    if the template group has multiple languages defined and the user didn’t
    specify a language to use the default language (C\#) will be used. Otherwise
    the input results in error.
-   then the other matches of the group are evaluated based on the other
    template info filters (type, author, tag, baseline) and template specific
    parameters.

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

-   *Example:* `dotnet new console --framework net5.0 --langVersion 9.0`

#### Error cases

**Invalid template parameter value (choice)**

```
> dotnet .\dotnet-new3.dll console --framework invalid
Error: Invalid option(s):
--framework invalid
   'invalid' is not a valid value for --framework. The possible values are:
      net5.0          - Target net5.0
      net6.0          - Target net6.0
      netcoreapp2.1   - Target netcoreapp2.1
      netcoreapp3.1   - Target netcoreapp3.1

For more information, run:
   dotnet new3 console -h

```

**Invalid template parameter**

```
> dotnet .\dotnet-new3.dll console --invalid
Error: Invalid option(s):
--invalid
   '--invalid' is not a valid option

For more information, run:
   dotnet new3 console -h

```

**Invalid language**

```
> dotnet .\dotnet-new3.dll console --language invalid
No templates found matching: 'console', language='invalid'.

To list installed templates, run:
   dotnet new3 --list
To search for the templates on NuGet.org, run:
   dotnet new3 console –search

```

**Combination of invalid parameters:**
```
> dotnet .\dotnet-new3.dll console --language invalid --invalidParam
No templates found matching: 'console', language='invalid'.

To list installed templates, run:
   dotnet new3 --list
To search for the templates on NuGet.org, run:
   dotnet new3 console --search

```

## List the templates (dotnet new --list)

Argument (name) matching: exact or partial short name or name, optional.

*Example:* `dotnet new --list`
*Example:* `dotnet new con --list`

### Template info filters

-   Language
    -   If user specified the language via `--language` option, only the template
        groups that match the language will be shown (exact match)
    -   The default language is not considered

    -   *Example:* `dotnet new --list --language F#`
    -   *Example:* `dotnet new con --list --language F#`

-   Type
    -   If user specified the type via `--type` option, only the template groups
        that match the type will be shown (exact match)

    -   *Example:* `dotnet new --list --type project`
    -   *Example:* `dotnet new con --list --type project`

-   Baseline (hidden)
    -   If user specified the baseline via `--baseline` option, only the template
        groups that match the baseline will be shown (exact match)

    -   *Example:* `dotnet new --list --baseline standard`
    -   *Example:* `dotnet new con --list --baseline standard`

-   Author
    -   If user specified the author via `--author` option, only the template
        groups that match the author will be shown (exact or partial match)

    -   *Example:* `dotnet new --list --author Microsoft`
    -   *Example:* `dotnet new con --list --author soft`

-   Tags
    -   If user specified the tag via `--tag` option, only the template groups
        that match the tag will be shown (exact match on single tag)

    -   *Example:* `dotnet new --list --tag Common`
    -   *Example:* `dotnet new con --list --tag Common`

### Template parameter filters

-   if template parameter is given without the value, list all the templates that
    have that parameter

    -   *Example:* `dotnet new --list --framework`
    -   *Example:* `dotnet new con --list --framework`

-   (choice only) if template parameter is given with a value, then list all the
    templates that have that parameter and the value fits the parameter constraints.

    -   *Example:* `dotnet new --list --framework net5.0`
    -   *Example:* `dotnet new con --list--framework net5.0`

-   (non-choice only) if template parameter is given with a value, then list all
    the templates that have that parameter and ignore the value

    -   *Example:* `dotnet new --list --langVersion 9.0` *(same as dotnet new --list
        \--langVersion)*
    -   *Example:* `dotnet new con --list -- langVersion 9.0` *(same as dotnet new
        con --list --langVersion)*

### Error cases

#### No argument

**No match on language:**

```
dotnet .\dotnet-new3.dll console --list --language invalid
No templates found matching: 'console', language='invalid'.
3 template(s) partially matched, but failed on language='invalid'.

To search for the templates on NuGet.org, run:
   dotnet new3 console --search

```

**Parameter matching errors:**

```
> dotnet .\dotnet-new3.dll --list --invalid
No templates found matching: --invalid.
26 template(s) partially matched, but failed on --invalid.

To search for the templates on NuGet.org, run:
   dotnet new3 <TEMPLATE_NAME> --search
> dotnet .\dotnet-new3.dll --list --framework invalid
No templates found matching: --framework='invalid'.
26 template(s) partially matched, but failed on --framework='invalid'.

To search for the templates on NuGet.org, run:
   dotnet new3 <TEMPLATE_NAME> --search

```

### With argument

**No match on language:**

```
dotnet .\dotnet-new3.dll con --list --language invalid
No templates found matching: 'con', language='invalid'.
9 template(s) partially matched, but failed on language='invalid'.

To search for the templates on NuGet.org, run:
   dotnet new3 con --search

```
**Parameter matching errors:**
```
> dotnet .\dotnet-new3.dll con --list --invalid
No templates found matching: 'con', --invalid.
9 template(s) partially matched, but failed on --invalid.

To search for the templates on NuGet.org, run:
   dotnet new3 con –search

> dotnet .\dotnet-new3.dll con --list --framework invalid
No templates found matching: 'con', --framework='invalid'.
9 template(s) partially matched, but failed on --framework='invalid'.

To search for the templates on NuGet.org, run:
   dotnet new3 con --search

```

## Search for the templates (dotnet new --search)

Argument (name) matching: exact or partial short name or name. Optional if
filters applied.

*Example:* `dotnet new con --search`

### Template info filters:

-   Language
    -   If user specified the language via `--language` option, only the template
        groups that match the language will be shown (exact match)
    -   The default language is not considered

    -   *Example:* `dotnet new --search --language F#`
    -   *Example:* `dotnet new con --search --language F#`

-   Type
    -   If user specified the type via `--type` option, only the template groups
        that match the type will be shown (exact match)

    -   *Example:* `dotnet new --search --type project`
    -   *Example:* `dotnet new con --search --type project`

-   Baseline (hidden)
    -   If user specified the baseline via `--baseline` option, only the template
        groups that match the baseline will be shown (exact match)

    -   *Example:* `dotnet new --search --baseline standard`
    -   *Example:* `dotnet new con --search --baseline standard`

-   Author
    -   If user specified the author via `--author` option, only the template
        groups that match the author will be shown (exact or partial match)

    -   *Example:* `dotnet new --search --author Microsoft`
    -   *Example:* `dotnet new con --search --author soft`

-   Tags
    -   If user specified the tag via `--tag` option, only the template groups
        that match the tag will be shown (exact match on single tag)

    -   *Example:* `dotnet new --search --tag Common`
    -   *Example:* `dotnet new con --search --tag Common`

-   Package name
    -   If user specified the package name via `--package` option, only the
        template groups that match the package name will be shown (exact or
        partial)

    -   *Example:* `dotnet new --search --package Micro`
    -   *Example:* `dotnet new con --search --package Micro`

### Template parameter filters

-   if template parameter is given without a value, list all the templates that
    have that parameter

    -   *Example:* `dotnet new --search --framework`
    -   *Example:* `dotnet new con --search --framework`

-   (choice only) if template parameter is given with a value, then list all the
    templates that have that parameter and value fits the parameter constraints.

    -   *Example:* `dotnet new --search --framework net5.0`
    -   *Example:* `dotnet new con --search --framework net5.0`

-   (non-choice only) if template parameter is given with a value, then list all
    the templates that have that parameter and ignore the value

    -   *Example:* `dotnet new --search --langVersion 9.0` *(same as dotnet new
        \--search --langVersion)*
    -   *Example:* `dotnet new con --search -- langVersion 9.0` *(same as dotnet new
        con --search --langVersion)*

### Error cases

#### No argument

**No match**
```
> dotnet .\dotnet-new3.dll --search --language invalid
Searching for the templates...
No templates found matching: language='invalid'.

```

**Template parameter matching**

```
> dotnet .\dotnet-new3.dll --search --invalid
Searching for the templates...
No templates found matching: --invalid.

> dotnet .\dotnet-new3.dll --search --invalid invalid
Searching for the templates...
No templates found matching: --invalid='invalid'.

```

#### With argument
```
> dotnet .\dotnet-new3.dll con  --search --invalid invalid
Searching for the templates...
No templates found matching: 'con', --invalid='invalid'.

> dotnet .\dotnet-new3.dll --search --invalid invalid
Searching for the templates...
No templates found matching: --invalid='invalid'.

```

## Template help (dotnet new \<short name\> --h)

Argument (name) matching: exact short name

*Example:* `dotnet new console -h`

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
> dotnet .\dotnet-new3.dll classlib -h
Class Library (C#)
Author: Microsoft
Description: A project for creating a class library that targets .NET Standard or .NET Core
Options:
  -f|--framework  The target framework for the project.
                      net6.0            - Target net6.0
                      netstandard2.1    - Target netstandard2.1
                      netstandard2.0    - Target netstandard2.0
                      net5.0            - Target net5.0
                      netcoreapp3.1     - Target netcoreapp3.1
                      netcoreapp2.1     - Target netcoreapp2.1
                  Default: net6.0

  --langVersion   Sets the LangVersion property in the created project file
                  text - Optional

  --no-restore    If specified, skips the automatic restore of the project on create.
                  bool - Optional
                  Default: false

  --nullable      Whether to enable nullable reference types for this project.
                  bool - Optional
                  Default: true

> dotnet .\dotnet-new3.dll classlib -h --baseline standard
Class Library (C#)
Author: Microsoft
Description: A project for creating a class library that targets .NET Standard or .NET Core
Options:
  -f|--framework  The target framework for the project.
                      net6.0            - Target net6.0
                      netstandard2.1    - Target netstandard2.1
                      netstandard2.0    - Target netstandard2.0
                      net5.0            - Target net5.0
                      netcoreapp3.1     - Target netcoreapp3.1
                      netcoreapp2.1     - Target netcoreapp2.1
                  Default: netstandard2.0

  --langVersion   Sets the LangVersion property in the created project file
                  text - Optional

  --no-restore    If specified, skips the automatic restore of the project on create.
                  bool - Optional
                  Default: false

  --nullable      Whether to enable nullable reference types for this project.
                  bool - Optional
                  Default: true

```

(note difference in default value for framework)

### Template parameter filters

(on hold until new parser)

-   If not specified all parameters will be shown
-   If specified:
    -   Only the templates with the parameter will be considered.
    -   If specified with value, the given value will be shown in the help.
