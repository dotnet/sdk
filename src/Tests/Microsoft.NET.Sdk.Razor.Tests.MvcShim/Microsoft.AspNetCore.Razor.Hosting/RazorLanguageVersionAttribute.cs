// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.Hosting
{
    /// <summary>
    /// Specifies the name of a Razor configuration as defined by the Razor SDK.
    /// </summary>
    /// <remarks>
    /// This attribute is part of a set of metadata attributes that can be applied to an assembly at build
    /// time by the Razor SDK. These attributes allow the Razor configuration to be loaded at runtime based 
    /// on the settings originally provided by the project file.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class RazorLanguageVersionAttribute : Attribute
    {
        /// <summary>
        /// Creates a new instance of <see cref="RazorLanguageVersionAttribute"/>.
        /// </summary>
        /// <param name="languageVersion">The language version of Razor</param>
        public RazorLanguageVersionAttribute(string languageVersion)
        {
            if (languageVersion == null)
            {
                throw new ArgumentNullException(nameof(languageVersion));
            }

            LanguageVersion = languageVersion;
        }

        /// <summary>
        /// Gets the Razor language version.
        /// </summary>
        public string LanguageVersion { get; }
    }
}