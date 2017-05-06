The sample in this folder demonstrates:

 - **Run `dotnet restore` after create**

Using a *post action* you can run `dotnet restore` after the project is created.
In this sample a parameter is configured so the user can skip the restore step if desired.

See 

 - [`template.json`](./MyProject.Con.CSharp/.template.config/template.json)
 - [`dotnetcli.host.json`](./MyProject.Con.CSharp/.template.config/dotnetcli.host.json)

Related

For more info on *post actions*, including the list of available ones, see https://github.com/dotnet/templating/wiki/Post-Action-Registry.