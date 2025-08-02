// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.PackageValidation
{
    public static class ResourceSingleton
    {
        /// <summary>
        /// Change the embedded resources culture.
        /// </summary>s
        public static void ChangeCulture(CultureInfo culture)
        {
            Resources.Culture = culture;
            ApiCompatibility.ResourceSingleton.ChangeCulture(culture);
        }
    }
}
