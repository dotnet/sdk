// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper
{
    /// <summary>
    /// An instance of this exception is thrown when hostfxr fails to be loaded
    /// by the native bundler due to problems finding its path.
    /// </summary>
    public class HostFxrResolutionException : Exception
    {
        internal HostFxrResolutionException()
            : base()
        {
        }

        internal HostFxrResolutionException(string message)
            : base(message)
        {
        }

        public HostFxrResolutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// The path specified by HOSTFXR_PATH points to a file which could not be loaded.
    /// </summary>
    public sealed class HostFxrNotFoundException : HostFxrResolutionException
    {
        public HostFxrNotFoundException()
            : base()
        {
        }

        public HostFxrNotFoundException(string message)
            : base(message)
        {
        }

        public HostFxrNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Runtime property HOSTFXR_PATH was not set or empty. This property should have been
    /// set by the muxer. 
    /// </summary>
    public sealed class HostFxrRuntimePropertyNotSetException : HostFxrResolutionException
    {
        public HostFxrRuntimePropertyNotSetException()
            : base()
        {
        }

        public HostFxrRuntimePropertyNotSetException(string message)
            : base(message)
        {
        }

        public HostFxrRuntimePropertyNotSetException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
