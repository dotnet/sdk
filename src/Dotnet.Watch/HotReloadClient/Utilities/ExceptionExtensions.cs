// Copyright (c) Microsoft Corporation. All rights reserved.

#if NETFRAMEWORK

using System.Diagnostics.CodeAnalysis;

namespace System;

internal static class ExceptionExtensions
{
    extension(ObjectDisposedException)
    {
        public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
        {
            if (condition)
            {
                throw new ObjectDisposedException(instance?.GetType().FullName);
            }
        }
    }
}

#endif
