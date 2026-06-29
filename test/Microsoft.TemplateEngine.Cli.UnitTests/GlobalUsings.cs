// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Microsoft.TemplateEngine.TestHelper still references xunit.v3.extensibility.core
// (it is consumed by other in-tree xUnit projects and is not part of this migration).
// EnvironmentSettingsHelper's constructor takes an Xunit.Sdk.IMessageSink. We surface
// it here with a short alias so the tests below can construct a no-op sink without
// pulling in xUnit's global usings (which would conflict with MSTest's Assert).
global using IMessageSink = Xunit.Sdk.IMessageSink;
global using IMessageSinkMessage = Xunit.Sdk.IMessageSinkMessage;

// Verify.MSTest's VerifyBase is used by snapshot test classes (HelpTests,
// TabCompletionTests via BaseTest).
global using VerifyMSTest;
