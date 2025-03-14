// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Tasks;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class RestartPrompt(IReporter reporter, ConsoleInputReader requester, bool? noPrompt)
    {
        private bool? _restartImmediatelySessionPreference = noPrompt;

        public async ValueTask<bool> WaitForRestartConfirmationAsync(string question, CancellationToken cancellationToken)
        {
            if (_restartImmediatelySessionPreference.HasValue)
            {
                reporter.Output("Restarting");
                return _restartImmediatelySessionPreference.Value;
            }

            var key = await requester.GetKeyAsync(
                $"{question} Yes (y) / No (n) / Always (a) / Never (v)",
                AcceptKey,
                cancellationToken);

            switch (key)
            {
                case ConsoleKey.Escape:
                case ConsoleKey.Y:
                    return true;

                case ConsoleKey.N:
                    return false;

                case ConsoleKey.A:
                    _restartImmediatelySessionPreference = true;
                    return true;

                case ConsoleKey.V:
                    _restartImmediatelySessionPreference = false;
                    return false;
            }

            throw new InvalidOperationException();

            static bool AcceptKey(ConsoleKeyInfo info)
                => info is { Key: ConsoleKey.Y or ConsoleKey.N or ConsoleKey.A or ConsoleKey.V, Modifiers: ConsoleModifiers.None };
        }
    }
}
