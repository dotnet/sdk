// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Utils
{
    public class ContentGenerationException : Exception
    {
        public ContentGenerationException(string message)
            : base(message)
        {            
        }

        public ContentGenerationException(string message, Exception innerException)
            : base(message, innerException)
        {            
        }
    }
}
