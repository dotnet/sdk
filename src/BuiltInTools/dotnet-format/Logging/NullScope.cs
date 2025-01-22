// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    internal class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new NullScope();

        private NullScope()
        {
        }

        public void Dispose()
        {
        }
    }
}
