// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Tools.Internal
{
    internal sealed class ConsoleInputReader(IConsole console, bool quiet, bool suppressEmojis)
    {
        private readonly object _writeLock = new();

        public async Task<ConsoleKey> GetKeyAsync(string prompt, Func<ConsoleKey, bool> validateInput, CancellationToken cancellationToken)
        {
            if (quiet)
            {
                return ConsoleKey.Escape;
            }

            var questionMark = suppressEmojis ? "?" : "❔";
            while (true)
            {
                WriteLine($"  {questionMark} {prompt}");

                lock (_writeLock)
                {
                    console.ForegroundColor = ConsoleColor.DarkGray;
                    console.Out.Write($"  {questionMark} ");
                    console.ResetColor();
                }

                var tcs = new TaskCompletionSource<ConsoleKey>(TaskCreationOptions.RunContinuationsAsynchronously);
                console.KeyPressed += KeyPressed;
                try
                {
                    return await tcs.Task.WaitAsync(cancellationToken);
                }
                catch (ArgumentException)
                {
                    // Prompt again for valid input
                }
                finally
                {
                    console.KeyPressed -= KeyPressed;
                }

                void KeyPressed(ConsoleKeyInfo key)
                {
                    if (validateInput(key.Key))
                    {
                        WriteLine(key.KeyChar.ToString());
                        tcs.TrySetResult(key.Key);
                    }
                    else
                    {
                        WriteLine(key.KeyChar.ToString(), ConsoleColor.DarkRed);
                        tcs.TrySetException(new ArgumentException($"Invalid key {key.KeyChar} entered."));
                    }
                }
            }

            void WriteLine(string message, ConsoleColor color = ConsoleColor.DarkGray)
            {
                lock (_writeLock)
                {
                    console.ForegroundColor = color;
                    console.Out.WriteLine(message);
                    console.ResetColor();
                }
            }
        }
    }
}
