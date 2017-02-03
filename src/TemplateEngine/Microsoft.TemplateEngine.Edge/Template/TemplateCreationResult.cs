using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public enum CreationResultStatus
    {
        Success = 0,
        CreateFailed = unchecked((int)0x80020009),
        MissingMandatoryParam = unchecked((int)0x8002000F),
        InvalidParamValues = unchecked((int)0x80020005),
        OperationNotSpecified = unchecked((int)0x8002000E),
        NotFound = unchecked((int)0x800200006)
    }

    public class TemplateCreationResult
    {
        public TemplateCreationResult(string message, CreationResultStatus status, string templateFullName)
            :this(message, status, templateFullName, null, null)
        { }

        public TemplateCreationResult(string message, CreationResultStatus status, string templateFullName, ICreationResult creationOutputs, string outputBaseDir)
        {
            Message = message;
            Status = status;
            TemplateFullName = templateFullName;
            ResultInfo = creationOutputs;
            OutputBaseDirectory = outputBaseDir;
        }

        public string Message { get; }

        public CreationResultStatus Status { get; }

        public string TemplateFullName { get; }

        public ICreationResult ResultInfo { get; }

        public string OutputBaseDirectory { get; }
    }
}
