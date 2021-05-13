// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.TemplateEngine.Edge.Template
{
    /// <summary>
    /// Status of the template instantiation.
    /// </summary>
    public enum CreationResultStatus
    {
        /// <summary>
        /// The template was instantiated successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The template instantiation failed.
        /// </summary>
        CreateFailed = unchecked((int)0x80020009),

        /// <summary>
        /// The mandatory parameters for template are missing.
        /// </summary>
        MissingMandatoryParam = unchecked((int)0x8002000F),

        /// <summary>
        /// The values passed for template parameters are invalid.
        /// </summary>
        InvalidParamValues = unchecked((int)0x80020005),

        [Obsolete("not used.")]
        OperationNotSpecified = unchecked((int)0x8002000E),

        /// <summary>
        /// The template is not found.
        /// </summary>
        NotFound = unchecked((int)0x800200006),

        /// <summary>
        /// The operation is cancelled.
        /// </summary>
        Cancelled = unchecked((int)0x80004004)
        
        
        /// <summary>
        /// The operation is cancelled due to destructive changes to existing files are detected.
        /// </summary>
        DestructiveChangesDetected = unchecked((int)0x80004005)
    }
}
