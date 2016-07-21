using System;
using System.Diagnostics;

namespace Microsoft.TemplateEngine.Core
{
    public class Timing : IDisposable
    {
        private readonly Action<TimeSpan> _result;
        private readonly Stopwatch _stopwatch;

        public Timing(Action<TimeSpan> result)
        {
            _result = result;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _result(_stopwatch.Elapsed);
        }
    }
}
