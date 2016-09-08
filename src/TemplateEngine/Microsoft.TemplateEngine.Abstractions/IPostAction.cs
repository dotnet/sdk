using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IPostAction
    {
        string Description { get; }

        Guid ActionId { get; }

        bool ContinueOnError { get; }

        IReadOnlyDictionary<string, string> Args { get; }

        string ManualInstructions { get; }

        string ConfigFile { get; }
    }
}
