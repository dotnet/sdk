// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.ApiCompatibility
{
    public static class ResourceSingleton
    {
        /// <summary>
        /// Change the embedded resources culture.
        /// </summary>
        public static void ChangeCulture(CultureInfo culture) => Resources.Culture = culture;
    }
}
