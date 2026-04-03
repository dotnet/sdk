The sample in this folder demonstrates:

 - **How to create a computed symbol and use that for conditions**

In this sample we define a few parameter in the [`template.json`](./MyProject.Con/.template.config/template.json) file. There is also
a computed symbols `BackgroundGreyAndDisplayCopyright` which combines the value for two parameters. Then this symbol can be used
as a conditional in source files.

Try the sample with the following and compare the results.
 - `dotnet new sample15 -o Sam15 --DisplayCopywrite false`
 - `dotnet new sample15 -o Sam15 --DisplayCopywrite true --BackgroundColor aliceblue`

## See
  - [`template.json`](./MyProject.Con/.template.config/template.json)
  - [`Program.cs`](./MyProject.Con/Program.cs)

