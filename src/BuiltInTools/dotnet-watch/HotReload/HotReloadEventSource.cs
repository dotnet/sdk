using System;
using System.Diagnostics.Tracing;

namespace Microsoft.DotNet.Watcher.Tools
{
    [EventSource(Name = "HotReload")]
    class HotReladEventSource : EventSource
    {

        public static enum StartType
        {
            Main,
            StaticHandler,
            CompilationHandler,
            ScopedCssHandler
        }

        public class Keywords
        {
            public const EventKeywords Perf = (EventKeywords)1;
        }

        [Event(1, Message = "Hot reload started for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadStart(StartType s) { WriteEvent(1, s); }

        [Event(2, Message = "Hot reload finished for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadEnd(StartType s) { WriteEvent(2, s); }

        public static HotReladEventSource Log = new HotReladEventSource();
    }
}