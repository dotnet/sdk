using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IVersionSpecification
    {
        bool CheckIfVersionIsValid(string versionToCheck);
    }
}
