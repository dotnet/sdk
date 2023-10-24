// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal enum RudeEditAction
    {
        /// <summary>
        /// Restarts the app.
        /// </summary>
        Restart,

        /// <summary>
        /// Continue the session. The user may update the code to remove rude edits.
        /// </summary>
        Continue,
    }

    internal sealed class RudeEditDialog
    {
        private readonly IReporter _reporter;
        private readonly IRequester _requester;
        private readonly IConsole _console;
        private RudeEditAction? _preferredAction;

        public RudeEditDialog(IReporter reporter, IRequester requester, IConsole console)
        {
            _reporter = reporter;
            _requester = requester;
            _console = console;

            var alwaysRestart = Environment.GetEnvironmentVariable("DOTNET_WATCH_RESTART_ON_RUDE_EDIT");

            if (alwaysRestart == "1" || string.Equals(alwaysRestart, "true", StringComparison.OrdinalIgnoreCase))
            {
                _reporter.Verbose($"DOTNET_WATCH_RESTART_ON_RUDE_EDIT = '{alwaysRestart}'. Restarting without prompt.");
                _preferredAction = RudeEditAction.Restart;
            }
        }

        /// <summary>
        /// Returns true to restart the app.
        /// </summary>
        public async Task<RudeEditAction> EvaluateAsync(CancellationToken cancellationToken)
        {
            if (_preferredAction.HasValue)
            {
                return _preferredAction.Value;
            }

            var key = await _requester.GetKeyAsync(
                "Do you want to restart your app - Yes (y) / No (n) / Always (a) / Never (v)?",
                validateInput: key => key is ConsoleKey.Y or ConsoleKey.N or ConsoleKey.A or ConsoleKey.V,
                cancellationToken);

            switch (key)
            {
                case ConsoleKey.Escape:
                case ConsoleKey.Y:
                    return RudeEditAction.Restart;

                case ConsoleKey.N:
                    return RudeEditAction.Continue;

                case ConsoleKey.A:
                    _preferredAction = RudeEditAction.Restart;
                    return RudeEditAction.Restart;

                case ConsoleKey.V:
                    _preferredAction = RudeEditAction.Continue;
                    return RudeEditAction.Continue;

                default:
                    throw new UnreachableException();
            }
        }
    }
}
