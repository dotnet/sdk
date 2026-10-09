// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Parses string entries and format placeholders from RESX content.
/// </summary>
internal static class ResxParser
{
    /// <summary>
    ///  The maximum number of resource entries accepted from one file.
    /// </summary>
    internal const int MaxResourceEntries = 4096;

    /// <summary>
    ///  The maximum number of format arguments accepted from one resource value.
    /// </summary>
    internal const int MaxFormatArguments = 64;

    /// <summary>
    ///  Parses one resource input and reports invalid content through the generator context.
    /// </summary>
    /// <param name="context">The source production context used to report diagnostics.</param>
    /// <param name="input">The resource input to parse.</param>
    /// <returns>The parsed resource, or <see langword="null"/> when the input is invalid.</returns>
    internal static ParsedResource? Parse(SourceProductionContext context, ResourceInput input)
    {
        if (input.ContentLength > ResourceInput.MaxSourceCharacters)
        {
            ReportInvalid(
                context,
                input,
                line: 0,
                column: 0,
                $"the file contains {input.ContentLength} characters; the maximum supported is "
                    + $"{ResourceInput.MaxSourceCharacters}");

            return null;
        }

        if (input.Content is null)
        {
            ResxSourceGenerator.Report(
                context,
                GeneratorDiagnostics.s_invalidResource,
                input,
                line: 0,
                column: 0,
                input.Path,
                "the compiler did not provide the file contents");

            return null;
        }

        try
        {
            XDocument document = LoadDocument(input.Content);
            if (document.Root is null)
            {
                ReportInvalid(context, input, line: 0, column: 0, "the XML document has no root element");
                return null;
            }

            ImmutableArray<ResourceEntry>.Builder entries = ImmutableArray.CreateBuilder<ResourceEntry>();
            HashSet<string> names = [with(StringComparer.Ordinal)];
            int entryCount = 0;

            foreach (XElement data in document.Root.Elements().Where(
                static element => element.Name.LocalName == "data"))
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                (int line, int column) = GetLocation(data);
                entryCount++;
                if (entryCount > MaxResourceEntries)
                {
                    ReportInvalid(
                        context,
                        input,
                        line,
                        column,
                        $"the file contains more than {MaxResourceEntries} resource entries");

                    return null;
                }

                string? name = data.Attribute("name")?.Value;

                if (name is null || name.Length == 0)
                {
                    ReportInvalid(context, input, line, column, "a data element has no non-empty name");
                    continue;
                }

                if (!names.Add(name))
                {
                    ReportInvalid(context, input, line, column, $"resource entry '{name}' is duplicated");
                    continue;
                }

                string? typeName = data.Attribute("type")?.Value;
                string? mimeType = data.Attribute("mimetype")?.Value;
                if (!IsStringResource(typeName, mimeType))
                {
                    string resourceType = typeName is not null && typeName.Length > 0
                        ? typeName
                        : mimeType ?? "unknown";

                    ResxSourceGenerator.Report(
                        context,
                        GeneratorDiagnostics.s_nonStringResource,
                        input,
                        line,
                        column,
                        name,
                        resourceType);

                    continue;
                }

                XElement? valueElement = data.Elements().FirstOrDefault(
                    static element => element.Name.LocalName == "value");

                if (valueElement is null)
                {
                    ReportInvalid(context, input, line, column, $"resource entry '{name}' has no value element");
                    continue;
                }

                string value = valueElement.Value.Trim();
                string identifier = CSharpIdentifier.FromResourceName(name);
                FormatArguments formatArguments = FormatArguments.Parse(value);
                if (formatArguments.Error is not null)
                {
                    ReportInvalid(
                        context,
                        input,
                        line,
                        column,
                        $"resource entry '{name}' {formatArguments.Error}");

                    return null;
                }

                entries.Add(new(
                    name,
                    value,
                    identifier,
                    line,
                    column,
                    formatArguments));
            }

            return new(entries.ToImmutable());
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (XmlException exception)
        {
            ReportInvalid(
                context,
                input,
                Math.Max(0, exception.LineNumber - 1),
                Math.Max(0, exception.LinePosition - 1),
                exception.Message);

            return null;
        }
        catch (Exception exception)
        {
            ReportInvalid(context, input, line: 0, column: 0, exception.Message);
            return null;
        }
    }

    private static XDocument LoadDocument(string content)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = ResourceInput.MaxSourceCharacters,
            XmlResolver = null
        };

        using StringReader textReader = new(content);
        using XmlReader xmlReader = XmlReader.Create(textReader, settings);
        return XDocument.Load(xmlReader, LoadOptions.SetLineInfo);
    }

    private static bool IsStringResource(string? typeName, string? mimeType)
    {
        if (!string.IsNullOrEmpty(mimeType))
        {
            return false;
        }

        if (typeName is null || string.IsNullOrWhiteSpace(typeName))
        {
            return true;
        }

        string normalizedType = typeName.Trim();
        const string StringType = "System.String";
        return normalizedType.Equals(StringType, StringComparison.Ordinal)
            || (normalizedType.StartsWith(StringType, StringComparison.Ordinal)
                && normalizedType.Length > StringType.Length
                && normalizedType[StringType.Length] == ',');
    }

    private static (int Line, int Column) GetLocation(XElement element)
    {
        if (element is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo())
        {
            return (0, 0);
        }

        return (Math.Max(0, lineInfo.LineNumber - 1), Math.Max(0, lineInfo.LinePosition - 1));
    }

    private static void ReportInvalid(
        SourceProductionContext context,
        ResourceInput input,
        int line,
        int column,
        string detail)
    {
        ResxSourceGenerator.Report(
            context,
            GeneratorDiagnostics.s_invalidResource,
            input,
            line,
            column,
            input.Path,
            detail);
    }
}
