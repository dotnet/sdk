// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Utils
{
    public static class ArrayExtensions
    {
        public static T[] CombineArrays<T>(params T[][] arrayList)
        {
            int combinedLength = 0;
            foreach (T[] arg in arrayList)
            {
                combinedLength += arg.Length;
            }

            T[] combinedArray = new T[combinedLength];
            int nextIndex = 0;
            foreach (T[] arg in arrayList)
            {
                Array.Copy(arg, 0, combinedArray, nextIndex, arg.Length);
                nextIndex += arg.Length;
            }

            return combinedArray;
        }
    }
}
