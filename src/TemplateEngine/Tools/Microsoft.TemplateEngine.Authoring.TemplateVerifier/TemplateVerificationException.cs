// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier
{
    [Serializable]
    public class TemplateVerificationException : Exception
    {
        public TemplateVerificationException(string message, TemplateVerificationErrorCode templateVerificationErrorCode) : base(message)
        {
            TemplateVerificationErrorCode = templateVerificationErrorCode;
        }

        public TemplateVerificationException(string message, TemplateVerificationErrorCode templateVerificationErrorCode, Exception inner) : base(message, inner)
        {
            TemplateVerificationErrorCode = templateVerificationErrorCode;
        }

        protected TemplateVerificationException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }

        public TemplateVerificationErrorCode TemplateVerificationErrorCode { get; init; }
    }
}
