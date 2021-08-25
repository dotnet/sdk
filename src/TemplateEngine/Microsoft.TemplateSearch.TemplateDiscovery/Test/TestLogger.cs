// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Test
{
    internal class TestOutputLogger : ITestOutputHelper
    {
        public static readonly TestOutputLogger Instance = new TestOutputLogger();

        public void WriteLine(string message) => Console.WriteLine(message);

        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
    }
}
