// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.DotNet.Watcher.Tools
{
    [EventSource(Name = "HotReload")]
    internal sealed class HotReloadEventSource : EventSource
    {
        public enum StartType
        {
            Main,
            StaticHandler,
            CompilationHandler,
            ScopedCssHandler
        }

        internal sealed class Keywords
        {
            public const EventKeywords Perf = (EventKeywords)1;
        }

        [Event(1, Message = "Hot reload started for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadStart(StartType handlerType) { WriteEvent(1, handlerType); }

        [Event(2, Message = "Hot reload finished for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadEnd(StartType handlerType) { WriteEvent(2, handlerType); }

        public static readonly HotReloadEventSource Log = new HotReloadEventSource();
    }
}
