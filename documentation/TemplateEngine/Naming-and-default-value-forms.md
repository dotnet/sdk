# `sourceName` default value forms

When selecting a `sourceName` for a template you're authoring, keep in mind the default value forms applied to this symbol: 
- `identity` - the value as entered by user.
- `namespace` - the value transformed in a way to be a correct .NET namespace.  [Details](https://github.com/dotnet/templating/blob/b0b1283f8c96be35f1b65d4b0c1ec0534d86fc2f/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/ValueForms/DefaultSafeNamespaceValueFormFactory.cs#L17-L59)
- `class name` - the value transformed in a way to be a correct .NET class name. [Details](https://github.com/dotnet/templating/blob/b0b1283f8c96be35f1b65d4b0c1ec0534d86fc2f/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/ValueForms/DefaultSafeNameValueFormFactory.cs#L15-L21)
- `lower case namespace` - same as `namespace`, but lower case.
- `lower case class name` - same as `class name`, but lower case.

A good choice for `sourceName` is the value that produces distinct values under the below transformations, for example `Template.1`: 
Form | Source | Transformed value
-------|------|--------
`identity` | `Template.1` | `Template.1`
`namespace` | `Template.1` | `Template._1`
`class name`  | `Template.1` | `Template__1`
`lower case namespace` | `Template.1` | `template._1`
`lower case class name`| `Template.1` | `template__1`

In this case, you can use the transformed value to force using the form, for example:
```csharp
namespace Template._1; //uses `namespace` form

public class Template__1  //uses `class` form
{
   var str = "My template name is Template.1"; //uses `identity` form
}
```

In this case, if user use `My-App` as the name, the generated content will be as follows:
Form | Source | Transformed value
-------|------|--------
`identity` | ` My-App` | `My-App`
`namespace` | ` My-App` | `My_App`
`class name`  | `My-App` | `My_App`
`lower case namespace` | `My-App` | `my_app`
`lower case class name`| `My-App` | `my_app`

```csharp
namespace My_App; //uses `namespace` form

public class My_App  //uses `class` form
{
   var str = "My template name is My-App"; //uses `identity` form
}
```

An example of wrong `sourceName` is `template1`, in this case all form transformations result in `template1`.
Referring to previous example:
```csharp
namespace template1; //intent to use `namespace` form

public class template1  //intent to use `class` form
{
   var str = "My template name is template1"; //intent to use `identity` form
}
```

In this case, if the user use `My-App` as the name, the generated content may be as follows:
```csharp
namespace My-App; //intent to use `namespace` form, but `identity` was used instead. It is not guaranteed which of `My-App`, `My_App`, `my_app` will be used here.

public class My-App //intent to use `class` form, but `identity` was used instead
{
   var str = "My template name is My_App"; //intent to use `identity` form, but `namespace` was used instead
}
```

As the result, this code won't compile as namespace and class are not using correct names.

For more details on value forms, refer to [the article](Value-Forms.md).