using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplateEngineHost
    {
        void LogMessage(string message);

        void OnCriticalError(string code, string message, string currentFile, long currentPosition);

        bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition);

        bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue);

        void OnSymbolUsed(string symbol, object value);

        void OnTimingCompleted(string label, TimeSpan timing);
    }
}
