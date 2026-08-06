// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class ConsoleReporterTests
    {
        private static readonly string EOL = Environment.NewLine;

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WritesToStandardStreams(bool suppressEmojis)
        {
            var testConsole = new TestConsole();
            var reporter = new ConsoleReporter(testConsole, verbose: true, quiet: false, suppressEmojis: suppressEmojis);

            reporter.Report(id: default, Emoji.Watch, MessageSeverity.Verbose, "verbose {0}");
            Assert.Equal($"dotnet watch {(suppressEmojis ? ":" : "⌚")} verbose {{0}}" + EOL, testConsole.GetError());
            testConsole.Clear();

            reporter.Report(id: default, Emoji.Watch, MessageSeverity.Output, "out");
            Assert.Equal($"dotnet watch {(suppressEmojis ? ":" : "⌚")} out" + EOL, testConsole.GetError());
            testConsole.Clear();

            reporter.Report(id: default, Emoji.Warning, MessageSeverity.Warning, "warn");
            Assert.Equal($"dotnet watch {(suppressEmojis ? ":" : "⚠")} warn" + EOL, testConsole.GetError());
            testConsole.Clear();

            reporter.Report(id: default, Emoji.Error, MessageSeverity.Error, "error");
            Assert.Equal($"dotnet watch {(suppressEmojis ? ":" : "❌")} error" + EOL, testConsole.GetError());
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
}
