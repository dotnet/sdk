// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace Microsoft.DotNet.FileBasedPrograms;

internal readonly partial struct ProjectCollection(Microsoft.Build.Evaluation.ProjectCollection inner)
{
    public Microsoft.Build.Evaluation.ProjectCollection Inner => inner;
    public partial IDictionary<string, string> GlobalProperties => inner.GlobalProperties;
}

internal readonly partial struct ProjectInstance(Microsoft.Build.Execution.ProjectInstance inner)
{
    public static partial ProjectInstance FromProjectRootElement(
        ProjectRootElement projectRoot,
        ProjectCollection projectCollection,
        IDictionary<string, string> globalProperties)
    {
        return new(Microsoft.Build.Execution.ProjectInstance.FromProjectRootElement(projectRoot.Inner, new()
        {
            ProjectCollection = projectCollection.Inner,
            GlobalProperties = globalProperties,
        }));
    }

    public partial IEnumerable<ProjectItemInstance> GetItems(string itemType) => inner.GetItems(itemType).Select(i => new ProjectItemInstance(i));
    public partial string GetPropertyValue(string propertyName) => inner.GetPropertyValue(propertyName);
    public partial string ExpandString(string value) => inner.ExpandString(value);
}

internal readonly partial struct ProjectItemInstance(Microsoft.Build.Execution.ProjectItemInstance inner)
{
    public partial string GetMetadataValue(string name) => inner.GetMetadataValue(name);
}

internal readonly partial struct ProjectRootElement(Microsoft.Build.Construction.ProjectRootElement inner)
{
    public static partial ProjectRootElement Create(XmlReader xmlReader, ProjectCollection projectCollection)
    {
        return new(Microsoft.Build.Construction.ProjectRootElement.Create(xmlReader, projectCollection.Inner));
    }

    public Microsoft.Build.Construction.ProjectRootElement Inner => inner;
    public partial string FullPath { get => inner.FullPath; set => inner.FullPath = value; }
}
