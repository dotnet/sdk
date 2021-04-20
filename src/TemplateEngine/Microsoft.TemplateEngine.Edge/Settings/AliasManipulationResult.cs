// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class AliasManipulationResult
    {
        public AliasManipulationResult(AliasManipulationStatus status)
            :this(status, null, null)
        {
        }

        public AliasManipulationResult(AliasManipulationStatus status, string aliasName, IReadOnlyList<string> aliasTokens)
        {
            Status = status;
            AliasName = aliasName;
            AliasTokens = aliasTokens;
        }

        public AliasManipulationStatus Status { get; }
        public string AliasName { get; }
        public IReadOnlyList<string> AliasTokens { get; }
    }
}
