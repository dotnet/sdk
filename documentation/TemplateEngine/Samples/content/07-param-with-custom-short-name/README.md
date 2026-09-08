The sample in this folder demonstrates:

 - Customizing parameter names on the CLI 

To customize parameter names for the command line (`dotnet new`), you will create a new file named `dotnetcli.host.json`.
In that file you can define the long and short name of the parameters, as well as other settings.

See 
 - [`dotnetcli.host.json`](./MyProject.Con/.template.config/dotnetcli.host.json)
 - [`template.json`](./MyProject.Con/.template.config/template.json)

Note: This sample is the same as [`02-add-parameters`](https://github.com/dotnet/dotnet-template-samples/tree/master/02-add-parameters) plus a `dotnetcli.host.json` file.