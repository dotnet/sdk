// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.Utils
{
    public sealed class Timing : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _label;
        private readonly Stopwatch _stopwatch;
        private readonly IDisposable _disposable;

        public Timing(ILogger logger, string label)
        {
            _logger = logger;
            _label = label;
            _stopwatch = Stopwatch.StartNew();
            _disposable = logger.BeginScope(_label);
            _logger.LogDebug($"{_label} started");
        }

        public static Timing Over(ILogger logger, string label)
        {
            return new Timing(logger, label);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.LogDebug($"{_label} finished, took {_stopwatch.ElapsedMilliseconds} ms");
            _disposable.Dispose();
        }
    }
}
