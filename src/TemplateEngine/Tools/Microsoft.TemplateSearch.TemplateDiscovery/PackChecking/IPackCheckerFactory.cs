// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    internal interface IPackCheckerFactory
    {
        internal Task<PackSourceChecker> CreatePackSourceCheckerAsync(CommandArgs config, CancellationToken cancellationToken);
    }
}
