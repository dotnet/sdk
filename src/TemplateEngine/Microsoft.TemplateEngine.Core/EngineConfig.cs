using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class EngineConfig : IEngineConfig
    {
        public static IReadOnlyList<string> DefaultLineEndings = new[] {"\r", "\n", "\r\n"};

        public static IReadOnlyList<string> DefaultWhitespaces = new[] {" ", "\t"};

        public EngineConfig(IVariableCollection variables, string variableFormatString = "{0}")
            : this(DefaultWhitespaces, DefaultLineEndings, variables, variableFormatString)
        {
        }

        public EngineConfig(IReadOnlyList<string> whitespaces, IReadOnlyList<string> lineEndings, IVariableCollection variables, string variableFormatString = "{0}")
        {
            Whitespaces = whitespaces;
            LineEndings = lineEndings;
            Variables = variables;
            VariableFormatString = variableFormatString;
            Flags = new Dictionary<string, bool>();
        }

        public IReadOnlyList<string> LineEndings { get; }

        public string VariableFormatString { get; }

        public IVariableCollection Variables { get; }

        public IReadOnlyList<string> Whitespaces { get; }

        public IDictionary<string, bool> Flags { get; }
    }
}
