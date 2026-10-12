// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.IO;

namespace TestNamespace
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestForwardDotnetRootEnvironmentVariables()
        {
            // This project is compiled, and executed by the tests in "test/dotnet-test.Tests/GivenDotnetTestForwardDotnetRootEnvironmentVariables.cs"
            foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                if (env.Key.ToString().Contains("VSTEST_WINAPPHOST_"))
                {
                    Console.WriteLine($"{env.Key.ToString()}={env.Value.ToString()}");
                }
            }

            Console.WriteLine($"TESTHOST_VSTEST_ARTIFACTS_DIRECTORY={Environment.GetEnvironmentVariable("VSTEST_ARTIFACTS_DIRECTORY")}");
            Console.WriteLine($"TESTHOST_TEMPPATH={Path.GetTempPath()}");
        }
    }
}
