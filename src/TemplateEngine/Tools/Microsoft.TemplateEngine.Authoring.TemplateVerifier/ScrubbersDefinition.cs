// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier;

public class ScrubbersDefinition
{
    public static readonly ScrubbersDefinition Empty = new();

    public ScrubbersDefinition(Action<StringBuilder> scrubber, string? extension = null)
    {
        AddScrubber(scrubber, extension);
    }

    private ScrubbersDefinition() { }

    public delegate void ScrubFileByPath(string relativeFilePath, StringBuilder content);

    internal Dictionary<string, Action<StringBuilder>> ScrubersByExtension { get; private set; } = new Dictionary<string, Action<StringBuilder>>();

    internal Action<StringBuilder>? GeneralScrubber { get; private set; }

    internal List<ScrubFileByPath> ByPathScrubbers { get; private set; } = new List<ScrubFileByPath>();

    public ScrubbersDefinition AddScrubber(Action<StringBuilder> scrubber, string? extension = null)
    {
        if (ReferenceEquals(this, Empty))
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
            extension = extension.Trim();
            if (extension.StartsWith('.'))
            {
                throw new TemplateVerificationException(LocalizableStrings.VerificationEngine_Error_ScrubberExtension, TemplateVerificationErrorCode.InvalidOption);
            }

            if (ScrubersByExtension.TryGetValue(extension, out var origScrubber))
            {
                scrubber = origScrubber + scrubber;
            }

            ScrubersByExtension[extension] = scrubber;
        }

        return this;
    }

    public ScrubbersDefinition AddScrubber(ScrubFileByPath fileScrubber)
    {
        if (ReferenceEquals(this, Empty))
        {
            return new ScrubbersDefinition().AddScrubber(fileScrubber);
        }

        ByPathScrubbers.Add(fileScrubber);
        return this;
    }
}
