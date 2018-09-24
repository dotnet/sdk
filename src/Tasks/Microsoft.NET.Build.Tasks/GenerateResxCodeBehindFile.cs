using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateResxCodeBehindFile : TaskBase
    {
        private enum Lang
        {
            CSharp,
            VisualBasic
        }

        private enum ParsedDocCommentMode
        {
            None,
            Content,
            ResxComment
        }

        private const int maxDocCommentLength = 256;

        [Required]
        public string Language { get; set; }

        [Required]
        public string ResourceFile { get; set; }

        [Required]
        public string ResourceName { get; set; }

        [Required]
        public string DocCommentMode { get; set; }

        [Required]
        public string OutputPath { get; set; }

        protected override void ExecuteCore()
        {
            // The algorithm used by this class is borrowed from:
            // https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Arcade.Sdk/src/GenerateResxSource.cs

            if (!Enum.TryParse<ParsedDocCommentMode>(DocCommentMode, out var commentMode))
            {
                throw new BuildErrorException($"Invalid DocCommentMode '{DocCommentMode}'");
            }

            string namespaceName;
            string className;

            if (string.IsNullOrEmpty(ResourceName))
            {
                throw new BuildErrorException("ResourceName not specified");
            }

            string[] nameParts = ResourceName.Split('.');
            if (nameParts.Length == 1)
            {
                namespaceName = null;
                className = nameParts[0];
            }
            else
            {
                namespaceName = string.Join(".", nameParts, 0, nameParts.Length - 1);
                className = nameParts.Last();
            }

            string docCommentStart;
            Lang language;
            switch (Language.ToUpperInvariant())
            {
                case "C#":
                    language = Lang.CSharp;
                    docCommentStart = "///";
                    break;

                case "VB":
                    language = Lang.VisualBasic;
                    docCommentStart = "'''";
                    break;

                default:
                    throw new BuildErrorException($"GenerateResxSource doesn't support language: '{Language}'");
            }

            string classIndent = (namespaceName == null ? "" : "    ");
            string memberIndent = classIndent + "    ";

            var strings = new StringBuilder();
            foreach (var node in XDocument.Load(ResourceFile).Descendants("data"))
            {
                string name = node.Attribute("name")?.Value;
                if (name == null)
                {
                    throw new BuildErrorException("Missing resource name");
                }

                if (commentMode != ParsedDocCommentMode.None)
                {
                    string comment;
                    if (commentMode == ParsedDocCommentMode.Content)
                    {
                        comment = node.Elements("value").FirstOrDefault()?.Value.Trim();
                        if (comment == null)
                        {
                            throw new BuildErrorException($"Missing resource value: '{name}'");
                        }
                    }
                    else
                    {
                        comment = node.Elements("comment").FirstOrDefault()?.Value.Trim();
                        if (comment == null)
                            comment = "";
                    }

                    if (name == "")
                    {
                        throw new BuildErrorException($"Empty resource name");
                    }

                    if (comment.Length > 0)
                    {
                        if (comment.Length > maxDocCommentLength)
                        {
                            comment = comment.Substring(0, maxDocCommentLength) + " ...";
                        }

                        string escapedTrimmedValue = new XElement("summary", comment).ToString();

                        foreach (var line in escapedTrimmedValue.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                        {
                            strings.Append($"{memberIndent}{docCommentStart} ");
                            strings.AppendLine(line);
                        }
                    }
                }

                string identifier = IsLetterChar(CharUnicodeInfo.GetUnicodeCategory(name[0])) ? name : "_" + name;

                switch (language)
                {
                    case Lang.CSharp:
                        strings.AppendLine($"{memberIndent}internal static string {identifier} => ResourceManager.GetString(\"{name}\", Culture);");
                        break;

                    case Lang.VisualBasic:
                        strings.AppendLine($"{memberIndent}Friend Shared ReadOnly Property {identifier} As String");
                        strings.AppendLine($"{memberIndent}    Get");
                        strings.AppendLine($"{memberIndent}        Return ResourceManager.GetString(\"{name}\", Culture)");
                        strings.AppendLine($"{memberIndent}    End Get");
                        strings.AppendLine($"{memberIndent}End Property");
                        break;

                    default:
                        throw new InvalidOperationException();
                }
            }

            string namespaceStart, namespaceEnd;
            if (namespaceName == null)
            {
                namespaceStart = namespaceEnd = null;
            }
            else
            {
                switch (language)
                {
                    case Lang.CSharp:
                        namespaceStart = $@"namespace {namespaceName}{Environment.NewLine}{{";
                        namespaceEnd = "}";
                        break;

                    case Lang.VisualBasic:
                        namespaceStart = $"Namespace {namespaceName}";
                        namespaceEnd = "End Namespace";
                        break;

                    default:
                        throw new InvalidOperationException();
                }
            }

            string result;
            switch (language)
            {
                case Lang.CSharp:
                    result = $@"// <auto-generated>
using System.Reflection;

{namespaceStart}
{classIndent}internal static class {className}
{classIndent}{{
{memberIndent}internal static global::System.Globalization.CultureInfo Culture {{ get; set; }}
{memberIndent}internal static global::System.Resources.ResourceManager ResourceManager {{ get; }} = new global::System.Resources.ResourceManager(""{ResourceName}"", typeof({className}).GetTypeInfo().Assembly);

{strings}
{classIndent}}}
{namespaceEnd}
";
                    break;

                case Lang.VisualBasic:
                    result = $@"' <auto-generated>
Imports System.Reflection

{namespaceStart}
{classIndent}Friend Class {className}
{memberIndent}Private Sub New
{memberIndent}End Sub
{memberIndent}
{memberIndent}Friend Shared Property Culture As Global.System.Globalization.CultureInfo
{memberIndent}Friend Shared ReadOnly Property ResourceManager As New Global.System.Resources.ResourceManager(""{ResourceName}"", GetType({className}).GetTypeInfo().Assembly)

{strings}
{classIndent}End Class
{namespaceEnd}
";
                    break;

                default:
                    throw new InvalidOperationException();
            }

            File.WriteAllText(OutputPath, result);
        }

        private bool IsLetterChar(UnicodeCategory cat)
        {
            // letter-character:
            //   A Unicode character of classes Lu, Ll, Lt, Lm, Lo, or Nl 
            //   A Unicode-escape-sequence representing a character of classes Lu, Ll, Lt, Lm, Lo, or Nl

            switch (cat)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                    return true;
            }

            return false;
        }
    }
}
