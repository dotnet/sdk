// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create an ApiComparer instance.
    /// </summary>
    public sealed class ApiComparerFactory(IRuleFactory ruleFactory,
        ApiComparerSettings? settings = null,
        IDifferenceVisitorFactory? differenceVisitorFactory = null,
        IRuleContext? ruleContext = null,
        Func<IRuleFactory, IRuleContext, IRuleRunner>? ruleRunnerFactory = null,
        Func<IRuleRunner, IElementMapperFactory>? elementMapperFactory = null) : IApiComparerFactory
    {
        /// <inheritdoc />
        public IApiComparer Create() => new ApiComparer(ruleFactory,
            settings,
            differenceVisitorFactory,
            ruleContext,
            ruleRunnerFactory,
            elementMapperFactory);
    }
}
