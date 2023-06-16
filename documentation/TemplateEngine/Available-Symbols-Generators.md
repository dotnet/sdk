Inside the `template.json` file, you can define custom symbols that will be used inside the template files. 
The supported symbol types are:
- Parameter - the value is typically provided by the user when creating the template. If not provided, the value is taken from host configuration, otherwise default value is used. 
- Derived - defines transformation of another symbol.  The value of this symbol is derived from the value of another symbol by applying the defined form.
- Computed - the boolean value is evaluated during the processing of the template based on the other symbol values.
- Generated - the value is computed by a built-in symbol value generator.
There are no restrictions on symbol order: e.g. generated/computed/derived symbols can be used in any type of other symbols. The only rule is in avoiding circular dependencies such as:
```json
"symbols": {
  "switchCheck": {
    "type": "generated",
    "generator": "switch",
    "datatype": "string",
    "parameters": {
      "evaluator": "C++",
      "cases": [
        {
          "condition": "(switchCheck2 == 'regions')",
          "value": "regions"
        }
      ]
    }
  },
  "switchCheck2": {
    "type": "generated",
    "generator": "switch",
    "datatype": "string",
    "parameters": {
      "evaluator": "C++",
      "cases": [
        {
          "condition": "(switchCheck == 'regions')",
          "value": "regions"
        }
      ]
    }
  }  
```
This article covers available generators for generated symbols.

To use a generated symbol inside your `template.json` file:
1. Add `"type": "generated"` to the symbol definition
1. Use the `"generator": ...` parameter to select the generator to use.    
This is a sample of definition of a generated symbol, the `port` generator, that generates a random number for an http port.    

```json
"IISExpressPortGenerated": {    
  "type": "generated",    
  "generator": "port",
  "parameters": {
    "fallback":"5000"
  }  
},   
```

Most of the generators need to be configured via parameters that let you select the source of the data and select among the options available. Below is a sample of a symbol that use the `now` generator to replace a fixed year indication present in the source files with the current year.

```json
"copyrightYear": {
  "type": "generated",
  "generator": "now",
  "replaces": "1975",
  "parameters": {
    "format": "yyyy"
  }
},
```

Available built-in generators for computing generated symbols values are:

