// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.Exceptions
{
    /// <summary>
    /// Base exception type for all the exceptions specific to Template Localizer.
    /// </summary>
    internal class TemplateLocalizerException : Exception
    {
        public TemplateLocalizerException() : base() { }

        public TemplateLocalizerException(string message) : base(message) { }

        public TemplateLocalizerException(string message, Exception innerException) : base(message, innerException) { }
    }
}
