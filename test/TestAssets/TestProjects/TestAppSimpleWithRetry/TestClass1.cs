// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass1
{
    [TestMethod]
    public void TestMethod1()
    {
        if (Environment.GetCommandLineArgs().Any(arg => arg.Contains("Retries") && !arg.EndsWith("2")))
        {
            Assert.Fail("Failing...");
        }
    }
}