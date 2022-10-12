// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier;

public class ScrubbersDefinition
{
    public static readonly ScrubbersDefinition Empty = new();

    public ScrubbersDefinition() { }

    public ScrubbersDefinition(Action<StringBuilder> scrubber, string? extension = null)
    {
        AddScrubber(scrubber, extension);
    }

    public Dictionary<string, Action<StringBuilder>> ScrubersByExtension { get; private set; } = new Dictionary<string, Action<StringBuilder>>();

    public Action<StringBuilder>? GeneralScrubber { get; private set; }

    public ScrubbersDefinition AddScrubber(Action<StringBuilder> scrubber, string? extension = null)
    {
        if (object.ReferenceEquals(this, Empty))
        {
            return new ScrubbersDefinition().AddScrubber(scrubber, extension);
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            GeneralScrubber += scrubber;
        }
        // This is to get the same behavior as Verify.NET
        else
        {
            ScrubersByExtension[extension.Trim()] = extension.Trim().StartsWith('.')
                ? throw new TemplateVerificationException(LocalizableStrings.VerificationEngine_Error_ScrubberExtension, TemplateVerificationErrorCode.InvalidOption)
                : scrubber;
        }

        return this;
    }
}
