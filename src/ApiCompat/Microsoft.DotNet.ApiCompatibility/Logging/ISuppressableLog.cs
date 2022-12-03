// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    public interface ISuppressableLog : ILog
    {
        bool LogError(Suppression suppression, string code, string format, params string[] args);
        bool LogWarning(Suppression suppression, string code, string format, params string[] args);
        bool SuppressionWasLogged { get; }
    }
}
