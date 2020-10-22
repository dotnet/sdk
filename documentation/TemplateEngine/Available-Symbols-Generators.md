Inside the `template.json` file, you can define custom symbols that will be used inside the template files. 
The supported symbol types are:
- Parameter - the value is typically provided by the user when creating the template. If not provided, the value is taken from host configuration, otherwise default value is used. 
- Derived - defines transformation of another symbol.  The value of this symbol is derived from the value of another symbol by applying the defined form.
- Computed - the boolean value is evaluated during the processing of the template based on the other symbol values.
- Generated - the value is computed by a built-in symbol value generator.
This article covers available generators for generated symbols.

To use a generated symbol inside your `template.json` file:
1. Add `"type": "generated"` to the symbol definition
1. Use the `"generator": ...` parameter to select the generator to use.    
This is a sample of definition of a generated symbol, the `port` generator, that generates a random number for an http port.    

```
"IISExpressPortGenerated": {    
  "type": "generated",    
  "generator": "port",
  "parameters": {
    "fallback":"5000"
  }  
},   
```

Most of the generators need to be configured via parameters that let you select the source of the data and select among the options available. Below is a sample of a symbol that use the `now` generator to replace a fixed year indication present in the source files with the current year.

```
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
| [evaluate](#evaluate) | Evaluate a code expression (using C style syntax) |
| [port](#port) | Generate a port number that can be used by web projects. |
| [guid](#guid) | Create a new guid. |
| [now](#now) | Get the current date/time. |
| [random](#random) | Generate random int. |
| [regex](#regex) | Process a regular expression. |
| [switch](#switch) | Behaves like a C# `switch` statement. |

## Casing
Changes the case of the text of the source value to all upper-case or all lower-case.  It does not affect spaces (i.e. does not do any sort of Camel Casing).

#### Parameters
| Name     |Data Type| Description   |
|----------|------|---------------|
|source|string|Source of the data| 
|toLower|bool| applies lower case if true, uppercase otherwise|

### Samples

In this sample three symbols are defined:     
 - `ownerName` is a parameter which can be set on the command line using `dotnet new` It has a default value of "John Doe", that will be used if the no value is received from the host. The value will be used to replace "John Smith (a)".
 - `nameUpper` and `nameLower` are the symbols that generate the upperCase and lowerCase version of `ownerName` that are used to replace any instance of "John Smith (U)" and "John Smith (l)". 

```
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

[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/CaseChangeMacro.cs)
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/11-change-string-casing)


## Coalesce
Behaves like the C# `??` operator.

#### Parameters

| Name     |Data Type| Description   |
|----------|------|---------------|
|sourceVariableName|string|source of the data|
|fallbackVariableName|string|alternate source of data|

### Samples

In this sample three symbols are defined:
 - `MessageYear` - is a parameter set by the user when calling `dotnet new`.   
 - `ThisYear` - use the now generator to calculate the current year.
 - `YearReplacer` - ensures that any occurrence of "1234" is replaced. If `MessageYear` was passed in by the user that value will be used. Otherwise `ThisYear` will be used.

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

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/CoalesceMacro.cs)

## Constant

Uses constant value.

#### Parameters

| Name     |Data Type| Description   |
|----------|------|---------------|
|value|string|constant value| 


### Samples

`myConstant` is a symbol that replaces "1234" with "5001"

```
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

[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/ConstantMacro.cs)
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/13-constant-value)


## Evaluate

Defines expression that will be evaluated during the processing of the template based on parameters and other symbols.
The `computed` type symbols can be use for same purpose.

#### Parameters

| Name     |Data Type| Description   |
|----------|------|---------------|
|value|string|expression to be evaluated| 

### Samples

In this sample `IndividualAuth` is `true` if the value of `auth`, another symbol defined in the template, is `IndividualB2C`
```
    "IndividualAuth": {
      "type": "generated",
	  "generator": "evaluate",
	  "parameters": {
		  "action": "(auth == \"IndividualB2C\")"
		},
    },
```
### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/EvaluateMacro.cs)


## Port
Gets an available port number on the machine.   
During evaluation looks for a valid free port number trying to create a socket, and in case of problems, returns the value defined in the `fallback` parameter.

#### Parameters

| Name     |Data Type| Description   |
|----------|------|---------------|
|fallback|string|fallback value| 

### Samples
In this sample `KestrelPortGenerated` is a symbol that return the number of an available port or 5000.

```
"KestrelPortGenerated": {
  "type": "generated",
  "generator": "port"
  "parameters": {
    "fallback":"5000"
  }
},
```

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/GeneratePortNumberMacro.cs)


## Guid

Creates a formatted guid for a replacement. To configure the output format of the macro you can use the **format** parameter that accepts a value from **ndbpx** for lowercase output and **NDPBX** for uppercase output. 

#### Parameters
| Name     |Data Type| Description   |
|----------|------|---------------|
|format|string|Format Descriptor| 

### Samples
This sample creates different symbols showing the different formatting available for the generated guid.

```
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

### Related 
[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/GuidMacro.cs)
[`Guid Format Documentation`](https://msdn.microsoft.com/en-us/library/97af8hh4(v=vs.110).aspx)    
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/14-guid)


## Now

Creates a symbol from the current date/time. 

#### Parameters 
| Name     |Data Type| Description   |
|----------|------|---------------|
|format|string|DateTime.ToString format|
|utc|bool|UTC time if true, local time otherwise|    

### Samples
In this sample a symbol is created showing the current data, and replacing any instance of "01/01/1999"    

```
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
[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/NowMacro.cs)   
[`DateTime.ToString documentation`](https://msdn.microsoft.com/en-us/library/zdtaw1bw(v=vs.110).aspx)     
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/10-symbol-from-date)   

## Random

Creates a random integer value in a specified range. 

#### Parameters
| Name     |Data Type| Description   |
|----------|------|---------------|
|low|integer|lower bound|
|high|integer|upper bound|   

### Samples   
This sample shows a symbol that generates a value from `0` to `10000` excluded, and replace any instance of `4321`

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

### Related 
[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/RandomMacro.cs)    
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/12-random-number)


## Regex
Defines a list of data manipulation steps based on regex expressions.     

#### Parameters
| Name     |Data Type| Description   |
|----------|------|---------------|
|source|string|data source|
|steps|array|replacement steps|

Replacement steps     

| Name     |Data Type| Description   |    
|----------|------|---------------|    
|regex|string|selection pattern|    
|replacement|string|replacement formula|

### Samples

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

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/RegexMacro.cs)
[`RegEx.Replace Documentation`](https://msdn.microsoft.com/en-us/library/xwewhkd1(v=vs.110).aspx)     

## Switch

Defines a set of conditions to be evaluated, and the value to return if the condition is met. The first condition to evaluate to true is used. To include a default case, add a condition that always evaluates to true as the last entry in `cases`.

#### Parameters
| Name     |Data Type| Description   |    
|----------|------|---------------|    
|cases|array|choices to evaluate|
|evaluator|string|expression evaluation engine, if not C++|

Cases definition

| Name     |Data Type| Description   |    
|----------|------|---------------|    
|condition|string|condition to evaluate|
|value|string|value to return if match|

### Samples

This sample shows how to change the replacement value based on evaluating conditions using other symbols:

```
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
[`Implementation class`](https://github.com/dotnet/templating/blob/master/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/SwitchMacro.cs)
