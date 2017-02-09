using System;

namespace Microsoft.TemplateEngine.Utils
{
    public class TemplateAuthoringException : Exception
    {
        public TemplateAuthoringException(string message, string configItem)
            : base(message)
        {
            ConfigItem = configItem;
        }

        public TemplateAuthoringException(string message, string configItem, Exception innerException)
            : base(message, innerException)
        {
            ConfigItem = configItem;
        }

        public string ConfigItem { get; }
    }
}
