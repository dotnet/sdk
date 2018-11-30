using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplateWithTimestamp
    {
        DateTime? ConfigTimestampUtc { get; set; }
    }
}
