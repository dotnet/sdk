// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class InvalidNuGetSourceException : Exception
    {
        public InvalidNuGetSourceException(string message) : base(message) { }

        public InvalidNuGetSourceException(string message, IEnumerable<string> sources)
            : base(message + ", attempted sources: " + string.Join(", ", sources) + ".")
        {
            SourcesList = sources;
        }

        public InvalidNuGetSourceException(string message, Exception inner) : base(message, inner) { }

        public InvalidNuGetSourceException(string message, IEnumerable<string> sources, Exception inner)
            : base(message + ", attempted sources: " + string.Join(", ", sources) + ".", inner)
        {
            SourcesList = sources;
        }

        public IEnumerable<string> SourcesList { get; private set; }
    }
}
