// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class TemplateValidationException : Exception
    {
        internal TemplateValidationException(string message) : base(message) { }
    }
}
