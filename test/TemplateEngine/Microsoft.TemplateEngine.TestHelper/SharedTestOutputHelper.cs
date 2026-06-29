// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Microsoft.TemplateEngine.TestHelper
{
    /// <summary>
    /// This is so we can pass ITestOutputHelper to TestCommand constructor
    /// when calling from SharedHomeDirectory.
    /// </summary>
    public class SharedTestOutputHelper : ITestOutputHelper
    {
        private readonly IMessageSink _sink;
        private readonly StringBuilder _output = new();

        public SharedTestOutputHelper(IMessageSink sink)
        {
            this._sink = sink;
        }

        public string Output => _output.ToString();

        public void Write(string message)
        {
            _output.Append(message);
            _sink.OnMessage(new DiagnosticMessage(message));
        }

        public void Write(string format, params object[] args)
        {
            string message = string.Format(format, args);
            _output.Append(message);
            _sink.OnMessage(new DiagnosticMessage(message));
        }

        public void WriteLine(string message)
        {
            _output.AppendLine(message);
            _sink.OnMessage(new DiagnosticMessage(message));
        }

        public void WriteLine(string format, params object[] args)
        {
            string message = string.Format(format, args);
            _output.AppendLine(message);
            _sink.OnMessage(new DiagnosticMessage(message));
        }
    }
}
