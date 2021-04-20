// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Expressions
{
    public class Token<TToken>
    {
        public Token(TToken family, string value)
        {
            Family = family;
            Value = value;
        }

        public TToken Family { get; }

        public string Value { get; }
    }
}
