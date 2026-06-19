# How to create new generated symbol type

Available generated symbol types are described [here](../Available-Symbols-Generators.md).
Generated symbols are processed by a corresponding macro component that can generate new variables before the template is instantiated. New variables can be created from other variables (usually parameters) or independently.

We appreciate creating new types of generated symbols and macros by the community. 

Generated symbols follow the following syntax:
```json
{
   "symbols":
   {
        "symbolName":
        {
            "type": "generated",
            "generator": "your-type", //type of generated symbol; should be same as type of the macro implementing it
            "dataType": "string", //data type of the value that symbol generates: "string", "choice", "bool", "float", "int", "hex", "text"
            "parameters":
            {
                "name": "value" //key-value parameters for the symbol. The value may be JSON array or object, if more complicated configuration is needed.
            },
            "replaces": "to-be-replaced", // the text to replace with the value of this symbol in template content
            "fileRename": "to-be-replaced", // defines the portion of file name which will be replaced by symbol value
        }

   }
}
```

To create new generated symbol type, follow the following guideline

1. Implement [`Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions.IGeneratedSymbolMacro<T>`](../../src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Abstractions/IGeneratedSymbolMacro.cs) interface.

Any macro implementation is a component ([`IIdentifiedComponent`](../../src/Microsoft.TemplateEngine.Abstractions/IIdentifiedComponent.cs)) and will be loaded by template engine.
The existing implementation macro components are located in [`Macros`](../../src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros) folder of `Microsoft.TemplateEngine.Orchestrator.RunnableProjects` projects.

The implementation should have:
- `Id` property - unique GUID for component 
- `Type` property - unique `string` name, matching generated symbol type
- `Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, T config)` method - evaluates the variables based on `config` specified
- `Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IGeneratedSymbolConfig generatedSymbolConfig)` method - evaluates the variables based on `generatedSymbolConfig` specified
- `CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig)` method - creates macro-specific configuration from `generatedSymbolConfig`, that later on can be passed to `Evaluate` method.

The implementation of macro config may have:
- Properties/methods derived from `IMacroConfigDependency` interface:
    `ResolveSymbolDependencies(IReadOnlyList<string> symbols)` - the method should identify the dependencies of macro configuration on symbols, and populate `Dependencies` property of the macro with dependent symbol names.

You may want to derive your own implementation from `BaseGeneratedSymbolMacro<T>` base class that offers some common logic. Configuration of the macro may derive from `BaseMacroConfig` class, that already have some utilities to parse the JSON configuration.
When using base class, you need to implement the following members:
- `Id` and `Type` properties assigned to constant unique values
- `Evaluate` method: the method should create new variables to `IVariableCollection variableCollection` using parsed `config` and existing variables. It is recommended to log errors and debug information using logger available from `environmentSettings`. If you need to access the file system, do so via `environmentSettings.Host.FileSystem` abstraction.
- `CreateConfig` method - creates macro specific config from `IGeneratedSymbolConfig` config.

The very basic implementation may be:
```CSharp
    internal class HelloMacro : BaseGeneratedSymbolMacro<HelloMacroConfig>
    {
        public override string Type => "hello";

        public override Guid Id { get; } = new Guid("342BC62F-8FED-4E5A-AB59-F9AB98030155");

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, HelloMacroConfig config)
        {
            string greetings = $"Hello {config.NameToGreet}!";
            //set configured variable to Hello Name!
            variableCollection[config.VariableName] = greetings;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(HelloMacro), config.VariableName, greetings);
        }

        public override HelloMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig) => new(this, generatedSymbolConfig);
    }

    internal class HelloMacroConfig : BaseMacroConfig<HelloMacro, HelloMacroConfig>, IMacroConfigDependency
    {
        internal HelloMacroConfig(HelloMacro macro, string variableName, string nameToGreet, string sourceVariable, string? dataType = null) : base(macro, variableName, dataType)
        {
            if (string.IsNullOrEmpty(nameToGreet))
            {
                throw new ArgumentException($"'{nameof(nameToGreet)}' cannot be null or empty.", nameof(nameToGreet));
            }

            if (string.IsNullOrWhiteSpace(sourceVariable))
            {
                throw new ArgumentException($"'{nameof(sourceVariable)}' cannot be null or whitespace.", nameof(sourceVariable));
            }

            NameToGreet = nameToGreet;
            Source = sourceVariable;
        }

        internal HelloMacroConfig(HelloMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
            : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            NameToGreet = GetMandatoryParameterValue(generatedSymbolConfig, nameof(NameToGreet));
            Source = GetMandatoryParameterValue(generatedSymbolConfig, "source");
        }

        internal string NameToGreet { get; }

        internal string Source { get; private set; }

        public void ResolveSymbolDependencies(IReadOnlyList<string> symbols)
        {
            PopulateMacroConfigDependencies(Source, symbols);
        }
    }
```

`IMacroConfigDependency` interface depicts macro capability to derive it value(s) from other macros.
Dependencies are defined based on passed `IReadOnlyList<string> symbols` collection. An example of implemetation can be find here: [`CoalesceMacroConfig.cs`](../../src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Macros/CoalesceMacroConfig.cs).

`IGeneratedSymbolConfig` config already contains the pre-parsed JSON from template.json. It has properties for: symbol name, data type (if specified) and parameters collection. 
Parameters collection contains parameter key-value pairs from JSON. Note that value is in JSON format, i.e. if the parameter value is string, then it contains `"\"string-value\""`.
It is recommend to get `JToken` using `JToken.Parse` on this value when parsing the value or use helper methods available in `BaseMacroConfig` that can parse the data.

2. Once the macro is implemented, add it to [components collection](../../src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Components.cs).

3. [optional] Update [JSON schema](../../src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Schemas/JSON/template.json) with new generated symbol syntax.
If you do so, also add [a new test case](../../test/Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests/SchemaTests/GeneratorTest.json) for testing the syntax.

4. Add unit tests for new implementation. Macro related unit tests are located in [this folder](../../test/Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests/MacroTests/).
For more complete scenario, consider adding the full template generation tests to [`RunnableProjectGeneratorTests.cs`](../../test/Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests/RunnableProjectGeneratorTests.cs).

5. Update documentation in [docs folder](../Available-Symbols-Generators.md).

