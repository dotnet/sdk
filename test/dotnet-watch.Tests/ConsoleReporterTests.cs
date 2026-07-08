// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class ConsoleReporterTests
{
    private static readonly string EOL = Environment.NewLine;

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void WritesToStandardStreams(bool suppressEmojis)
    {
        var testConsole = new TestConsole();
        var reporter = new ConsoleReporter(testConsole, "test prefix", suppressEmojis: suppressEmojis);

        reporter.Report(id: default, Emoji.Watch, LogLevel.Trace, "trace {0}");
        Assert.AreEqual($"test prefix {(suppressEmojis ? ":" : "⌚")} trace {{0}}" + EOL, testConsole.GetError());
        testConsole.Clear();

        reporter.Report(id: default, Emoji.Watch, LogLevel.Debug, "verbose");
        Assert.AreEqual($"test prefix {(suppressEmojis ? ":" : "⌚")} verbose" + EOL, testConsole.GetError());
        testConsole.Clear();

        reporter.Report(id: default, Emoji.Watch, LogLevel.Information, "out");
        Assert.AreEqual($"test prefix {(suppressEmojis ? ":" : "⌚")} out" + EOL, testConsole.GetError());
        testConsole.Clear();

        reporter.Report(id: default, Emoji.Warning, LogLevel.Warning, "warn");
        Assert.AreEqual($"test prefix {(suppressEmojis ? ":" : "⚠")} warn" + EOL, testConsole.GetError());
        testConsole.Clear();

        reporter.Report(id: default, Emoji.Error, LogLevel.Error, "error");
        Assert.AreEqual($"test prefix {(suppressEmojis ? ":" : "❌")} error" + EOL, testConsole.GetError());
        testConsole.Clear();

        reporter.Report(id: default, Emoji.Error, LogLevel.Critical, "critical");
        Assert.AreEqual($"test prefix {(suppressEmojis ? ":" : "❌")} critical" + EOL, testConsole.GetError());
        testConsole.Clear();
    }

    private class TestConsole : IConsole
    {
        private readonly StringBuilder _out;
        private readonly StringBuilder _error;
        public TextWriter Out { get; }
        public TextWriter Error { get; }
        public ConsoleColor ForegroundColor { get; set; }

        public TestConsole()
        {
            _out = new StringBuilder();
            _error = new StringBuilder();
            Out = new StringWriter(_out);
            Error = new StringWriter(_error);
        }

        event Action<ConsoleKeyInfo> IConsole.KeyPressed
        {
            add { }
            remove { }
        }

        public string GetOutput()
            => _out.ToString();

        public string GetError()
            => _error.ToString();

        public void Clear()
        {
            _out.Clear();
            _error.Clear();
        }

        public void ResetColor()
        {
            ForegroundColor = default;
        }
    }
}
