using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    internal sealed class PerformanceLogEventListener : EventListener
    {
        internal struct ProviderConfiguration
        {
            internal string Name { get; set; }
            internal EventKeywords Keywords { get; set; }
            internal EventLevel Level { get; set; }
        }

        private static ProviderConfiguration[] s_config = new ProviderConfiguration[]
        {
            new ProviderConfiguration()
            {
                Name = "Microsoft-Dotnet-CLI-Performance",
                Keywords = EventKeywords.All,
                Level = EventLevel.Verbose
            }
        };

        private const char EventDelimiter = '\n';
        private StreamWriter _writer;

        [ThreadStatic]
        private StringBuilder s_builder;

        internal static PerformanceLogEventListener Create(IFileSystem fileSystem, string logDirectory)
        {
            PerformanceLogEventListener eventListener = null;
            try
            {
                // Initialization happens as a separate step and not in the constructor to ensure that
                // if an exception is thrown during init, we have the opportunity to dispose of the listener,
                // which will disable any EventSources that have been enabled.  Any EventSources that existed before
                // this EventListener will be passed to OnEventSourceCreated before our constructor is called, so
                // we if we do this work in the constructor, and don't get an opportunity to call Dispose, the
                // EventSources will remain enabled even if there aren't any consuming EventListeners.
                eventListener = new PerformanceLogEventListener();
                eventListener.Initialize(fileSystem, logDirectory);
            }
            catch
            {
                if(eventListener != null)
                {
                    eventListener.Dispose();
                }
            }

            return eventListener;
        }

        private PerformanceLogEventListener()
        {
        }

        internal void Initialize(IFileSystem fileSystem, string logDirectory)
        {
            // TODO: Should we choose something that is guaranteed to be unique?
            int pid = Process.GetCurrentProcess().Id;
            string logFilePath = Path.Combine(logDirectory, $"perf-{pid}.log");

            Stream outputStream = fileSystem.File.OpenFile(
                logFilePath,
                FileMode.Create,    // Create or overwrite.
                FileAccess.Write,   // Open for writing.
                FileShare.Read,     // Allow others to read.
                4096,               // Default buffer size.
                FileOptions.None);  // No hints about how the file will be written.

            _writer = new StreamWriter(outputStream);
        }

        public override void Dispose()
        {
            lock (this)
            {
                if (_writer != null)
                {
                    _writer.Dispose();
                    _writer = null;
                }
            }

            base.Dispose();
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            try
            {
                // Enable the provider if it matches a requested configuration.
                foreach (ProviderConfiguration entry in s_config)
                {
                    if (entry.Name.Equals(eventSource.Name))
                    {
                        EnableEvents(eventSource, entry.Level, entry.Keywords);
                    }
                }
            }
            catch
            {
                // TODO
            }

            base.OnEventSourceCreated(eventSource);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            try
            {
                if (s_builder == null)
                {
                    s_builder = new StringBuilder();
                }
                else
                {
                    s_builder.Clear();
                }

                s_builder.Append($"[{eventData.TimeStamp.ToString("MM/dd/yyyy H:mm:ss.ffffff")}] Event={eventData.EventSource.Name}/{eventData.EventName} ThreadID={eventData.OSThreadId}\t ");
                for (int i = 0; i < eventData.PayloadNames.Count; i++)
                {
                    s_builder.Append($"{eventData.PayloadNames[i]}=\"{eventData.Payload[i]}\" ");
                }

                lock (this)
                {
                    if (_writer != null)
                    {
                        foreach (ReadOnlyMemory<char> mem in s_builder.GetChunks())
                        {
                            _writer.Write(mem);
                        }
                        _writer.Write(EventDelimiter);
                    }
                }
            }
            catch
            {
                // TODO
            }

            base.OnEventWritten(eventData);
        }
    }
}
