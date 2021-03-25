using System;
using System.Diagnostics.Tracing;

namespace Microsoft.DotNet.Watcher.Tools
{
    [EventSource(Name = "HotReload")]
    class HotReladEventSource : EventSource
    {
        public class Keywords
        {
            public const EventKeywords Perf = (EventKeywords)1;
        }

        [Event(1, Message = "Hot reload started for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadStaticFileStart(string fileChanged) { WriteEvent(1, fileChanged); }

        [Event(2, Message = "Hot reload finished for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadStaticFileEnd(string fileChanged) { WriteEvent(2, fileChanged); }

        [Event(3, Message = "Hot reload started for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadCompilationStart(string fileChanged) { WriteEvent(3, fileChanged); }

        [Event(4, Message = "Hot reload finished for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadCompilationEnd(string fileChanged) { WriteEvent(4, fileChanged); }

        public static HotReladEventSource Log = new HotReladEventSource();
    }
}