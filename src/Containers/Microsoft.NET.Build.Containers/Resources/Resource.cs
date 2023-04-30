// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Microsoft.NET.Build.Containers.Resources
{
    /// <summary>
    /// This class provides access to the assembly's resources.
    /// </summary>
    /// <remarks>
    /// Codes used for warnings/errors:
    /// CONTAINER1xxx: HTTP or local daemon related failures
    /// CONTAINER2xxx: Invalid/missing data related failures
    /// CONTAINER3xxx: Docker process related failures
    /// CONTAINER4xxx: Invalid command line parameters
    /// CONTAINER9000: Unhanled exception
    /// </remarks>
    internal static class Resource
    {
        internal static readonly ResourceManager Manager = new ResourceManager(typeof(Strings).FullName!, typeof(Resource).GetTypeInfo().Assembly);

        /// <summary>
        /// Looks up a resource value for a particular name. Looks in the CurrentUICulture, and if not found, all parent CultureInfos.
        /// </summary>
        /// <param name="name">Name of the resource.</param>
        /// <returns>Localized string or resource name if the resource isn't found.</returns>
        public static string GetString(string name)
        {
            string? resource = Manager.GetString(name, CultureInfo.CurrentUICulture);

            Debug.Assert(resource != null, $"Resource with name {name} was not found");

            return resource ?? $"<{name}>";
        }

        /// <summary>
        /// Looks up a resource value for a particular name and uses it as format. Looks in the CurrentUICulture, and if not found, all parent CultureInfos.
        /// </summary>
        /// <param name="name">Name of the resource.</param>
        /// <returns>Localized formatted string or resource name if the resource isn't found.</returns>
        public static string FormatString(string name, params object?[] args)
        {
            string? resource = Manager.GetString(name, CultureInfo.CurrentUICulture);

            Debug.Assert(resource != null, $"Resource with name {name} was not found");

            return resource is null ?
                $"<{name}>" :
                string.Format(CultureInfo.CurrentCulture, resource, args);
        }
    }
}
