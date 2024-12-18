// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.Extensions.Tools.Internal
{
    internal class TestConsole : IConsole
    {
        public event ConsoleCancelEventHandler? CancelKeyPress;
        public event Action<ConsoleKeyInfo>? KeyPressed;

        private readonly TestOutputWriter _testWriter;

        public TextWriter Error { get; }
        public TextWriter Out { get; }
        public TextReader In { get; set; } = new StringReader(string.Empty);
        public bool IsInputRedirected { get; set; } = false;
        public bool IsOutputRedirected { get; } = false;
        public bool IsErrorRedirected { get; } = false;
        public ConsoleColor ForegroundColor { get; set; }

        public TestConsole(ITestOutputHelper output)
        {
            _testWriter = new TestOutputWriter(output);
            Error = _testWriter;
            Out = _testWriter;
        }

        public void Clear() { }

        public void PressKey(ConsoleKeyInfo key)
        {
            Assert.NotNull(KeyPressed);
            KeyPressed.Invoke(key);
        }

        public void PressCancelKey()
        {
            Assert.NotNull(CancelKeyPress);

            var ctor = typeof(ConsoleCancelEventArgs)
                .GetTypeInfo()
                .DeclaredConstructors
                .Single(c => c.GetParameters().First().ParameterType == typeof(ConsoleSpecialKey));

            CancelKeyPress.Invoke(this, (ConsoleCancelEventArgs)ctor.Invoke([ConsoleSpecialKey.ControlC]));
        }

        public void ResetColor()
        {
        }

        public string GetOutput()
        {
            return _testWriter.GetOutput();
        }

        public void ClearOutput()
        {
            _testWriter.ClearOutput();
        }

        private class TestOutputWriter : TextWriter
        {
            private readonly ITestOutputHelper _output;
            private readonly StringBuilder _sb = new();
            private readonly StringBuilder _currentOutput = new();

            public TestOutputWriter(ITestOutputHelper output)
            {
                _output = output;
            }

            public override Encoding Encoding => Encoding.Unicode;

            public override void Write(char value)
            {
                if (value == '\r' || value == '\n')
                {
                    if (_sb.Length > 0)
                    {
                        _output.WriteLine(_sb.ToString());
                        _sb.Clear();
                    }

                    _currentOutput.Append(value);
                }
                else
                {
                    _sb.Append(value);
                    _currentOutput.Append(value);
                }
            }

            public string GetOutput()
            {
                return _currentOutput.ToString();
            }

            public void ClearOutput()
            {
                _currentOutput.Clear();
            }
        }
    }
}
