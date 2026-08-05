# Using group identity

## What is group identity

Template configuration features optional `groupIdentity` property.  From `template.json` documentation, `groupIdentity` is the ID of the group this template belongs to. This allows multiple templates to be displayed as one, with the decision for which one to use based on the template options.

It depends on template engine host implementation how `groupIdentity` is implemented. In this article, the implementation for dotnet CLI (`dotnet new`) will be explained. 

When developing the template, **it is important to create unique group identity to avoid clashes with other templates**.

The are 2 typical use cases for using `groupIdentity`:

- grouping the templates for different languages under the same short name 

Example: the console template for different languages
```
> dotnet new list console
These templates matched your input: 'console'

Template Name             Short Name    Language    Tags
------------------------  ------------  ----------  -----------------
Console App               console       [C#],F#,VB  Common/Console

```

- grouping the several templates under the same group identity that adds up different choice parameters to base template. The most known example for this use case is having `Framework` choice symbol listing supported frameworks for the template and implementing each supported framework in separate template. This also allows distributing different templates in different template packages if needed. 

When using groups, it is important to mention two other important properties from template configuration to pay attention too:
- `identity` - the template identity. **All the templates, including the individual templates in the template group, should have unique `identity`.**
- `precedence` - an integer value used to determine the precedence of this template among the other templates within the same group identity. The highest value is the preferred one. It is recommended to have different precedence values in all the templates in a group to avoid clashes during template resolution.

## Use case #1: how to use group identity to group templates for different languages

