There are several built in generators for computing parameter values. This page summarizes those.

| Name     | Description   |
|----------|---------------|
| [casing](#casing) | Enables changing the casing of a string. |
| [coalesce](#coalesce) | Behaves like the C# `??` operator. |
| [constant](#constant) | Constant value |
| [evaluate](#evaluate) | Evaluate a code expression (using C style syntax) |
| [port](#port) | Generate a port number that can be used by web projects. |
| [guid](#guid) | Create a new guid. |
| [now](#now) | Get the current date/time. |
| [random](#random) | Generate random int. |
| [regex](#regex) | Process a regular expression. |
| [switch](#switch) | Behaves like a C# `switch` statement. |

## Casing
Changes a symbol (like a parameter, generated value, etc) to all upper-case or all lower-case.  It does not affect spaces (i.e. does not do any sort of Camel Casing).

Implementation class: [`CaseChangeMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/CaseChangeMacro.cs)

```
"symbols":{
    "createdate": {
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
      "replaces":"John Smith (U)"
    },

    "nameLower":{
      "type": "generated",
      "generator": "casing",
      "parameters": {
        "source":"ownername",
        "toLower": true
      },
      "replaces":"John Smith (l)"
    }
}
```

## Coalesce

Implementation class: [`CoalesceMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/CoalesceMacro.cs)

```
  "symbols":{
    "MessageYear":{
      "type": "parameter",
      "datatype":"int"
    },
    "ThisYear":{
      "type": "generated",
      "generator": "now",
      "parameters": {
        "format": "yyyy"
      }
    },
    "YearReplacer": {
      "type": "generated",
      "generator": "coalesce",
      "parameters": {
        "sourceVariableName": "MessageYear",
        "fallbackVariableName": "ThisYear"
      },
      "replaces": "1234"
    }
  }
```

Note: coalesce only works in `new3` currently.

## Constant

Implementation class: [`ConstantMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/ConstantMacro.cs)

```
"symbols":{
    "myconstant": {
    "type": "generated",
    "generator": "constant",
    "parameters": {
      "value":"5001"
    },
    "replaces":"1234"
  }
}
```

## Evaluate

Implementation class: [`EvaluateMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/EvaluateMacro.cs)


## Port
Enables you to get the port number for an available port on the machine.

Implementation class: [`GeneratePortNumberMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/GeneratePortNumberMacro.cs)


## Guid

Enables you to create a formatted guid for a replacement.

Implementation class: [`GuidMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/GuidMacro.cs)

Reference for format parameters for the guid is available at https://msdn.microsoft.com/en-us/library/97af8hh4(v=vs.110).aspx.

```
"guids": [
  "4BC5DF1F-B155-4A69-9719-0AB349B1ACB2"
],
"symbols":{
  "id01":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid01",
    "parameters": {
      "format":"N"
    }
  },
  "id02":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid02",
    "parameters": {
      "format":"D"
    }
  },
  "id03":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid03",
    "parameters": {
      "format":"B"
    }
  },
  "id04":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid04",
    "parameters": {
      "format":"P"
    }
  },
  "id05":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid05",
    "parameters": {
      "format":"X"
    }
  }
}
```

## Now

Enables you to create a symbol from the current date/time.

Implementation class: [`NowMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/NowMacro.cs)

```
"symbols":{
  "createdate": {
    "type": "generated",
    "generator": "now",
    "parameters": {
    "format": "MM/dd/yyyy"
    },
    "replaces":"01/01/1999"
  }
}
```

## Random

Creates a random int.

Implementation class: [`RandomMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/RandomMacro.cs)

```
"symbols":{
  "myRandomNumber":{
    "type": "generated",
    "generator": "random",
    "parameters": {
    "low": 0,
    "high": 10000
    },
    "replaces": "4321"
  }
}
```

## Regex

Performs a replacement with a regular expression

Implementation class: [`RegexMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/RegexMacro.cs)

```
"symbols": {
  "regexExample": {
    "type": "generated",
    "generator": "regex",
    "dataType": "string",
    "replaces": "A different message",  //The value to replace in the output
    "parameters": {
      "source": "message",              //The name of the symbol whose value should be operated on
      "steps": [
        {
          "regex": "^test",             //The regular expression whose matches will be replaced with '[Replaced]`
          "replacement": "[Replaced]"   //The value to replace matches of the expression '^test' with
        },
        {
          "regex": "test$",
          "replacement": "[/Replaced]"
        }
      ]
    }
  }
}
```

## Switch

Selects a value based on conditions - if no condition evaluates to true, the value of the symbol is set to empty string

Implementation class: [`SwitchMacro`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/SwitchMacro.cs)

```
"symbols": {
  "regexExample": {
    "type": "generated",
    "generator": "switch",
    "dataType": "string",
    "replaces": "Some text",                          //The value to replace in the output
    "parameters": {
      "evaluator": "C++",                             //The name of the evaluator that handles the syntax in the conditions below (C++ is the default)
      "cases": [
        {
          "condition": "(symbol1 == 'hello world')",  //The condition to evaluate, if true, the associate value is used
          "value": "First case"                       //The value to use for this symbol if the condition evaluates to true
        },
        {
          "condition": "(symbol2 == 'hi there')",     //The condition to evaluate, if true, the associate value is used
          "value": "Second case"                      //The value to use for this symbol if the condition evaluates to true
        }
      ]
    }
  }
}
```
