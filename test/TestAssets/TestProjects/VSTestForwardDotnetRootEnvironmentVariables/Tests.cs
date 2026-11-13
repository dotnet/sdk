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
                if (env.Key.ToString().Contains("VSTEST_"))
                {
                    Console.WriteLine($"{env.Key.ToString()}={env.Value.ToString()}");
                }
            }

            // Reported so that the SDK tests can assert which artifacts directory the test host actually runs
            // with. Printed unconditionally - including when the value is empty - so that a missing variable is
            // an assertion failure rather than a missing line.
            Console.WriteLine($"TESTHOST_VSTEST_ARTIFACTS_DIRECTORY={Environment.GetEnvironmentVariable("VSTEST_ARTIFACTS_DIRECTORY")}");
            Console.WriteLine($"TESTHOST_TEMPPATH={Path.GetTempPath()}");
        }
    }
}
