// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Verify.XunitV3 and xunit.v3.assert are available transitively (via the TemplateVerifier tool
// and Microsoft.NET.TestFramework), which makes Assert, TestContext and Verifier ambiguous.
// Pin them to the MSTest types.
global using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
global using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;
global using Verifier = VerifyMSTest.Verifier;
