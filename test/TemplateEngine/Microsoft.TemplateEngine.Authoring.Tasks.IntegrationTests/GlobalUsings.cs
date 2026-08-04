// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// The shared Microsoft.TemplateEngine.CommandUtils sources (compiled into this project
// via <Compile Include="$(TemplateEngSrcDir)Tools\Shared\**\*.cs" />) reference
// ITestOutputHelper by its short name. Bind it to the runner-agnostic
// Microsoft.NET.TestFramework.ITestOutputHelper (from Microsoft.NET.TestFramework.MSTest)
// so the shared file compiles under MSTest without any xUnit dependency.
global using ITestOutputHelper = Microsoft.NET.TestFramework.ITestOutputHelper;
