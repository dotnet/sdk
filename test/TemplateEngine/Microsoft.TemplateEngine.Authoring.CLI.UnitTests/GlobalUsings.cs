// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// xunit.v3.assert is available transitively (via Microsoft.NET.TestFramework),
// which causes Assert and TestContext to be ambiguous. Pin to the MSTest types.
global using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
global using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

// The shared Microsoft.TemplateEngine.CommandUtils sources reference
// ITestOutputHelper by its short name. Provide the alias so shared code compiles.
global using ITestOutputHelper = Xunit.ITestOutputHelper;
