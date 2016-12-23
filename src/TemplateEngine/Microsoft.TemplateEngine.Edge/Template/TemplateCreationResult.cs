using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public enum CreationResultStatus
    {
        CreateSucceeded,
        CreateFailed,
        MissingMandatoryParam,
        InvalidParamValues
    }

    public class TemplateCreationResult
    {
        public TemplateCreationResult(int resultCode, string message, CreationResultStatus status, string templateFullName, ICreationResult creationOutputs = null)
        {
            ResultCode = resultCode;
            Message = message;
            Status = status;
            TemplateFullName = templateFullName;
            PostActions = creationOutputs.PostActions;
            PrimaryOutputs = creationOutputs.PrimaryOutputs;
        }

        public int ResultCode { get; }

        public string Message { get; }

        public CreationResultStatus Status { get; }

        public string TemplateFullName { get; }

        public IReadOnlyList<IPostAction> PostActions { get; }

        public IReadOnlyList<ICreationPath> PrimaryOutputs { get; }
    }
}
