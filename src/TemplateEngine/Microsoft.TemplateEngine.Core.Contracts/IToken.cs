// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IToken
    {
        /// <summary>
        /// The value to match.
        /// </summary>
        byte[] Value { get; }

        /// <summary>
        /// Start of actual token. May be not 0, when look arounds are used.
        /// </summary>
        int Start { get; }

        /// <summary>
        /// Start of actual token. May be not Value.Length, when look arounds are used.
        /// </summary>
        int End { get; }

        /// <summary>
        /// The length of the actual token.
        /// </summary>
        int Length { get; }
    }
}