| Name     | Description   |
|----------|---------------|
| [casing](#casing) | Enables changing the casing of a string. |
| [coalesce](#coalesce) | Behaves like the C# `??` operator. |
| [constant](#constant) | Constant value |
| [port](#port) | Generate a port number that can be used by web projects. |
| [guid](#guid) | Create a new guid. |
| [now](#now) | Get the current date/time. |
| [random](#random) | Generate random int. |
| [regex](#regex) | Process a regular expression. |
| [regexMatch](#regexmatch) | Checks if the value matches the regex pattern. |
| [switch](#switch) | Behaves like a C# `switch` statement. |
| [join](#join) | Concatenates multiple symbols or constants. |

## Casing
Changes the case of the text of the source value to all upper-case or all lower-case.  It does not affect spaces (i.e. does not do any sort of Camel Casing).

#### Parameters
| Name     |Data Type| Description |Mandatory|
|----------|------|---------------|---|
|`source`|`string`| The name of symbol to use as the source of data.| yes |
|`toLower`|`bool`| applies lower case if `true`, upper case otherwise| no |

### Samples

In this sample three symbols are defined:     
 - `ownerName` is a parameter which can be set on the command line using `dotnet new` It has a default value of "John Doe", that will be used if the no value is received from the host. The value will be used to replace "John Smith (a)".
 - `nameUpper` and `nameLower` are the symbols that generate the upperCase and lowerCase version of `ownerName` that are used to replace any instance of "John Smith (U)" and "John Smith (l)". 

```json
"symbols":{
    "ownerName":{
      "type": "parameter",
      "datatype":"text",
      "replaces": "John Smith (a)",
      "defaultValue": "John Doe"
    },

    "nameUpper":{
      "type": "generated",
      "generator": "casing",
      "parameters": {
        "source":"ownerName",
        "toLower": false
      },
      "replaces":"John Smith (U)"
    },

    "nameLower":{
      "type": "generated",
      "generator": "casing",
      "parameters": {
        "source":"ownerName",
        "toLower": true
      },
      "replaces":"John Smith (l)"
    }
}
```

### Related

[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/CaseChangeMacro.cs)
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/11-change-string-casing)


## Coalesce
Behaves like the C# `??` operator. Note: the empty string value and default value of value type is treated as `null`.
The typical use of this generator is to check if the parameter was provided by user, otherwise set fallback generated value.

#### Parameters

|Name|Data Type|Description|Mandatory|
|---|---|---|---|
|`sourceVariableName`|`string`|the symbol name which is a primary source of data (left operand of `coalesce`)|yes|
|`fallbackVariableName`|`string`|the symbol name which is an alternate source of data(right operand of `coalesce`)|yes|
|`defaultValue`|`string`|The default value. In case it is specified, and primary source is equal to this value, the fallback value will be used.|no|

### Samples

In this sample three symbols are defined:
 - `MessageYear` - is a parameter set by the user when calling `dotnet new`.   
 - `ThisYear` - use the now generator to calculate the current year.
 - `YearReplacer` - ensures that any occurrence of "1234" is replaced. If `MessageYear` was passed in by the user that value will be used. Otherwise `ThisYear` will be used.

```json
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

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/CoalesceMacro.cs)

## Constant

Uses constant value.

#### Parameters

|Name|Data Type|Description|Mandatory|
|----------|------|---------------|---|
|`value`|`string`|constant value|yes|


### Samples

`myConstant` is a symbol that replaces "1234" with "5001"

```json
"symbols":{
  "myConstant": {
    "type": "generated",
    "generator": "constant",
    "parameters": {
      "value":"5001"
    },
    "replaces":"1234"
  }
}
```

### Related

[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/ConstantMacro.cs)
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/13-constant-value)


## Port
Gets an available port number on the machine.   
During evaluation looks for a valid free port number trying to create a socket, and in case of problems, returns the value defined in the `fallback` parameter.

#### Parameters

|Name|Data Type|Description|Mandatory|
|----------|------|---------------|---|
|`high`|`integer`|defined the high bound of range to select port from. The maximum value is `65535`. If greater value is specified, `65535` is used instead. |no, default: `65535`|
|`low`|`integer`|defined the low bound of range to select port from. The minimum value is `1024`. If less value is specified, `1024` is used instead.|no, default: `1024`|
|`fallback`|`integer`|fallback value|no, default: `0`|

Note: if `low` > `high`, the default values for `low` and `high` are used: 1024 - 65535.

The following ports are reserved:
- 1719 - H323 (RAS)
- 1720 - H323 (Q931)
- 1723 - H323 (H245)
- 2049 - NFS
- 3659 - apple-sasl / PasswordServer [Apple addition]
- 4045 - lockd
- 4190 - ManageSieve [Apple addition]
- 5060 - SIP
- 5061 - SIPS
- 6000 - X11
- 6566 - SANE
- 6665 - Alternate IRC [Apple addition]
- 6666 - Alternate IRC [Apple addition]
- 6667 - Standard IRC [Apple addition]
- 6668 - Alternate IRC [Apple addition]
- 6669 - Alternate IRC [Apple addition]
- 6679 - Alternate IRC SSL [Apple addition]
- 6697 - IRC+SSL [Apple addition]
- 10080 - amanda

### Samples
In this sample `KestrelPortGenerated` is a symbol that return the number of an available port or 5000.

```json
  "KestrelPortGenerated": {
    "type": "generated",
    "generator": "port"
    "parameters": {
      "fallback":"5000"
    }
  },
```

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/GeneratePortNumberMacro.cs)


## Guid

*Note:* [Guids section in `template.json`](Reference-for-template.json.md#guids) can be used to achieve same goals with easier configuration

Creates a formatted guid for a replacement. To configure the output format of the macro you can use the **defaultFormat** parameter that accepts a single value from **{'n', 'd', 'b', 'p', 'x'}** for lowercase output or **{'N', 'D', 'B', 'P', 'X'}** for uppercase output. The formats are defined in [`Guid.ToString()`  method documentation](https://msdn.microsoft.com/en-us/library/97af8hh4(v=vs.110).aspx)
#### Parameters
|Name|Data Type|Description|Mandatory|
|----------|------|---------------|---|
|`defaultFormat`|`string`|format descriptor|no, default: `D`|

### Samples
This sample creates different symbols showing the different formatting available for the generated guid.

```json
"symbols":{
  "id01":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid01",
    "parameters": {
      "defaultFormat":"N"
    }
  },
  "id02":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid02",
    "parameters": {
      "defaultFormat":"D"
    }
  },
  "id03":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid03",
    "parameters": {
      "defaultFormat":"B"
    }
  },
  "id04":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid04",
    "parameters": {
      "defaultFormat":"P"
    }
  },
  "id05":{
    "type": "generated",
    "generator": "guid",
    "replaces": "myid05",
    "parameters": {
      "defaultFormat":"X"
    }
  }
}
```

### Related 
[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/GuidMacro.cs)
[`Guid Format Documentation`](https://msdn.microsoft.com/en-us/library/97af8hh4(v=vs.110).aspx)
[Guids section in `template.json`](Reference-for-template.json.md#guids)


## Now

Creates a symbol from the current date/time. 

#### Parameters 
|Name|Data Type|Description|Mandatory|
|----------|------|---------------|---|
|`format`|`string`|[`DateTime.ToString()`](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings) format|no|
|`utc`|`bool`|UTC time if `true`, local time otherwise|no|

### Samples
In this sample a symbol is created showing the current data, and replacing any instance of "01/01/1999"    

```json
"symbols":{
  "createdDate": {
    "type": "generated",
    "generator": "now",
    "parameters": {
    "format": "MM/dd/yyyy"
    },
    "replaces":"01/01/1999"
  }
}
```

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/NowMacro.cs)   
[`DateTime.ToString documentation`](https://msdn.microsoft.com/en-us/library/zdtaw1bw(v=vs.110).aspx)     
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/10-symbol-from-date)   

## Random

Creates a random integer value in a specified range. 

#### Parameters
|Name|Data Type|Description|Mandatory|
|----------|------|---------------|---|
|`low`|`integer`|lower inclusive bound|yes|
|`high`|`integer`|upper exclusive bound|no, default:`int.MaxValue`|

### Samples   
This sample shows a symbol that generates a value from `0` to `10000` excluded, and replace any instance of `4321`

```json
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

### Related 
[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/RandomMacro.cs)    
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/12-random-number)


## Regex
Defines a list of data manipulation steps based on regex expressions.     

#### Parameters
|Name|Data Type|Description|Mandatory|
|----------|------|---------------|---|
|`source`|`string`|the symbol to transform|yes|
|`steps`|`array`|replacement steps|yes|

`steps` element definition:     
|Name|Data Type|Description|Mandatory| 
|----------|------|---------------|---|
|`regex`|`string`, regex pattern|selection pattern|yes|
|`replacement`|`string`|the replacement value for matched pattern|yes|

### Samples

```json
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

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/RegexMacro.cs)
[`RegEx.Replace Documentation`](https://msdn.microsoft.com/en-us/library/xwewhkd1(v=vs.110).aspx)     

## RegexMatch
Tries to match regex pattern against value of source symbol and returns `true` if matched, otherwise `false`.

#### Parameters
|Name|Data Type|Description|Mandatory|
|----------|------|---------------|---|
|`source`|`string`|the symbol to attempt to match value|yes|
|`pattern`|`string`, regex pattern|the regex match pattern|yes|

### Samples

```json
"symbols": {
  "isMatch": {
    "type": "generated",
    "generator": "regexMatch",
    "dataType": "bool",
    "replaces": "test.value1",
    "parameters": {
    "source": "name",
    "pattern": "^hello$"
    }
  }
}
```

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/RegexMatchMacro.cs)
[`Regex.IsMatch Documentation`](https://docs.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.ismatch)     

## Switch

Defines a set of conditions to be evaluated, and the value to return if the condition is met. The first condition to evaluate to true is used. To include a default case, add a condition that always evaluates to true as the last entry in `cases`.

#### Parameters
|Name|Data Type|Description|Mandatory|
|----------|------|---------------|---|    
|`cases`|`array`|choices to evaluate|yes|
|`evaluator`|`enum`: `C++2`, `C++`, `MSBuild`, `VB`|expression evaluation engine|no, default: `C++2`|

`cases` definition
|Name|Data Type|Description|Mandatory| 
|----------|------|---------------|---|    
|`condition`|`string`|the condition to evaluate, keep empty for default clause.|no|
|`value`|`string`|the value to return, if `condition` evaluates to `true`|yes|

### Samples

This sample shows how to change the replacement value based on evaluating conditions using other symbols:

```json
"symbols": {
  "test": {
    "type": "parameter",
    "datatype": "string"
  },
  "example": {
    "type": "generated",
    "generator": "switch",
    "replaces": "abc",
    "parameters": {
      "evaluator": "C++",
      "datatype": "string",
      "cases": [
        {
          "condition": "(test == '123')",
          "value": "456"
        },
        {
          "condition": "(test == '789')",
          "value": "012"
        }
      ]
    }
  }
}
```
In this case, if the user enters the value `123` as the value of the parameter `test`, `abc` in the content will be replaced with `456`, if the user enters `789`, `abc` is replaced with `012` instead.

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/SwitchMacro.cs)

## Join

Concatenates multiple symbols or constants with the defined separator into a new symbol.

#### Parameters
|Name|Data Type|Description|Mandatory|   
|----------|---------|----------|-----|    
|`symbols`   |`array`    |defines the values to concatenate|yes|
|`separator` |`string`   |the value used as the separator between the values to be concatenated, notice that you can use `/` as folder separator also on Windows, since File API will convert it into `\` | no |
|`removeEmptyValues` |`bool`|indicates whether the empty values should be skipped or honored. By default this switch is off - leading to multiple consecutive separators in output string in case that same input values are null or empty| no |

`symbols` definition
|Name|Data Type|Description|Mandatory|   
|------|---------|---------------|---|
|`type`  |`enum`: `ref`, `const` |`ref` indicates that the value is referenced from another symbol </br> `const` - the value is a string constant|no, default: `const`|
|`value` |`string`   |either a name of another symbol or string constant|yes, should be not empty or whitespace when `type` is `ref`|

### Samples

This sample shows how to change the replacement value based on evaluating conditions using other symbols:

```json
"symbols": {
  "company": {
    "type": "parameter",
    "dataType": "string",
    "defaultValue": "Microsoft"
  },
  "product": {
    "type": "parameter",
    "dataType": "string",
    "defaultValue": "Visual Studio"
  },
  "joinedRename": {
    "type": "generated",
    "generator": "join",
    "fileRename": "Api",
    "parameters": {
      "symbols": [
        {
          "type": "const",
          "value": "Source"
        },
        {
          "type": "const",
          "value": "Api"
        },
        {
          "type": "ref",
          "value": "company"
        },
        {
          "type": "ref",
          "value": "product"
        }
      ],
      "separator": "/",
      "removeEmptyValues": true
    }
  }
}
```
This sample will rename folder called `Api` into `Source/Api/Microsoft/Visual Studio`. Notice that File API will automatically change `/` into `\` on Windows.

<a id="multichoice-join-sample"></a>Joining [multi-choice symbol](Reference-for-template.json.md#multichoice-symbols-specifics) values:

`template.json`:
```json
"symbols": {
  "Platform": {
    "type": "parameter",
    "description": "The target framework for the project.",
    "datatype": "choice",
    "allowMultipleValues": true,
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
  },
  "joinedRename": {
    "type": "generated",
    "generator": "join",
    "replaces": "SupportedPlatforms",
    "parameters": {
      "symbols": [
        {
          "type": "ref",
          "value": "Platform"
        }
      ],
      "separator": ", ",
      "removeEmptyValues": true,
    }
  }
}
```

`Program.cs`:
```C#
// This file is generated for platfrom: SupportedPlatforms
```

This sample will expand and join values of `Platform` argument and replace `SupportedPlatforms` string with `MacOS, iOS`:

`Program.cs`:
```C#
// This file is generated for platfrom: MacOS, iOS
```

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/JoinMacro.cs)
