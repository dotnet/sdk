// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
