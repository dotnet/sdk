// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Test
{
    internal class TestOutputLogger : ITestOutputHelper
    {
        public static readonly TestOutputLogger Instance = new TestOutputLogger();

        private readonly StringBuilder _output = new();

        public string Output => _output.ToString();

        public void Write(string message)
        {
            _output.Append(message);
            Console.Write(message);
        }

        public void Write(string format, params object[] args)
        {
            string message = string.Format(format, args);
            _output.Append(message);
            Console.Write(message);
        }

        public void WriteLine(string message)
        {
            _output.AppendLine(message);
            Console.WriteLine(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            string message = string.Format(format, args);
            _output.AppendLine(message);
            Console.WriteLine(message);
        }
    }
}
