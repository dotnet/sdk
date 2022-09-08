# How to create a new value form

Available value forms are described [here](../Runnable-Project-Templates---Value-Forms.md).
Value form represent the form of the parameter, usually related to specific casing.

We appreciate creating new value forms by the community. 

Value forms follow the following syntax:
```json
{
   "forms":
   {
        "nameOfTheForm":
        {
            "identifier": "name-unique", //type of value form; should be unique value
            "param1": "value1", //key-value parameters for the form. The value may be JSON array or object, if more complicated configuration is needed.
            "param2": "value2"
        }
   }
}
```

To create new value form, follow the following guideline:

1. Implement [`Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms.IValueFormFactory`](../../src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/ValueForms/IValueFormFactory.cs) interface.

The existing implementation of value form are located in [`ValueForms`](../../src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/ValueForms) folder of `Microsoft.TemplateEngine.Orchestrator.RunnableProjects` projects.

The implementation should have:
- `Identifier` property - unique identifier of the form
- `Type` property - unique `string` name, matching generated symbol type
- `Create(string? name = null)` method - creates the form for default configuration
- `FromJObject(string name, JObject? configuration = null)` method - creates the form for json configuration

The `IValueForm` has `Identifier` property that matches factory `Identifier`, `Name` corresponding the name of the form in JSON and `Process` method that create the form for the passed `value`.

You may want to derive your own implementation from one of the base classes:
- `BaseValueFormFactory` - basic features
- `ActionableValueFormFactory`- the form that just does the action without configuration
- `ConfigurableValueFormFactory`- the form that performs the action and based on configuration
- `DependantValueFormFactory` - the form that needs other forms for processing

The very basic implementation may be:
```CSharp
    internal class HelloValueForm : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "hello";

        public HelloValueForm() : base(FormIdentifier)
        {
        }

        protected override string Process(string value) => $"Hello {value}!";
    }
```

2. Once the value form is implemented, add it to [value form collection](../../src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/ValueFormRegistry.cs).

3. [optional] Update [JSON schema](../../src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Schemas/JSON/template.json) with new value form syntax.
If you do so, also add [a new test case](../../test/Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests/SchemaTests/) for testing the syntax.

4. Add unit tests for new implementation. Macro related unit tests are located in [this folder](../../test/Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests/ValueFormTests/).
For more complete scenario, consider adding the full template generation tests to [`RunnableProjectGeneratorTests.cs`](../../test/Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests/RunnableProjectGeneratorTests.cs).

5. Update documentation in [docs folder](../Runnable-Project-Templates---Value-Forms.md).