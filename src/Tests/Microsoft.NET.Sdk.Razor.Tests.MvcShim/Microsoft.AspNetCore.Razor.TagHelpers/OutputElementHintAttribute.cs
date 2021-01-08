// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.TagHelpers
{
    /// <summary>
    /// Provides a hint of the <see cref="ITagHelper"/>'s output element.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class OutputElementHintAttribute : Attribute
    {
        /// <summary>
        /// Instantiates a new instance of the <see cref="OutputElementHintAttribute"/> class.
        /// </summary>
        /// <param name="outputElement">
        /// The HTML element the <see cref="ITagHelper"/> may output.
        /// </param>
        public OutputElementHintAttribute(string outputElement)
        {
            if (outputElement == null)
            {
                throw new ArgumentNullException(nameof(outputElement));
            }

            OutputElement = outputElement;
        }

        /// <summary>
        /// The HTML element the <see cref="ITagHelper"/> may output.
        /// </summary>
        public string OutputElement { get; }
    }
}