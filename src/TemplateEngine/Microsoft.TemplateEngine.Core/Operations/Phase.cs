// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class Phase
    {
        public Phase(ITokenConfig match, IReadOnlyList<ITokenConfig> resetsWith)
            : this(match, null, resetsWith)
        {
        }

        public Phase(ITokenConfig match, string replacement, IReadOnlyList<ITokenConfig> resetsWith)
        {
            Match = match;
            Replacement = replacement;
            ResetsWith = resetsWith;
            Next = new List<Phase>();
        }

        public ITokenConfig Match { get; }

        public List<Phase> Next { get; }

        public string Replacement { get; }

        public IReadOnlyList<ITokenConfig> ResetsWith { get; }
    }
}
