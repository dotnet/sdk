// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Verify.MSTest's VerifyBase is used by snapshot test classes (HelpTests,
// TabCompletionTests via BaseTest).
global using VerifyMSTest;

// The xUnit-only Directory.Build.targets adds these as global usings for VSTest
// projects. MSTest.Sdk projects do not get them automatically.
global using Microsoft.NET.TestFramework;
global using Microsoft.NET.TestFramework.Utilities;
