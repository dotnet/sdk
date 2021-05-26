// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.Exceptions
{
    /// <summary>
    /// Represents an exception thrown to indicate that a json element with the given name
    /// was not found, even though it was expected to exist under the given parent json element.
    /// </summary>
    internal sealed class JsonMemberMissingException : Exception
    {
        /// <summary>
        /// Creates an instance of <see cref="JsonMemberMissingException"/>.
        /// </summary>
        /// <param name="owningElementName">Name of the Json element that was expected to contain
        /// a member with the given name.</param>
        /// <param name="memberName">Name of the missing member.</param>
        public JsonMemberMissingException(string owningElementName, string memberName)
            : base(string.Format(LocalizableStrings.stringExtractor_log_jsonMemberIsMissing, owningElementName, memberName))
        {
            OwningElementName = owningElementName;
            MemberName = memberName;
        }

        /// <summary>
        /// Gets the name of the json element that was expected to contain the missing member.
        /// </summary>
        public string OwningElementName { get; }

        /// <summary>
        /// Gets the name of the member that was missing.
        /// </summary>
        public string MemberName { get; }
    }
}
