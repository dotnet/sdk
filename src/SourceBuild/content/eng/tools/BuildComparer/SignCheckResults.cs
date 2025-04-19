using System;
using System.Collections.Generic;
using System.Xml.Serialization;

[XmlRoot("SignCheckResults")]
public class SignCheckResults
{
    [XmlElement("File")]
    public List<SignCheckResultFile> Files { get; set; }
}

public class SignCheckResultFile
{
    [XmlAttribute("Name")]
    public string Name { get; set; }

    [XmlAttribute("Outcome")]
    public SignCheckOutcome Outcome { get; set; }

    [XmlElement("File")]
    public List<SignCheckResultFile> NestedFiles { get; set; }

    public string NormalizedName => Name.RemoveVersionsNormalized();
}

public enum SignCheckOutcome
{
    [XmlEnum("Signed")]
    Signed,

    [XmlEnum("Unsigned")]
    Unsigned,

    [XmlEnum("Skipped")]
    Skipped,

    [XmlEnum("Excluded")]
    Excluded,

    [XmlEnum("SkippedExcluded")]
    SkippedExcluded,

    [XmlEnum("Unknown")]
    Unknown,
}
