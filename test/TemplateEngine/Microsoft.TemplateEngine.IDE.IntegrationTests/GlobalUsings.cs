// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// xUnit v3 types are pulled in transitively (e.g., via Verify.XunitV3 / Microsoft.TemplateEngine.TestHelper),
// which makes TestContext ambiguous with MSTest's TestContext. Alias to MSTest's TestContext explicitly.
global using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
global using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;
