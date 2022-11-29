The sample in this folder demonstrates:

 - **Creating a template that supports multiple languages** - Using `groupIdentity` to create a console project in `C#` and `F#` 

In this sample there are two project templates, one using C# and the other F#.
By using `groupIdentiy` these templates will appear as the same template in the output of `dotnet new --list`.

To make more than one template appear as the same in `dotnet new --list` ensure the following:

 - Each has a unique value for `identity`
 - Each has the **same** value for `groupIdentity`
 - Each has the **same** value for `name`
 - Each has a `language` tag specifying the language supported

See 
 - [`template.json` (c#)](./MyProject.Con.CSharp/.template.config/template.json)
 - [`template.json` (f#)](./MyProject.Con.FSharp/.template.config/template.json)
