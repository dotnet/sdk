﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Xunit;

namespace Microsoft.Extensions.Tools.Internal
{
    public class ReporterTests
    {
        private static readonly string EOL = Environment.NewLine;

        [Fact]
        public void WritesToStandardStreams()
        {
            var testConsole = new TestConsole();
            var reporter = new ConsoleReporter(testConsole, verbose: true, quiet: false);
            var dotnetWatchDefaultPrefix = "dotnet watch ⌚ ";

            // stdout
            reporter.Verbose("verbose");
            Assert.Equal($"{dotnetWatchDefaultPrefix}verbose" + EOL, testConsole.GetOutput());
            testConsole.Clear();

            reporter.Output("out");
            Assert.Equal($"{dotnetWatchDefaultPrefix}out" + EOL, testConsole.GetOutput());
            testConsole.Clear();

            reporter.Warn("warn");
            Assert.Equal($"{dotnetWatchDefaultPrefix}warn" + EOL, testConsole.GetOutput());
            testConsole.Clear();

            // stderr
            reporter.Error("error");
            Assert.Equal($"dotnet watch ❌ error" + EOL, testConsole.GetError());
            testConsole.Clear();
        }

        [Fact]
        public void WritesToStandardStreamsWithCustomEmojis()
        {
            var testConsole = new TestConsole();
            var reporter = new ConsoleReporter(testConsole, verbose: true, quiet: false);
            var dotnetWatchDefaultPrefix = "dotnet watch";

            // stdout
            reporter.Verbose("verbose", emoji: "😄");
            Assert.Equal($"{dotnetWatchDefaultPrefix} 😄 verbose" + EOL, testConsole.GetOutput());
            testConsole.Clear();

            reporter.Output("out", emoji: "😄");
            Assert.Equal($"{dotnetWatchDefaultPrefix} 😄 out" + EOL, testConsole.GetOutput());
            testConsole.Clear();

            reporter.Warn("warn", emoji: "😄");
            Assert.Equal($"{dotnetWatchDefaultPrefix} 😄 warn" + EOL, testConsole.GetOutput());
            testConsole.Clear();

            // stderr
            reporter.Error("error", emoji: "😄");
            Assert.Equal($"{dotnetWatchDefaultPrefix} 😄 error" + EOL, testConsole.GetError());
            testConsole.Clear();
        }

        private class TestConsole : IConsole
        {
            private readonly StringBuilder _out;
            private readonly StringBuilder _error;

            event Action<ConsoleKeyInfo> IConsole.KeyPressed
            {
                add { }
                remove { }
            }

            public TestConsole()
            {
                _out = new StringBuilder();
                _error = new StringBuilder();
                Out = new StringWriter(_out);
                Error = new StringWriter(_error);
            }

            event ConsoleCancelEventHandler IConsole.CancelKeyPress
            {
                add { }
                remove { }
            }

            public string GetOutput() => _out.ToString();
            public string GetError() => _error.ToString();

            public void Clear()
            {
                _out.Clear();
                _error.Clear();
            }

            public void ResetColor()
            {
                ForegroundColor = default(ConsoleColor);
            }

            public TextWriter Out { get; }
            public TextWriter Error { get; }
            public TextReader In { get; }
            public bool IsInputRedirected { get; }
            public bool IsOutputRedirected { get; }
            public bool IsErrorRedirected { get; }
            public ConsoleColor ForegroundColor { get; set; }
        }
    }
}
