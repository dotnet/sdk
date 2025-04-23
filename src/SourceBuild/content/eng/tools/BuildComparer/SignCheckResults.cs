using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Runtime.Serialization;

[XmlRoot("SignCheckResults")]
public class SignCheckResults
{
    [XmlElement("File")]
    public List<SignCheckResultFile> Files { get; set; }
}

public class SignCheckResultFile
{
    private string _rawName;
    private string _normalizedName;

    [XmlAttribute("Name")]
    public string Name { 
        get => _rawName;
        set
        {
            _rawName = value;
            _normalizedName = value.RemoveVersionsNormalized();
        }
    }

    [XmlAttribute("Outcome")]
    public SignCheckOutcome Outcome { get; set; }

    [XmlElement("File")]
    public List<SignCheckResultFile> NestedFiles { get; set; }

    [XmlIgnore]
    public string NormalizedName => _normalizedName;
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
