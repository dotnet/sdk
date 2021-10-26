To search for the templates available on NuGet.org, use [`dotnet new --search`](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-new-search).
```
    dotnet new --search web
    dotnet new --search azure --type project
    dotnet new --search azure --author Microsoft
```

To ensure that the template package appears in `dotnet new --search` result, set [the NuGet package type](https://docs.microsoft.com/en-us/nuget/create-packages/set-package-type) to `Template`.

Below is a list of selected templates which are available for use with `dotnet new`:

# C# Templates


| Name     | Quick Install |
|----------|:--------------|
| [.NET Boxed](https://github.com/Dotnet-Boxed/Templates) | `dotnet new --install "Boxed.Templates"`|
| [Auth0 Templates](https://github.com/auth0-community/auth0-dotnet-templates) | `dotnet new --install "Auth0.Templates"` |
| [AWS Lambda .NET Core Templates](https://github.com/aws/aws-lambda-dotnet/tree/master/Blueprints) | `dotnet new --install "Amazon.Lambda.Templates"`|
| [Avalonia UI Templates](https://github.com/AvaloniaUI/Avalonia) - Avalonia is a framework for creating cross platform UI | `dotnet new --install "Avalonia.Templates"`|
| [Blazor](http://blazor.net) - Full stack web development with C# and WebAssembly | `dotnet new --install "Microsoft.AspNetCore.Blazor.Templates::3.0.0-*"`|
| [Cake.Frosting](https://github.com/cake-build/cake) | `dotnet new --install "Cake.Frosting.Template"` |
| [Carter](https://github.com/CarterCommunity/Carter) - Carter is a library that allows Nancy-esque routing for use with ASP.Net Core. | `dotnet new --install "CarterTemplate"`|
| [CleanBlazor](https://github.com/fvilches17/CleanBlazor) - Minimal Blazor projects. Standard Blazor project templates minus any boilerplate assets (e.g. Bootstrap, Counter.razor, etc.)  | `dotnet new --install "FriscoVInc.DotNet.Templates.CleanBlazor"` |
| [cloudscribe](https://www.cloudscribe.com/docs/introduction) | `dotnet new --install "cloudscribe.templates"` |
| [DotVVM](https://github.com/riganti/dotvvm) - Open source MVVM framework for line of business web applications | `dotnet new --install "DotVVM.Templates"` |
| [Eto.Forms](https://github.com/picoe/Eto) | `dotnet new --install "Eto.Forms.Templates"` |
| [GCC.Build](https://github.com/roozbehid/dotnet-vcxproj) - C/C++/CPP/VCXPROJ Build Template using GCC/G++/EMCC or your favorate compiler | `dotnet new --install GCC.Build.Template` |
| [Geco](https://github.com/iQuarc/Geco) - C# 6 Interpolated strings based Code Generator | `dotnet new --install "iQuarc.Geco.CSharp"` |
| [GtkSharp](https://github.com/GtkSharp/GtkSharp) | `dotnet new --install "GtkSharp.Template.CSharp"` |
| [IdentityServer4.Templates](https://github.com/IdentityServer/IdentityServer4.Templates) | `dotnet new --install "identityserver4.templates"` |
| [Kentico Cloud Boilerplate](https://github.com/Kentico/cloud-boilerplate-net) | `dotnet new --install "KenticoCloud.CloudBoilerplateNet"` |
| [MonoGame (.NET Core)](https://github.com/MonoGame/MonoGame) | `dotnet new --install "MonoGame.Templates.CSharp"` |
| [MSBuild extension](https://github.com/tintoy/msbuild-extension-template) | `dotnet new --install "MSBuildExtensionTemplate"` |
| [MvxScaffolding Templates](https://github.com/Plac3hold3r/MvxScaffolding) - MvvmCross Xamarin native and Xamarin Forms templates. | `dotnet new --install "MvxScaffolding.Templates"` |
| [NancyFX Template](https://github.com/jchannon/NancyTemplate) | not on nuget.org |
| [NSpec Templates](https://github.com/nspec/DotNetNewNSpec) | `dotnet new --install "dotnet-new-nspec"` |
| [NUnit 3 Test Project Template](https://github.com/nunit/dotnet-new-nunit) | `dotnet new --install "NUnit3.DotNetNew.Template"` |
| [Paulovich.Caju](https://github.com/ivanpaulovich/dotnet-new-caju) - .NET applications with Event Sourcing, Hexagonal or Clean Architectures styles | `dotnet new --install "Paulovich.Caju"` |
| [Paulovich.Manga](https://github.com/ivanpaulovich/manga-clean-architecture) - Clean Architecture for .NET Applications! | `dotnet new --install "Paulovich.Manga"` |
| [Particular Templates](https://docs.particular.net/nservicebus/dotnet-templates) - Templates targeting NServiceBus and other tools and libraries from [Particular Software](https://particular.net/) | `dotnet new --install "ParticularTemplates"` |
| [Pioneer Console Boilerplate](https://github.com/PioneerCode/pioneer-console-boilerplate) - Boilerplated .NET Core console application that includes dependency injection, logging and configuration. | `dotnet new --install "Pioneer.Console.Boilerplate"` |
| [PowerShell Core](https://github.com/tintoy/ps-core-module-template) | `dotnet new --install "FiftyProtons.Templates.PSCore"` |
| [Prism Forms QuickStarts](https://github.com/dansiegel/Prism-Templates) - Empty &amp; QuickStart project Templates for Prism for Xamarin Forms. *Requires dotnet cli 2.0 pre3+* | `dotnet new --install "Prism.Forms.QuickstartTemplates"` |
| [Raspberry Pi 3](https://github.com/jeremylindsayni/RaspberryPiTemplate) - C# template for .NET Core 2 IoT applications. | `dotnet new --install "RaspberryPi.Template"` |
| [ServiceStack](https://github.com/NetCoreApps/templates) | `dotnet new --install "ServiceStack.Core.Templates"` |
| [SpecFlow.Templates.DotNet](https://github.com/SpecFlowOSS/SpecFlow) - A project template for creating executable specifications with SpecFlow. You can choose from different .NET frameworks and test frameworks. |`dotnet new --install "SpecFlow.Templates.DotNet"` |
| [Template templates](https://github.com/tintoy/dotnet-template-templates) - Templates to create new project and item templates. Requires `new3`. | `dotnet new --install "FiftyProtons.Templates.DotNetNew"` |
| [Zahasoft Templates](https://github.com/zahasoft/skele) | `dotnet new --install "Zahasoft.Skele"` |
| [ASP.NET Core Web API (extended)](https://github.com/popov1024/httpapi-template-sharp) | `dotnet new --install "Popov1024.HttpApi.Template.CSharp"` |
| [ASP.NET Core Web API for AKS](https://github.com/robbell/dotnet-aks-api-template) - A template for creating a fully-featured, 12 Factor, ASP.NET Core Web API for AKS | `dotnet new --install "RobBell.AksApi.Template"` |
| [HoNoSoFt.DotNet.Web.Spa.ProjectTemplates (VueJs + Picnic CSS)](https://github.com/Nordes/HoNoSoFt.DotNet.Web.Spa.ProjectTemplates) | `dotnet new --install "HoNoSoFt.DotNet.Web.Spa.ProjectTemplates"` |
| [xUnit Test Template](https://github.com/gatewayprogrammingschool/xUnit.Template) - Adds a xUnit test file to an existing test project. | `dotnet new --install GatewayProgrammingSchool.xUnit.CSharp`|
[RocketMod Plugin Templates](https://github.com/RocketMod/Rocket.Templates) RocketMod is a plugin framework for .NET based games. This template allows to quickly get started with a new RocketMod Plugin.| `dotnet new --install "Rocket.Templates"` |
| [EISK Web Api](https://github.com/eisk/eisk.webapi) - ASP.NET Core templates with simple use cases to build scalable web api with architectural best practices (DDD, Onion Architecture etc). | `dotnet new --install "eisk.webapi"` |
|[OpenMod Plugin Templates](https://github.com/openmod/openmod/tree/master/templates) - OpenMod is .NET plugin framework. These templates allow user to quickly get started with a new OpenMod Plugin.| `dotnet new --install "OpenMod.Templates"` |

# F# Templates

| Name     | Quick Install |
|----------|:--------------|
| [ASP.NET Core WebAPI F# Template](https://github.com/MNie/FSharpNetCoreWebApiTemplate) | `dotnet new --install "WebAPI.FSharp.Template"` |
| [Bolero: F# in WebAssembly](https://fsbolero.io/)| `dotnet new --install Bolero.Templates`|
| [Eto.Forms](https://github.com/picoe/Eto) | `dotnet new --install "Eto.Forms.Templates"` |
| [Expecto Template](https://github.com/MNie/Expecto.Template) | `dotnet new --install "Expecto.Template"`|
| [F# TypeProvider Template](https://github.com/fsprojects/FSharp.TypeProviders.SDK#the-f-type-provider-sdk)| `dotnet new --install FSharp.TypeProviders.Templates`|
| [Fable-elmish](https://github.com/fable-compiler/fable-elmish) | `dotnet new --install "Fable.Template.Elmish.React"` |
| [Fable, F# \|> Babel](http://fable.io) | `dotnet new --install "Fable.Template"` |
| [Fable Library](https://github.com/TheAngryByrd/Fable.Template.Library) - F# Template for creating and publishing Fable libraries | `dotnet new --install "Fable.Template.Library"` |
| [Fabulous for Xamarin.Forms](https://github.com/fsprojects/Fabulous/tree/master/Fabulous.XamarinForms)| `dotnet new --install Fabulous.XamarinForms.Templates`|
| [Freya](https://freya.io) | `dotnet new --install "Freya.Template"` |
| [Giraffe Template](https://github.com/giraffe-fsharp/giraffe-template) | `dotnet new --install "giraffe-template"` |
| [GtkSharp](https://github.com/GtkSharp/GtkSharp) | `dotnet new --install "GtkSharp.Template.FSharp"` |
| [Interstellar](https://github.com/fsprojects/Interstellar) | `dotnet new --install "Interstellar.Template"` |
| [MiniScaffold](https://github.com/TheAngryByrd/MiniScaffold) - F# Template for creating and publishing libraries targeting .NET Full (net45) and Core (netstandard1.6) | `dotnet new --install "MiniScaffold"` |
| [NancyFx](https://github.com/MNie/NancyFxCore)| `dotnet new --install "NancyFx.Core.Template"`|
| [SAFE Template](https://safe-stack.github.io/)| `dotnet new --install "SAFE.Template"`|
| [vbfox's F# Templates](https://github.com/vbfox/FSharpTemplates)| `dotnet new --install "BlackFox.DotnetNew.FSharpTemplates"`|
| [WebSharper](https://github.com/dotnet-websharper/core)| `dotnet new --install "WebSharper.Templates"`

# VBNet Templates

| Name     | Quick Install |
|----------|:--------------|
| [GtkSharp](https://github.com/GtkSharp/GtkSharp) | `dotnet new --install "GtkSharp.Template.VBNet"` |
| [InteXX Assorted Templates](https://github.com/InteXX/Templates) | `dotnet new --install "Intexx.Templates"` |
