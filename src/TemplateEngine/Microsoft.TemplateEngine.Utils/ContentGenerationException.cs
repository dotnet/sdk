using System;

namespace Microsoft.TemplateEngine.Utils
{
    public class ContentGenerationException : Exception
    {
        public ContentGenerationException(string message)
            : base(message)
        {            
        }

        public ContentGenerationException(string message, Exception innerException)
            : base(message, innerException)
        {            
        }
    }
}
