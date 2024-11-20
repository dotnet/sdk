// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal sealed class PhysicalConsole : IConsole
    {
        public const char CtrlC = '\x03';
        public const char CtrlR = '\x12';

        public event Action<ConsoleKeyInfo>? KeyPressed;

        public PhysicalConsole(TestFlags testFlags)
        {
            Console.OutputEncoding = Encoding.UTF8;

            bool readFromStdin;
            if (testFlags.HasFlag(TestFlags.ReadKeyFromStdin))
            {
                readFromStdin = true;
            }
            else
            {
                try
                {
                    Console.TreatControlCAsInput = true;
                    readFromStdin = false;
                }
                catch
                {
                    // fails when stdin is redirected
                    readFromStdin = true;
                }
            }

            _ = readFromStdin ? ListenToStandardInputAsync() : ListenToConsoleKeyPressAsync();
        }

        private async Task ListenToStandardInputAsync()
        {
            using var stream = Console.OpenStandardInput();
            var buffer = new byte[1];

            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer, CancellationToken.None);
                if (bytesRead != 1)
                {
                    break;
                }

                var c = (char)buffer[0];

                // handle all input keys that watcher might consume:
                var key = c switch
                {
                    CtrlC => new ConsoleKeyInfo('C', ConsoleKey.C, shift: false, alt: false, control: true),
                    CtrlR => new ConsoleKeyInfo('R', ConsoleKey.R, shift: false, alt: false, control: true),
                    >= 'a' and <= 'z' => new ConsoleKeyInfo(c, ConsoleKey.A + (c - 'a'), shift: false, alt: false, control: false),
                    >= 'A' and <= 'Z' => new ConsoleKeyInfo(c, ConsoleKey.A + (c - 'A'), shift: true, alt: false, control: false),
                    _ => default
                };

                if (key.Key != ConsoleKey.None)
                {
                    KeyPressed?.Invoke(key);
                }
            }
        }

        private Task ListenToConsoleKeyPressAsync()
            => Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    var key = Console.ReadKey(intercept: true);
                    KeyPressed?.Invoke(key);
                }
            }, TaskCreationOptions.LongRunning);

        public TextWriter Error => Console.Error;
        public TextWriter Out => Console.Out;

        public ConsoleColor ForegroundColor
        {
            get => Console.ForegroundColor;
            set => Console.ForegroundColor = value;
        }

        public void ResetColor() => Console.ResetColor();
        public void Clear() => Console.Clear();
    }
}
