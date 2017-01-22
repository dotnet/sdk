using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface ICustomFileGlobModel : IConditionedConfigurationElement
    {
        string Glob { get; }

        IReadOnlyList<ICustomOperationModel> Operations { get; }

        IVariableConfig VariableFormat { get; }

        string FlagPrefix { get; }
    }
}
