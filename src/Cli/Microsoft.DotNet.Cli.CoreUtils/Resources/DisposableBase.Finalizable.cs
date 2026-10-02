// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

internal abstract partial class DisposableBase
{
    /// <summary>
    ///  A derived class of <see cref="DisposableBase"/> that includes a finalizer to ensure resources
    ///  are released when the object is garbage collected if the consumer fails to call <see cref="Dispose()"/>.
    /// </summary>
    public abstract class Finalizable : DisposableBase
    {
        /// <summary>
        ///  Finalizes an instance of the <see cref="Finalizable"/> class.
        /// </summary>
        ~Finalizable() => DisposeInternal(disposing: false);
    }
}
