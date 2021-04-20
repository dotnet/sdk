// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Utils
{
    public static class Empty<T>
    {
        public static class List
        {
            // ReSharper disable once StaticMemberInGenericType
            public static readonly IReadOnlyList<T> Value = new List<T>();
        }

        public static class Array
        {
            // ReSharper disable once StaticMemberInGenericType
            public static readonly T[] Value = System.Array.Empty<T>();
        }
    }
}
