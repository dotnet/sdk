using System;
using System.Text;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface ITokenConfig : IEquatable<ITokenConfig>
    {
        string After { get; }

        string Before { get; }

        string Value { get; }

        IToken ToToken(Encoding encoding);
    }
}
