// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;

namespace Aspire.Tools.Service;

internal static class ImmutableInterlockedExtensions
{
    extension(ImmutableInterlocked)
    {
        public static (T oldValue, T newValue) Transform<T>(ref T location, Func<T, T> transformer) where T : class?
        {
            T oldValue = Volatile.Read(ref location);
            while (true)
            {
                T newValue = transformer(oldValue);
                if (ReferenceEquals(oldValue, newValue))
                {
                    // No change was actually required.
                    return (oldValue, newValue);
                }

                T interlockedResult = Interlocked.CompareExchange(ref location, newValue, oldValue);
                if (ReferenceEquals(oldValue, interlockedResult))
                {
                    return (oldValue, newValue);
                }

                oldValue = interlockedResult; // we already have a volatile read that we can reuse for the next loop
            }
        }
    }
}
