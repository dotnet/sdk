// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal interface IWatchFilter
    {
        ValueTask ProcessAsync(DotNetWatchContext context, CancellationToken cancellationToken);
    }
}
