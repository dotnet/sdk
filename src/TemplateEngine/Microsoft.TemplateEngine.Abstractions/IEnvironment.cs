using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IEnvironment
    {
        string ExpandEnvironmentVariables(string name);

        string GetEnvironmentVariable(string name);

        IReadOnlyDictionary<string, string> GetEnvironmentVariables();

        string NewLine { get; }
    }
}
