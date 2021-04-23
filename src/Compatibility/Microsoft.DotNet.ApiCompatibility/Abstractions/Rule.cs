// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Base class for Rules to use in order to be discovered and invoked by the <see cref="IRuleRunner"/>
    /// </summary>
    public abstract class Rule
    {
        public abstract void Initialize(RuleRunnerContext context);
    }
}
