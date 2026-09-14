// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.Exceptions
{
    /// <summary>
    /// Exception that is thrown when the template.json file contains elements
    /// whose identifiers are not unique.
    /// </summary>
    internal class LocalizationKeyIsNotUniqueException : TemplateLocalizerException
    {
        /// <summary>
        /// Creates an instance of <see cref="LocalizationKeyIsNotUniqueException"/>.
        /// </summary>
        /// <param name="reusedKey">The localization key that should have been unique, but was used more than once.</param>
        /// <param name="owningElementIdentifier">Identifier of the JSON element containing the reused key.</param>
        public LocalizationKeyIsNotUniqueException(string reusedKey, string owningElementIdentifier)
            : base(string.Format(LocalizableStrings.stringExtractor_log_jsonKeyIsNotUnique, owningElementIdentifier, reusedKey))
        {
            ReusedKey = reusedKey;
            OwningElementIdentifier = owningElementIdentifier;
        }

        /// <summary>
        /// The localization key that should have been unique, but was used more than once.
        /// </summary>
        /// <example>"myManualInstruction".</example>
        /// <example>"firstSymbol".</example>
        public string ReusedKey { get; }

        /// <summary>
        /// Identifier of the JSON element containing the reused key.
        /// </summary>
        /// <example>"postActions/pa0/manualInstructions".</example>
        /// <example>"symbols".</example>
        public string OwningElementIdentifier { get; }
    }
}
