// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using System.Collections.Generic;

    internal static class Namespaces
    {
        internal static readonly IReadOnlyList<string> System = new[]
        {
            nameof(System),
        };

        internal static readonly IReadOnlyList<string> SystemCollectionsGeneric = new[]
        {
            nameof(System),
            nameof(global::System.Collections),
            nameof(global::System.Collections.Generic),
        };

        internal static readonly IReadOnlyList<string> SystemThreadingTasks = new[]
        {
            nameof(System),
            nameof(global::System.Threading),
            nameof(global::System.Threading.Tasks),
        };

        internal static readonly IReadOnlyList<string> SystemRuntimeCompilerServices = new[]
        {
            nameof(System),
            nameof(global::System.Runtime),
            nameof(global::System.Runtime.CompilerServices),
        };
    }
}
