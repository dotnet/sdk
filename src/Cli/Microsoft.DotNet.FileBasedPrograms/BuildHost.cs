// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Xml;

namespace Microsoft.DotNet.FileBasedPrograms;

internal readonly partial struct ProjectCollection
{
    public partial IDictionary<string, string> GlobalProperties { get; }
}

internal readonly partial struct ProjectInstance
{
    public static partial ProjectInstance FromProjectRootElement(
        ProjectRootElement projectRoot,
        ProjectCollection projectCollection,
        IDictionary<string, string> globalProperties);
    public partial IEnumerable<ProjectItemInstance> GetItems(string itemType);
    public partial string GetPropertyValue(string propertyName);
    public partial string ExpandString(string value);
}

internal readonly partial struct ProjectItemInstance
{
    public partial string GetMetadataValue(string name);
}

internal readonly partial struct ProjectRootElement
{
    public static partial ProjectRootElement Create(XmlReader xmlReader, ProjectCollection projectCollection);
    public partial string FullPath { get; set; }
}

#if FILE_BASED_PROGRAMS_SOURCE_PACKAGE_BUILD

internal readonly partial struct ProjectCollection
{
    public partial IDictionary<string, string> GlobalProperties => throw null!;
}

internal readonly partial struct ProjectInstance
{
    public static partial ProjectInstance FromProjectRootElement(
        ProjectRootElement projectRoot,
        ProjectCollection projectCollection,
        IDictionary<string, string> globalProperties)
        => throw null!;
    public partial IEnumerable<ProjectItemInstance> GetItems(string itemType) => throw null!;
    public partial string GetPropertyValue(string propertyName) => throw null!;
    public partial string ExpandString(string value) => throw null!;
}

internal readonly partial struct ProjectItemInstance
{
    public partial string GetMetadataValue(string name) => throw null!;
}

internal readonly partial struct ProjectRootElement
{
    public static partial ProjectRootElement Create(XmlReader xmlReader, ProjectCollection projectCollection) => throw null!;
    public partial string FullPath { get => throw null!; set => throw null!; }
}

#endif