This is the most typical scenario for template groups. The example is a well-known console template. 
The template can be defined for only one language, however in dotnet CLI we often see the templates for multiple languages as:
```
> dotnet new list console
These templates matched your input: 'console'

Template Name             Short Name    Language    Tags
------------------------  ------------  ----------  -----------------
Console App               console       [C#],F#,VB  Common/Console

```
Typically, these are 3 individual templates put in the same template group.
You can see definition for this particular template here: [C# template](https://github.com/dotnet/sdk/tree/main/template_feed/Microsoft.DotNet.Common.ProjectTemplates.8.0/content/ConsoleApplication-CSharp), [F# template](https://github.com/dotnet/sdk/tree/main/template_feed/Microsoft.DotNet.Common.ProjectTemplates.8.0/content/ConsoleApplication-FSharp), [VB template](https://github.com/dotnet/sdk/tree/main/template_feed/Microsoft.DotNet.Common.ProjectTemplates.8.0/content/ConsoleApplication-VisualBasic).

To define a similar scenario, you need the following configuration for your templates defined in template.json:

C# template:
```json
  "author": "Me",
  "classifications": [
    "Common",
    "Library"
  ],
  "name": "My Awesome Template",
  "groupIdentity": "My.Awesome.Template.GroupID",
  "precedence": "100",
  "identity": "My.Awesome.Template.CSharp.1.0",
  "shortName": "awesome",
  "tags": {
    "language": "C#",
  },

```
F# template:
```json
  "author": "Me",
  "classifications": [
    "Common",
    "Library"
  ],
  "name": "My Awesome Template",
  "groupIdentity": "My.Awesome.Template.GroupID",
  "precedence": "100",
  "identity": "My.Awesome.Template.FSharp.1.0",
  "shortName": "awesome",
  "tags": {
    "language": "F#",
  },

```
VB template:
```json
  "author": "Me",
  "classifications": [
    "Common",
    "Library"
  ],
  "name": "My Awesome Template",
  "groupIdentity": "My.Awesome.Template.GroupID",
  "precedence": "100",
  "identity": "My.Awesome.Template.VB.1.0",
  "shortName": "awesome",
  "tags": {
    "language": "VB",
  },

```

With this configuration, after installed those templates will be shown in as
```
> dotnet new list awesome
These templates matched your input: 'awesome'

Template Name             Short Name    Language    Tags
------------------------  ------------  ----------  -----------------
My Awesome Template       awesome       [C#],F#,VB  Common/Library

```

The usage for those templates will be:
- `dotnet new awesome` - C# template will be run (as C# is default language)
- `dotnet new awesome --language F#` - F# template will be run
- `dotnet new awesome --language VB` - VB template will be run
- `dotnet new awesome --help` - the help for C# template will be shown (as C# is default language)
- `dotnet new awesome --help --language F#` - the help for F# template will be shown
- `dotnet new awesome --help --language VB` - the help for VB template will be shown

The templates in a group may be distributed via multiple template packages. This means that the author is usually able to define the new template for another language and group them with existing or default ones (if the author knows the group identity).

This means for previous example another author may define a different template for different language:
```json
  "author": "You",
  "classifications": [
    "Common"
  ],
  "name": "My Awesome Template",
  "groupIdentity": "My.Awesome.Template.GroupID",
  "precedence": "100",
  "identity": "My.Awesome.Template.Cust.1.0",
  "shortName": "awesome",
  "tags": {
    "language": "Cust",
  },

```
After new template is installed, it will be grouped together with templates:
```
Template Name             Short Name    Language    Author      Tags
------------------------  ------------  ----------  ----------  -----------------
My Awesome Template       awesome       [C#],F#,VB  Me          Common/Library
                                        Cust        You         Common
```


## Use case #2: how to use group identity to group templates with different choices in choice symbol

This is more advanced scenario for the grouping, and it is not visible in dotnet CLI for final user. Microsoft templates are using this scenario to split the templates for different target frameworks for the following reasons:
- so they can be distributed separately
- the templates for different target frameworks may differ a lot. Defining them in a separate template makes the template definition easier.

Console template is also one of such templates. You can see definition here: [8.0 C# template](https://github.com/dotnet/sdk/tree/main/template_feed/Microsoft.DotNet.Common.ProjectTemplates.8.0/content/ConsoleApplication-CSharp), [7.0 C# template](https://github.com/dotnet/sdk/tree/release/7.0.1xx/template_feed/Microsoft.DotNet.Common.ProjectTemplates.7.0/content/ConsoleApplication-CSharp).

To define a similar scenario, you need the following configuration for your templates defined in template.json:
.NET 8.0 C# template:
```json
  "author": "Me",
  "classifications": [
    "Common",
    "Library"
  ],
  "name": "My Awesome Template",
  "groupIdentity": "My.Awesome.Template.GroupID",
  "precedence": "800",
  "identity": "My.Awesome.Template.CSharp.8.0",
  "shortName": "awesome",
  "tags": {
    "language": "C#",
  },
  "symbols": {
    "Framework": {
        "type": "parameter",
        "description": "The target framework for the project.",
        "datatype": "choice",
        "choices": [
        {
            "choice": "net8.0",
            "description": "Target net8.0",
            "displayName": ".NET 8.0"
        }
        ],
        "replaces": "net8.0",
        "defaultValue": "net8.0",
        "displayName": "Framework"
    },
  }
```
.NET 7.0 C# template:
```json
  "author": "Me",
  "classifications": [
    "Common",
    "Library"
  ],
  "name": "My Awesome Template",
  "groupIdentity": "My.Awesome.Template.GroupID",
  "precedence": "700",
  "identity": "My.Awesome.Template.CSharp.7.0",
  "shortName": "awesome",
  "tags": {
    "language": "C#",
  },
  "symbols": {
    "Framework": {
        "type": "parameter",
        "description": "The target framework for the project.",
        "datatype": "choice",
        "choices": [
        {
            "choice": "net7.0",
            "description": "Target net7.0",
            "displayName": ".NET 7.0"
        }
        ],
        "replaces": "net7.0",
        "defaultValue": "net7.0",
        "displayName": "Framework"
    },
  }
```

Note:
- these configurations have different precedence values, so in case `Framework` parameter is not specified, the .NET 8.0 template is run
- these configurations have same choice symbol `Framework` defined, and they have different choice values.

With this configuration, after installed those templates will be shown in as
```
> dotnet new list awesome
These templates matched your input: 'awesome'

Template Name             Short Name    Language    Tags
------------------------  ------------  ----------  -----------------
My Awesome Template       awesome       [C#]        Common/Library

```
Note, there is no indication that group contains 2 templates.


The usage for those templates will be:
- `dotnet new awesome` - .NET 8 template is run, as its precedence is higher.
- `dotnet new awesome --Framework net8.0` - .NET 8 template is run (same as above)
- `dotnet new awesome --Framework net7.0` - .NET 7 template is run.
- `dotnet new awesome --Framework net6.0` - this is error, as net6.0 choice is not defined.

Note difference in showing help:
- `dotnet new awesome --help` - the combined help for both templates will be shown
```
...
Template options:
  -Framework <net8.0|net7.0>     The target framework for the project.
                                 Type: choice
                                    net8.0  Target net8.0
                                    net7.0  Target net7.0
                                 Default: net8.0
...
```

Note down:
- in case the templates have different parameter symbols defined, all of them are shown in help
- the choice parameters contain combined choices from all the templates in the group (here, net8.0 from 1st template. net7.0 from 2nd template)
- the help is shown always for a particular template language. If you are combining this use case with use case #1 (grouping for different language), only the templates for selected language are combined when the help is shown.

**When using this use case, it is very important to configure different precedence values in the template definition for all the templates in single template group. If any combination of template options results in more than one template resolved and their precedence values are not specified, or are the same, this combination is unusable and results in runtime error.**

## Links
- [Reference for `template.json`](Reference-for-template.json.md)
- [`template.json` JSON schema](https://json.schemastore.org/template.json)
- [Template resolution for `dotnet new`](./template-resolution-for-dotnet-cli.md)
- Open issues:
  - [#5358 Allow share template name for different languages](https://github.com/dotnet/templating/issues/5358)
  - [#5844 Item templates should pick correct language from project](https://github.com/dotnet/templating/issues/5844)
  - [#4135 Allow to pick up certain template group or certain template to be run in case of ambiguity](https://github.com/dotnet/templating/issues/4135)