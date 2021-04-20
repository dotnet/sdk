// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class Timing : IDisposable
    {
        private static int _depth = -1;
        private readonly Action<TimeSpan, int> _result;
        private readonly Stopwatch _stopwatch;

        public static Timing Over(ITemplateEngineHost host, string label)
        {
            return new Timing((x, d) =>
            {
                host.LogTiming(label, x, d);
                //string indent = string.Join("", Enumerable.Repeat("  ", d));
                //Console.WriteLine($"{indent} {label} {x.TotalMilliseconds}");
            });
        }

        public Timing(Action<TimeSpan, int> result)
        {
            ++_depth;
            _result = result;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _result(_stopwatch.Elapsed, _depth);
            --_depth;
        }
    }
}
