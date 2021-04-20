// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public class TemplateCreationResult
    {
        public TemplateCreationResult(string message, CreationResultStatus status, string templateFullName)
            :this(message, status, templateFullName, null, null, null)
        { }

        public TemplateCreationResult(string message, CreationResultStatus status, string templateFullName, ICreationResult creationOutputs, string outputBaseDir, ICreationEffects creationEffects)
        {
            Message = message;
            Status = status;
            TemplateFullName = templateFullName;
            ResultInfo = creationOutputs;
            OutputBaseDirectory = outputBaseDir;
            CreationEffects = creationEffects;
        }

        public string Message { get; }

        public CreationResultStatus Status { get; }

        public string TemplateFullName { get; }

        public ICreationResult ResultInfo { get; }

        public string OutputBaseDirectory { get; }

        public ICreationEffects CreationEffects { get; }
    }
}
