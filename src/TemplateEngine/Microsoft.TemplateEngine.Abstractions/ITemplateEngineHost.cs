using System;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplateEngineHost
    {
        IPhysicalFileSystem FileSystem { get; }

        string Locale { get; }

        string HostIdentifier { get; }

        Version Version { get; }

        void LogMessage(string message);

        void OnCriticalError(string code, string message, string currentFile, long currentPosition);

        bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition);

        // return of true means a new value was provided
        bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue);

        void OnSymbolUsed(string symbol, object value);

        void OnTimingCompleted(string label, TimeSpan timing);

        bool TryGetHostParamDefault(string paramName, out string value);
    }
}
