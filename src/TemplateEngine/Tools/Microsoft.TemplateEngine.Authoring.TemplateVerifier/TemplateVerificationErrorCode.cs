// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier
{
    /// <summary>
    /// VerificationEngine error codes in thrown <see cref="TemplateVerificationException"/>. Correspond to VerificationCommand exit codes.
    ///
    /// Exit codes based on
    ///  * https://tldp.org/LDP/abs/html/exitcodes.html
    ///  * https://github.com/openbsd/src/blob/master/include/sysexits.h.
    /// related reference: dotnet new exit codes: https://aka.ms/templating-exit-codes.
    /// Future exit codes should be allocated in a range of 107 - 113. If not sufficient, a range of 79 - 99 may be used as well.
    /// </summary>
    public enum TemplateVerificationErrorCode
    {
        /// <summary>
        /// Indicates failed verification - assertions defined for the scenarios were not met.
        /// E.g. unexpected exit code, stdout/stderr output or created templates content.
        /// </summary>
        VerificationFailed = 65,

        /// <summary>
        /// Unexpected internal error in <see cref="VerificationEngine"/>. This might indicate a bug.
        /// </summary>
        InternalError = 70,

        /// <summary>
        /// Configured working directory already exists and is not empty - so instantiation cannot proceed without destructive changes.
        /// </summary>
        WorkingDirectoryExists = 73,

        /// <summary>
        /// Selected template (via name or path) was not found.
        /// </summary>
        TemplateDoesNotExist = 103,

        /// <summary>
        /// The template instantiation failed and results were not created.
        /// </summary>
        InstantiationFailed = 100,

        /// <summary>
        /// Installation/Uninstallation Failed - Processing issues.
        /// </summary>
        InstallFailed = 106,

        /// <summary>
        /// Unrecognized option(s) and/or argument(s) for a command.
        /// </summary>
        InvalidOption = 127,
    }
}
