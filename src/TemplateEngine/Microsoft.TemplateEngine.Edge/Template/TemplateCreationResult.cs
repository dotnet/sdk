using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public enum CreationResultStatus
    {
        CreateSucceeded,
        CreateFailed,
        InstallSucceeded,
        InstallFailed,
        AliasSucceeded,
        AliasFailed,
        MissingMandatoryParam,
        TemplateNotFound
    }

    public class TemplateCreationResult
    {
        public TemplateCreationResult(int resultCode, string message, CreationResultStatus status, string templateFullName)
        {
            ResultCode = resultCode;
            Message = message;
            Status = status;
            TemplateFullName = templateFullName;
        }

        public int ResultCode { get; private set; }

        public string Message { get; private set; }

        public CreationResultStatus Status { get; private set; }

        public string TemplateFullName { get; private set; }
    }
}
