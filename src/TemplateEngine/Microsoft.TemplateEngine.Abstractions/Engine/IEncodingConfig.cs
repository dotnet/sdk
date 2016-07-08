using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Abstractions.Engine
{
    public interface IEncodingConfig
    {
        Encoding Encoding { get; }

        ITokenTrie LineEndings { get; }

        IReadOnlyList<byte[]> VariableKeys { get; }

        IReadOnlyList<Func<object>> VariableValues { get; }

        ITokenTrie Variables { get; }

        ITokenTrie Whitespace { get; }

        ITokenTrie WhitespaceOrLineEnding { get; }

        object this[int index] { get; }
    }
}
