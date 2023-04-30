// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal interface IRequester
    {
        Task<ConsoleKey> GetKeyAsync(string prompt, Func<ConsoleKey, bool> validateInput, CancellationToken cancellationToken);
    }
}
