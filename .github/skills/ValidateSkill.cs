#!/usr/bin/env dotnet

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:package YamlDotNet@16.3.0

using YamlDotNet.Serialization;
using System.Text.RegularExpressions;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet ValidateSkill.cs <path-to-skill-directory>");
    return 1;
}

string skillDir = Path.GetFullPath(args[0]);
string skillName = Path.GetFileName(Path.TrimEndingDirectorySeparator(skillDir));
string skillFile = Path.Combine(skillDir, "SKILL.md");

// SKILL.md must exist in the skill directory
if (!File.Exists(skillFile))
{
    Console.Error.WriteLine($"SKILL.md not found in {skillDir}");
    return 1;
}

string text = File.ReadAllText(skillFile);

// SKILL.md must begin with YAML frontmatter delimited by ---
if (!text.StartsWith("---"))
{
    Console.Error.WriteLine("No YAML frontmatter found.");
    return 1;
}

Match frontmatterMatch = Regex.Match(
    text,
    @"\A---\r?\n(?<yaml>.*?)(?:\r?\n)---(?:\r?\n|$)",
    RegexOptions.Singleline);
if (!frontmatterMatch.Success)
{
    Console.Error.WriteLine("Unterminated YAML frontmatter.");
    return 1;
}

string yaml = frontmatterMatch.Groups["yaml"].Value.Trim();

IDeserializer deserializer = new DeserializerBuilder().Build();
Dictionary<string, object> frontmatter = deserializer.Deserialize<Dictionary<string, object>>(yaml);

// name is required
if (!frontmatter.TryGetValue("name", out object? nameValue) || nameValue is not string frontmatterName)
{
    Console.Error.WriteLine("Frontmatter missing 'name' field.");
    return 1;
}

// name must be 1-64 characters
if (frontmatterName.Length == 0 || frontmatterName.Length > 64)
{
    Console.Error.WriteLine($"Name is {frontmatterName.Length} chars (must be 1-64).");
    return 1;
}

// name: lowercase alphanumeric and hyphens only, no leading/trailing/consecutive hyphens
if (!Regex.IsMatch(frontmatterName, @"^[a-z0-9]([a-z0-9-]*[a-z0-9])?$")
    || frontmatterName.Contains("--"))
{
    Console.Error.WriteLine($"Invalid name '{frontmatterName}'. Must be lowercase letters, numbers, and hyphens only. Must not start/end with a hyphen or contain consecutive hyphens.");
    return 1;
}

// name must match the parent directory name
if (!string.Equals(skillName, frontmatterName, StringComparison.Ordinal))
{
    Console.Error.WriteLine($"Name mismatch: directory is '{skillName}' but SKILL.md name is '{frontmatterName}'.");
    return 1;
}

// description is required
if (!frontmatter.TryGetValue("description", out object? descValue) || descValue is not string description)
{
    Console.Error.WriteLine("Frontmatter missing 'description' field.");
    return 1;
}

// description must be 1-1024 characters
if (description.Length == 0 || description.Length > 1024)
{
    Console.Error.WriteLine($"Description is {description.Length} chars (must be 1-1024).");
    return 1;
}

// Keep SKILL.md under 500 lines; move detailed content to references/ or scripts/
// See "Progressive Disclosure" at https://agentskills.io/specification.md
int lineCount = text.Split('\n').Length;
if (lineCount > 500)
{
    Console.Error.WriteLine($"SKILL.md is {lineCount} lines (max 500). See \"Progressive Disclosure\" at https://agentskills.io/specification.md");
    return 1;
}

if (!TryGetMetadataString(frontmatter, "binding", out string? binding, out string? metadataError))
{
    Console.Error.WriteLine(metadataError);
    return 1;
}

string overlayFile = Path.Combine(skillDir, "overlay.md");
bool overlayExists = File.Exists(overlayFile);

if (binding is not null && binding is not ("none" or "optional-overlay" or "required-overlay"))
{
    Console.Error.WriteLine($"Invalid metadata.binding '{binding}'. Expected none, optional-overlay, or required-overlay.");
    return 1;
}

if (overlayExists && binding is null)
{
    Console.Error.WriteLine("overlay.md requires metadata.binding in SKILL.md.");
    return 1;
}

if (binding is "optional-overlay" or "required-overlay" &&
    !text.Contains("If `overlay.md` exists beside this file, read it before acting", StringComparison.Ordinal))
{
    Console.Error.WriteLine($"metadata.binding '{binding}' requires an overlay loader instruction in SKILL.md.");
    return 1;
}

if (binding == "required-overlay" && !overlayExists)
{
    Console.Error.WriteLine("metadata.binding 'required-overlay' requires overlay.md beside SKILL.md.");
    return 1;
}

if (binding == "none" && overlayExists)
{
    Console.Error.WriteLine("overlay.md exists but metadata.binding is 'none'.");
    return 1;
}

if (overlayExists)
{
    Dictionary<string, object> overlayFrontmatter = ReadFrontmatter(overlayFile, "overlay.md", deserializer);

    if (!TryGetNonEmptyString(overlayFrontmatter, "core", out string? overlayCore))
    {
        Console.Error.WriteLine("overlay.md frontmatter missing non-empty 'core' field.");
        return 1;
    }

    if (!string.Equals(overlayCore, skillName, StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"overlay.md core '{overlayCore}' does not match skill directory '{skillName}'.");
        return 1;
    }

    if (!TryGetNonEmptyString(overlayFrontmatter, "core-pin", out _))
    {
        Console.Error.WriteLine("overlay.md frontmatter missing non-empty 'core-pin' field.");
        return 1;
    }
}

Console.WriteLine($"Skill '{frontmatterName}' is valid.");
return 0;

static bool TryGetMetadataString(
    Dictionary<string, object> frontmatter,
    string key,
    out string? value,
    out string? error)
{
    value = null;
    error = null;

    if (!frontmatter.TryGetValue("metadata", out object? metadataValue))
    {
        return true;
    }

    if (metadataValue is not IDictionary<object, object> metadata)
    {
        error = "Frontmatter field 'metadata' must be a mapping.";
        return false;
    }

    if (!metadata.TryGetValue(key, out object? metadataEntry))
    {
        return true;
    }

    if (metadataEntry is not string stringValue)
    {
        error = $"metadata.{key} must be a string.";
        return false;
    }

    value = stringValue;
    return true;
}

static Dictionary<string, object> ReadFrontmatter(string path, string displayName, IDeserializer deserializer)
{
    string text = File.ReadAllText(path);
    Match match = Regex.Match(
        text,
        @"\A---\r?\n(?<yaml>.*?)(?:\r?\n)---(?:\r?\n|$)",
        RegexOptions.Singleline);

    if (!match.Success)
    {
        throw new InvalidDataException($"{displayName} has missing or unterminated YAML frontmatter.");
    }

    return deserializer.Deserialize<Dictionary<string, object>>(match.Groups["yaml"].Value.Trim());
}

static bool TryGetNonEmptyString(Dictionary<string, object> frontmatter, string key, out string? value)
{
    if (frontmatter.TryGetValue(key, out object? rawValue) &&
        rawValue is string stringValue &&
        !string.IsNullOrWhiteSpace(stringValue))
    {
        value = stringValue;
        return true;
    }

    value = null;
    return false;
}
