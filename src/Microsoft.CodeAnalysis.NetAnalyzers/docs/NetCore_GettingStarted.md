# Getting started with .NetCore/.NetStandard Analyzers

1. Read through the [.NET Compiler Platform SDK](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) for understanding the different Roslyn elements `(Syntax Nodes, Tokens, Trivia)`. The factory methods and APIs are super useful.  
2. Learning this [tutorial](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix) for custom analyzer and trying it some level is very useful to get started. It is pretty easy step by step tutorial, it is time saving as it has a template generated for us (with analyzer, fixer and unit test), has good explanation, would give you pretty good understanding on how Roslyn analyzers work. 
3. Clone the `dotnet/roslyn-analyzers` repo, install all required dependencies and build the repo by the [instructions](https://github.com/dotnet/roslyn-analyzers#getting-started). 
4. Follow the coding style of the `dotnet/roslyn-analyzers` repo. [Guidelines about new rule id and doc](https://github.com/dotnet/roslyn-analyzers/blob/master/GuidelinesForNewRules.md). 
5. Open `RoslynAnalyzers.sln` and open the package where you are creating your analyzer. In our case, it is mostly `Microsoft.CodeAnalysis.NetAnalyzers`->`Microsoft.NetCore.Analyzers`. Create your analyzer and/or fixer class in the corresponding folder.  
6. Add a message, title and description for your analyzer into `MicrosoftNetCoreAnalyzersResources.resx` and build the repo before using them, the language specific resources will be generated. 
7. Make sure you have done everything from the [Definition of done list](#definition-of-done) below. 

## Definition of done 

- Analyzer implemented to work for C# and VB. 
	- Unit tests for C#:
		- All scenarios covered. 
		- Prefer markup syntax for the majority of tests. 
		- If your analyzer has placeholders in the diagnostic message and you want to test the arguments, write a smaller number of tests using the `VerifyCS.Diagnostic` syntax to construct specific diagnostic forms.
	- Unit tests for VB:
		- Obvious positive and negative scenarios covered. 
		- If the implementation uses any syntax-specific code, then all scenarios must be covered. 
- Fixer implemented for C#, using the language-agnostic APIs if possible. 
	- If the fixer can be entirely implemented with language-agnostic APIs `(IOperation)`, then VB support is essentially free. 
	- With a language-agnostic fixer, apply the attribute to indicate the fixer also applies to VB and add mainline VB tests. 
	- If language-specific APIs are needed to implement the fixer, the VB fixer is not required.
	- Do not separate analyzer tests from code fix tests. If the analyzer has a code fix, then write all your tests as code fix tests.
		- Calling `VerifyCodeFixAsync(source, source)` verifies that the analyzer either does not produce diagnostics, or produces diagnostics where no code fix is offered.
		- Calling `VerifyCodeFixAsync(source, fixedSource)` verifies the diagnostics (analyzer testing) and verifies that the code fix on source produces the expected output.
- Run the analyzer locally against `dotnet/runtime` [instructions](#Testing-against-the-Runtime-repo). 
	- Use the failures to discover nuance and guide the implementation details. 
	- Run the analyzer against `dotnet/roslyn` [instruction](#Testing-against-the-Roslyn-repo), and with `dotnet/aspnetcore` if feasable. 
	- Review each of the failures in those repositories and determine the course of action for each. 
- Document for review: severity, default, categorization, numbering, titles, messages, and descriptions.
- Document for review: matching and non-matching scenarios, including any discovered nuance. 
- Create the appropriate documentation for [docs.microsoft.com](https://github.com/MicrosoftDocs/visualstudio-docs-pr/tree/master/docs/code-quality) within **ONE WEEK** [instructions on OneNote](https://microsoft.sharepoint.com/teams/netfx/corefx/_layouts/15/Doc.aspx?sourcedoc={0cfbc196-0645-4781-84c6-5dffabd76bee}&action=edit&wd=target%28Engineering.one%7Cab467035-bb64-4353-b933-97f5877d508b%2FAdding%20documentation%20for%20new%20CA%20rules%7C9e44fc32-5cd8-4f7f-bbf8-3600653ca9b9%2F%29&wdorigin=703). External contributors should create an issue at https://github.com/microsoftDocs/visualstudio-docs/issues with a subject `Add documentation for analyzer rule [Your Rule ID]`. 
- PR merged into `dotnet/roslyn-analyzers`. 
- Failures in `dotnet/runtime` addressed. 

## Testing against the Runtime repo 

1. Navigate to the root of the Roslyn-analyzers repo and run these commands: 
	- `cd roslyn-analyzers` 
	- `set RUNTIMEPACKAGEVERSION=3.0.0` 
	- `build.cmd -ci /p:AssemblyVersion=%RUNTIMEPACKAGEVERSION% /p:OfficialBuild=true`
	- `cd artifacts\bin\Microsoft.NetCore.CSharp.Analyzers\Debug\netstandard2.0` 
2. Copy the two DLLs and replace the NuGet cache entries used by `dotnet/runtime`. They might be in `"runtime/.packages/..."` or `"%USERPROFILE%/.nuget/packages/... "`. You can check the exact path by building something in runtime with /bl and checking the binlog file. Example: 
	- `copy /y *.dll %USERPROFILE%\.nuget\packages\Microsoft.NetCore.Analyzers\%RUNTIMEPACKAGEVERSION%\analyzers\dotnet\cs` 
3.    Switch to the runtime project. 
4.    Introduce an error somewhere to prove that the rule ran. 
	- Be careful about in which project you are producing an error, choose an API not having reference from other APIs, else all dependent API's will fail. 
5. Build the runtime repo, either do a complete build or build each repo separately (coreclr, libraries, mono). 

## Testing against the Roslyn repo 

1. Clone `dotnet/roslyn` and build it with this command: 
	- `Build.cmd -restore -Configuration Release`
2. Build `dotnet/roslyn-analyzers` in debug mode:
	- `Build.cmd -Configuration Debug`
3. Run AnalyzerRunner from the Roslyn root directory to get the diagnostics. 
	- `.\artifacts\bin\AnalyzerRunner\Release\netcoreapp3.1\AnalyzerRunner.exe ..\roslyn-analyzers\artifacts\bin\Microsoft.NetCore.Analyzers.Package\Debug\netstandard2.0 .\Roslyn.sln /stats /concurrent /a AnalyzerNameToTest /log Output.txt` 
	- Do not forget change value after `/a` option with your testing analyzer name.
The diagnostics reported by the analyzer will be listed in Output.txt. 
