using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockCreationResult : ICreationResult
    {
        public IReadOnlyList<IPostAction> PostActions { get; set; }

        public IReadOnlyList<ICreationPath> PrimaryOutputs { get; set; }
    }
}
