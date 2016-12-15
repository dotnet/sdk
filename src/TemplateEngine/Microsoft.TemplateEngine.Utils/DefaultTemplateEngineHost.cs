using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Utils
{
    public class DefaultTemplateEngineHost : ITemplateEngineHost
    {
        private readonly IReadOnlyDictionary<string, string> _hostDefaults;

        public DefaultTemplateEngineHost(string hostIdentifier, string locale)
        {
            Locale = locale;
            HostIdentifier = hostIdentifier;
            _hostDefaults = new Dictionary<string, string>();
            FileSystem = new PhysicalFileSystem();
        }

        public DefaultTemplateEngineHost(string hostIdentifier, string locale, Dictionary<string, string> defaults)
        {
            Locale = locale;
            HostIdentifier = hostIdentifier;
            _hostDefaults = defaults;
        }

        public IPhysicalFileSystem FileSystem { get; }

        public string Locale { get; }

        public string HostIdentifier { get; }

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
            //Console.WriteLine("DefaultTemplateEngineHost::OnParameterError() called");
            Console.WriteLine("Parameter Error: {0}", message);
            Console.WriteLine("Parameter name = {0}", parameter.Name);
            Console.WriteLine("Parameter value = {0}", receivedValue);
            Console.WriteLine("Enter a new value for the param, or:");
            newValue = Console.ReadLine();
            return !string.IsNullOrEmpty(newValue);
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
            value = null;
            return false;
        }
    }
}
