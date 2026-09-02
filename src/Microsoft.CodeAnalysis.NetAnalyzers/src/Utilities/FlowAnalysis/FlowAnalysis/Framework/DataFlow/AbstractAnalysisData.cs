// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow
{
    public abstract class AbstractAnalysisData : IDisposable
    {
        public bool IsDisposed { get; private set; }

        protected virtual void Dispose(bool disposing)
        {
            IsDisposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
