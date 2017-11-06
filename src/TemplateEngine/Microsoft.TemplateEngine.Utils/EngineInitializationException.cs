using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Utils
{
    public class EngineInitializationException : Exception
    {
        public EngineInitializationException(string message, string settingsItem)
            : base(message)
        {
            SettingsItem = settingsItem;
        }

        public EngineInitializationException(string message, string settingsItem, Exception innerException)
            : base(message, innerException)
        {
            SettingsItem = settingsItem;
        }

        public string SettingsItem { get; }
    }
}
