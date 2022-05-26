Conditions are used to drive [dynamic content genarating or replacing](Conditional-processing-and-comment-syntax.md).

Conditions use C++ style of [conditional preprocessor expressions](https://docs.microsoft.com/en-us/cpp/preprocessor/hash-if-hash-elif-hash-else-and-hash-endif-directives-c-cpp?view=msvc-170). Expressions are composed from constant literals (strings, numbers, `true`, `false`), [operators](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Core/Expressions/Cpp/Operator.cs), [symbols](https://github.com/dotnet/templating/blob/main/docs/Available-Symbols-Generators.md), brackets and whitespaces. Only single line expressions are supported. Boolean and numerical expressions are supported (nonzero value is interpreted as `true`)

[Sample conditions in source code](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.Core.UnitTests/ConditionalTests.CStyleEvaluator.cs)

### Generated Conditions
Unlike C++ preprocessor conditions, template engine allows ability for using conditional expressions that are based on results of other expressions. Specifically [Evaluate](Available-Symbols-Generators.md#evaluate) and [Computed](Reference-for-template.json.md#computed-symbol) symbols can be leveraged for this purpose.

### Simplified extraction from [C# Console Application template](https://github.com/dotnet/templating/tree/main/template_feed/Microsoft.DotNet.Common.ProjectTemplates.7.0/content/ConsoleApplication-CSharp)

`template.json`:
```json
"symbols":{
    "langVersion": {
      "type": "parameter",
      "datatype": "text",
      "description": "Sets the LangVersion property in the created project file",
      "defaultValue": "",
      "replaces": "$(ProjectLanguageVersion)",
      "displayName": "Language version"
    },
    "csharp10orLater": {
      "type": "generated",
      "generator": "regexMatch",
      "datatype": "bool",
      "parameters": {
        "pattern": "^(|10\\.0|10|preview|latest|default|latestMajor)$",
        "source": "langVersion"
      }
    },
    "csharpFeature_ImplicitUsings": {
      "type": "computed",
      "value": "csharp10orLater == \"true\""
    },
}
```

`Program.cs`:
```C#
#if (!csharpFeature_ImplicitUsings)
using System;
#endif
```

### Choice literals

[Choice Symbol](Reference-for-template.json.md#examples) can have one of N predefined values. Those predefined values can be referenced in the conditions as quoted literals. Unquoted literals are as well supported as opt-in feature via [`enableQuotelessLiterals`](Reference-for-template.json.md#enableQuotelessLiterals). Following 2 expressions are equivalent when opted in:

`#if (PLATFORM == "Windows")`

`#if (PLATFORM == Windows)`

This allows for easier authoring of nested generated conditions.

### Multichoice literals

Information about multi-choice symbols can be found in [Reference for `template.json`](Reference-for-template.json.md#multichoice-symbols-specifics)

Comparison to multichoice symbol results in operation checking of a presence of any value of a multichoice parameter (meaning `==` operator behaves as `contains()` operation):

`template.json`:
```json
  "symbols": {
    "Platform": {
      "type": "parameter",
      "description": "The target platform for the project.",
      "datatype": "choice",
      "allowMultipleValues": true,
      "enableQuotelessLiterals": true,
      "choices": [
        {
          "choice": "Windows",
          "description": "Windows Desktop"
        },
        {
          "choice": "WindowsPhone",
          "description": "Windows Phone"
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

`Program.cs`:
```C#
#if (Platform = MacOS)
// MacOS choice flag specified here
#endif
```

In above example if `Platform` has it's default value (`MacOS` and `iOS`) or if those 2 values are passed to the engine (e.g. via commandline: `dotnet new MyTemplate --Platform MacOS --Platform iOS`), the condition in `Program.cs` file will be evaluated as true.

Order of operands doesn't matter - `PLATFORM == Windows` evaluates identical as `Windows == PLATFORM`. Comparing 2 multichoice symbols leads to standard equality check

### Using Computed Conditions to work with Multichoice Symbols

Cases that needs evaluation of different type of condition over multichoice symbols than 'contains' (e.g. exclusive equality or membership in subset of possible values) can be achieved with slightly more involved condition - so we recommend definition of aliases via computed conditions.

#### Example:

Lets consider following multichoice symbol:

`template.json`:
```json
  "symbols": {
    "PLATFORM": {
      "type": "parameter",
      "description": "The target platform for the project.",
      "datatype": "choice",
      "allowMultipleValues": true,
      "enableQuotelessLiterals": true,
      "choices": [
        {
          "choice": "Windows",
          "description": "Windows Desktop"
        },
        {
          "choice": "WindowsPhone",
          "description": "Windows Phone"
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
      "defaultValue": "WindowsPhone|iOS|android"
    }
}
```

Then Checking whether platform is a mobile platform can be performed with following condition: `(PLATFORM == android || PLATFORM == iOS || PLATFORM == WindowsPhone)  && PLATFORM != Windows && PLATFORM != MacOS && PLATFORM != nix`

Checking for one and only one platform needs similarly involved condition: `PLATFORM == android && PLATFORM != iOS && PLATFORM != WindowsPhone && PLATFORM != Windows && PLATFORM != MacOS`

This is given by the fact that we do not support exclusive equality operator (in the future, if needed, we can introduce dedicated operator for that - e.g. `===`).

To simplify templates and make them more readable - following computed conditions can be defined:

`template.json`:
```json
  "symbols": {
    "IsMobile": {
      "type": "computed",
      "value": "(PLATFORM == android || PLATFORM == iOS || PLATFORM == WindowsPhone)  && PLATFORM != Windows && PLATFORM != MacOS && PLATFORM != nix"
    },
    "IsAndroidOnly": {
      "type": "computed",
      "value": "PLATFORM == android && PLATFORM != iOS && PLATFORM != WindowsPhone && PLATFORM != Windows && PLATFORM != MacOS && PLATFORM != nix"
    },
}
```

Usage can then look as following:

`Program.cs`
```C#
#if IsAndroidOnly
// This renders for android only
#elseif IsMobile
// This renders for rest of mobile platforms
#else
// This renders for desktop platforms
#endif
```