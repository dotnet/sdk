// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper
{
    /// <summary>
    /// An instance of this exception is thrown when hostfxr fails to be loaded
    /// by the native bundler due to problems finding its path.
    /// </summary>
#if INTERNALIZE_SHARED_TYPES
    internal
#else
    public
#endif
    class HostFxrResolutionException : Exception
    {
        internal HostFxrResolutionException()
            : base()
        {
        }

        internal HostFxrResolutionException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// The path specified by HOSTFXR_PATH points to a file which could not be loaded.
    /// </summary>
#if INTERNALIZE_SHARED_TYPES
    internal
#else
    public
#endif
    sealed class HostFxrNotFoundException : HostFxrResolutionException
    {
        public HostFxrNotFoundException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Runtime property HOSTFXR_PATH was not set or empty. This property should have been
    /// set by the muxer. 
    /// </summary>
#if INTERNALIZE_SHARED_TYPES
    internal
#else
    public
#endif
    sealed class HostFxrRuntimePropertyNotSetException : HostFxrResolutionException
    {
        public HostFxrRuntimePropertyNotSetException()
            : base()
        {
        }
    }
}
