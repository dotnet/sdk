// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// The shared Microsoft.TemplateEngine.CommandUtils sources (compiled into this project
// via <Compile Include="$(TemplateEngSrcDir)Tools\Shared\**\*.cs" />) reference
// xUnit's ITestOutputHelper by its short name. The Xunit type is provided
// transitively (compile-only) through the Microsoft.TemplateEngine.TestHelper project
// reference, but the global using normally added by xunit.v3.extensibility.core's
// build targets does not flow through ProjectReferences. We add the minimal alias
// here so the shared file compiles without dragging the entire Xunit global using
// (which would conflict with MSTest's Assert).
global using ITestOutputHelper = Xunit.ITestOutputHelper;
