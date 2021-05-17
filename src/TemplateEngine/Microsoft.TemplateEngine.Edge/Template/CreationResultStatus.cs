// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Edge.Template
{
    public enum CreationResultStatus
    {
        Success = 0,
        CreateFailed = unchecked((int)0x80020009),
        MissingMandatoryParam = unchecked((int)0x8002000F),
        InvalidParamValues = unchecked((int)0x80020005),
        OperationNotSpecified = unchecked((int)0x8002000E),
        NotFound = unchecked((int)0x800200006),
        Cancelled = unchecked((int)0x80004004),
        DestructiveChangesDetected = unchecked((int)0x80004005)
    }
}
