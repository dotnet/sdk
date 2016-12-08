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
        public string Message { get; set; }
        public CreationResultStatus Status { get; set; }
        public string TemplateFullName { get; set; }
    }
}
