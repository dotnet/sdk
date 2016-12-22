using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Utils
{
    public class DefaultTemplateEngineHost : ITemplateEngineHost
    {
        private readonly IReadOnlyDictionary<string, string> _hostDefaults;
        private readonly IReadOnlyList<KeyValuePair<Guid, Func<Type>>> _hostBuiltInComponents;
        private static readonly IReadOnlyList<KeyValuePair<Guid, Func<Type>>> NoComponents = new KeyValuePair<Guid, Func<Type>>[0];

        public DefaultTemplateEngineHost(string hostIdentifier, string version, string locale)
            : this(hostIdentifier, version, locale, null)
        {
        }

        public DefaultTemplateEngineHost(string hostIdentifier, string version, string locale, Dictionary<string, string> defaults)
            : this (hostIdentifier, version, locale, defaults, NoComponents)
        {
        }

        public DefaultTemplateEngineHost(string hostIdentifier, string version, string locale, Dictionary<string, string> defaults, IReadOnlyList<KeyValuePair<Guid, Func<Type>>> builtIns)
        {
            HostIdentifier = hostIdentifier;
            Version = version;
            Locale = locale;
            _hostDefaults = defaults ?? new Dictionary<string, string>();
            FileSystem = new PhysicalFileSystem();
            _hostDefaults = defaults;
            _hostBuiltInComponents = builtIns;
        }

        public IPhysicalFileSystem FileSystem { get; }

        public string Locale { get; private set; }

        public void UpdateLocale(string newLocale)
        {
            Locale = newLocale;
        }

        public string HostIdentifier { get; }

        public string Version { get; }

        public virtual IReadOnlyList<KeyValuePair<Guid, Func<Type>>> BuiltInComponents => _hostBuiltInComponents;

        public virtual void LogMessage(string message)
        {
            //Console.WriteLine("LogMessage: {0}", message);
            Console.WriteLine(message);
        }

        public virtual void OnCriticalError(string code, string message, string currentFile, long currentPosition)
        {
        }

        public virtual bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            LogMessage(string.Format($"Error: {message}"));
            return false;
        }

        public virtual bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
        {
            newValue = null;
            return false;
        }

        public virtual void OnSymbolUsed(string symbol, object value)
        {
        }

        public virtual void OnTimingCompleted(string label, TimeSpan timing)
        {
            LogMessage(string.Format("{0}: {1} ms", label, timing.TotalMilliseconds));
        }

        // stub that will be built out soon.
        public virtual bool TryGetHostParamDefault(string paramName, out string value)
        {
            switch (paramName)
            {
                case "HostIdentifier":
                    value = HostIdentifier;
                    return true;
            }

            return _hostDefaults.TryGetValue(paramName, out value);
        }
    }
}
