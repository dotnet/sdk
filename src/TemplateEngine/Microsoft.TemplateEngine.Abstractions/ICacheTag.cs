using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ICacheTag
    {
        string Description { get; set; }

        IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; set; }

        string DefaultValue { get; set; }
    }
}
