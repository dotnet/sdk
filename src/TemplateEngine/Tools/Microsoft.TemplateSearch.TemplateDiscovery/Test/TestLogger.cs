// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Test
{
    internal class TestOutputLogger : ITestOutputHelper
    {
        public static readonly TestOutputLogger Instance = new TestOutputLogger();

        public string Output => string.Empty;

        public void Write(string message) => Console.Write(message);

        public void Write(string format, params object[] args) => Console.Write(format, args);

        public void WriteLine(string message) => Console.WriteLine(message);

        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
    }
}
