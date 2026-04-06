#!/usr/bin/env dotnet
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
string skillName = Path.GetFileName(skillDir);
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

int endIndex = text.IndexOf("---", 3);
if (endIndex < 0)
{
    Console.Error.WriteLine("Unterminated YAML frontmatter.");
    return 1;
}

string yaml = text.Substring(3, endIndex - 3).Trim();

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
if (description.Length > 1024)
{
    Console.Error.WriteLine($"Description is {description.Length} chars (max 1024).");
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

Console.WriteLine($"Skill '{frontmatterName}' is valid.");
return 0;
