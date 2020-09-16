Inside the `template.jon` file, you can define custom symbols that will be used inside the template files. Some of these symbols define parameters that you can input from command line, others are result of logical expressions applied to symbols, and others are generated using **Macros**.     
To use a Macro inside your `template.json` you have to add the `"type": "generated"` to the symbol definition, and use the `"generator": ...` parameter to select the macro to use.    
This is a sample of definition of a symbol with a macro, the `port` Macro, that generates a random number for an http port.    

```
"IISExpressPortGenerated": {    
  "type": "generated",    
  "generator": "port"   
},   
```

Most of the macros need to be configured via parameters that let you select the source of the data and select among the options available. Below is a sample of a symbol that use the `now` Macro to replace a fixed year indication present in the source files with the current year.

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

There are several built in generators for computing parameter values. This table summarizes those.

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

This macro converts a value lowercase or uppercase according to the configuration.     

#### Parameters
| Name     |Data Type| Description   |
|----------|------|---------------|
|source|string|Source of the data| 
|toLower|bool|Source value is lowercased if true, uppercase otherwise|

### Samples

In this sample four symbols are defined:     
 - `createddate` - has a constant value of 5001, and is applied to the template files, replacing every occurrance of "1234" with "5001".
 - `ownername` - is a parameter which can be set on the command line using `dotnet new` It has a default value of "John Doe", that will be used if the no value is received from the host. The value will be used to replace "John Smith (a)".
 - `nameUpper` and `nameLower` are due symbols that generate the uppprCase and lowerCase version of `ownerName` that are used to replace any instance of "John Smith (U)" and "John Smith (l)". 

```
"symbols":{
  "createddate": {
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

### Related

[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/CaseChangeMacro.cs)     
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/11-change-string-casing)

## Coalesce

Behaves like the C# `??` operator.

#### Parameters

| Name     |Data Type| Description   |
|----------|------|---------------|
|source|string|Source of the data| 
|sourceVariableName|string|source of the data|
|defaultValue|string|fixed value if previous was not defined|
|fallbackVariableName|string|alternate source of data|

### Samples

In this sample three symbols are defined:
 - `MessageYear` - is a parameter set by the user when calling `dotnet new`).   
 - `ThisYear` - use the now generator to calculate the cuyrrent year.
 - `YearReplacer` - ensures that any occurrance of "1234" is replaced. If `MessageYear` was passed in by the user that value will
 be used. Otherwise `ThisYear` will be used.
 
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

[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/CoalesceMacro.cs)

## Constant

#### Parameters

| Name     |Data Type| Description   |
|----------|------|---------------|
|value|string|constant value| 

Enable using a constant value.

### Samples

`myconstant` is a symbol that replaces "1234" with "5001"

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

### Related

[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/ConstantMacro.cs)    
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/13-constant-value)

## Evaluate

There is another type of symbols, the **computed** type symbols: they enable you to define expressions that will be evaluated during the processing of the templating, evaluting parameters and other symbols.

#### Parameters

| Name     |Data Type| Description   |
|----------|------|---------------|
|value|string|expression to be evaluated| 

### Samples

In this sample IndividualAuth is true if the value of **auth**, another symbol defined in the template, is "IndividualB2C"

    "IndividualAuth": {
      "type": "computed",
      "value": "(auth == \"IndividualB2C\")"
    },

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/EvaluateMacro.cs)


## Port

Enables you to get the port number for an available port on the machine.   
During evaluation looks for a valid free port number trying to create a socket, and in case of problems, returns the value defined in the **fallback** parameter.

#### Parameters

| Name     |Data Type| Description   |
|----------|------|---------------|
|fallback|string|fallaback value| 

### Samples
In this sample KestrelPortGenerated is a symbol that return the number of an available port or 5000.

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
[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/GeneratePortNumberMacro.cs)


## Guid

Enables you to create a formatted guid for a replacement. To configure the output format of the macro you can use the **format** parameter that accepts a value from **ndbpx** for lowercase output and **NDPBX** for uppercase output. 

#### Parameters
| Name     |Data Type| Description   |
|----------|------|---------------|
|format|string|Format Descriptor| 

### Samples
This sample created different symbols showing the different formatting available for the generated guid.

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

### Related 
[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/GuidMacro.cs)     
[`Guid Format Documentation`](https://msdn.microsoft.com/en-us/library/97af8hh4(v=vs.110).aspx)    
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/14-guid)


## Now

Enables you to create a symbol from the current date/time. 

#### Parameters 
| Name     |Data Type| Description   |
|----------|------|---------------|
|format|string|DateTime.ToString format|
|utc|bool|utc time if true, local time otherwise|    

### Samples
In this sample a symbol is created showing the current data, and replacing any instance of "01/01/1999"    

```
"symbols":{
  "createddate": {
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
[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/NowMacro.cs)    
[`DateTime.ToString documentation`](https://msdn.microsoft.com/en-us/library/zdtaw1bw(v=vs.110).aspx)     
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/10-symbol-from-date)   

## Random

Creates a random int in a specified range. 

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
[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/RandomMacro.cs)    
[`Sample`](https://github.com/dotnet/dotnet-template-samples/tree/master/12-random-number)

## Regex

Enables you to define a list of data manipulation steps according to regex expressions.     

#### Parameters
| Name     |Data Type| Description   |
|----------|------|---------------|
|source|string|data source|
|steps|array|replacement steps|

parameters of the replacement step     

| Name     |Data Type| Description   |    
|----------|------|---------------|    
|regex|string|selection pattern|    
|replacement|string|replacement formula|

### Samples

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/RegexMacro.cs)     
[`RegEx.Replace Documentation`](https://msdn.microsoft.com/en-us/library/xwewhkd1(v=vs.110).aspx)     

## Switch

Enables you to define a set of conditions to be evaluted, and the value to return if this conditions is match. The first condition to evaluate to true is the one that is used. To include a default case, add a condition that always evaluates to true as the last entry in `cases`.

#### Parameters
| Name     |Data Type| Description   |    
|----------|------|---------------|    
|cases|array|choices to evaluate|
|datatype|string| optional datatype, if not boolean|
|evaluator|string|expression evaluation engine, if not C++|

Parameters of a single case

| Name     |Data Type| Description   |    
|----------|------|---------------|    
|condition|string|condition to evaluate|
|value|string|value to return if match|

### Samples

This sample shows how to change what a value gets replaced with depending on the evaluation of conditions against user input

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

In this case, if the user supplies the value 123 as the value of the parameter `test`, `abc` in the content will be replaced with `456`, if the user supplies `789`, `abc` is replaced with `012` instead

### Related
[`Implementation class`](https://github.com/dotnet/templating/blob/rel/vs2017/3-Preview2/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/SwitchMacro.cs)
