// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Microsoft.DotNet.FileBasedPrograms;

public sealed class BuildService : IBuildService
{
    public static BuildService Instance { get; } = new();

    private BuildService() { }

    public IProjectInstance CreateProjectInstanceFromProjectRootElement(
        IProjectRootElement projectRoot,
        IProjectCollection projectCollection,
        IDictionary<string, string> globalProperties)
    {
        return ProjectInstance.FromProjectRootElement((ProjectRootElement)projectRoot, (ProjectCollection)projectCollection, globalProperties);
    }

    public IProjectRootElement CreateProjectRootElement(XmlReader xmlReader, IProjectCollection projectCollection, string entryPointFilePath)
    {
        return ProjectRootElement.Create(xmlReader, (ProjectCollection)projectCollection, entryPointFilePath);
    }
}

public static class BuildServiceExtensions
{
    private static readonly ConditionalWeakTable<Microsoft.Build.Evaluation.ProjectCollection, IProjectCollection> s_projectCollections = new();

    extension(Microsoft.Build.Evaluation.ProjectCollection projectCollection)
    {
        public IProjectCollection Wrap()
        {
            return s_projectCollections.GetValue(projectCollection, static inner => new ProjectCollection(inner));
        }
    }

    extension(Microsoft.Build.Execution.ProjectInstance projectInstance)
    {
        public IProjectInstance Wrap()
        {
            return new ProjectInstance(projectInstance);
        }
    }

    extension(IProjectRootElement projectRootElement)
    {
        public Microsoft.Build.Construction.ProjectRootElement Unwrap()
        {
            return ((ProjectRootElement)projectRootElement).Inner;
        }
    }

    extension(IProjectInstance projectInstance)
    {
        public Microsoft.Build.Execution.ProjectInstance Unwrap()
        {
            return ((ProjectInstance)projectInstance).Inner;
        }
    }
}

sealed file class ProjectCollection(Microsoft.Build.Evaluation.ProjectCollection inner) : IProjectCollection
{
    /// <summary>
    /// Keeps a strong reference to the latest virtual <see cref="IProjectRootElement"/> created for each entry point in this <see cref="IProjectCollection"/>,
    /// preventing it from being garbage collected when MSBuild's <c>ProjectRootElementCache</c> demotes it to a weak reference.
    /// Without this, nested <c>&lt;MSBuild&gt;</c> tasks that re-evaluate virtual projects with different properties
    /// would fail to find the <see cref="IProjectRootElement"/> in the cache and try to load it from disk,
    /// resulting in MSB4025 because the virtual project file does not exist on disk.
    /// See <see href="https://github.com/dotnet/sdk/issues/52714"/>.
    /// </summary>
    private readonly ConcurrentDictionary<string, Microsoft.Build.Construction.ProjectRootElement> _projectRootElements = new(StringComparer.OrdinalIgnoreCase);

    public Microsoft.Build.Evaluation.ProjectCollection Inner => inner;
    public IDictionary<string, string> GlobalProperties => inner.GlobalProperties;

    public void StoreProjectRootElement(string entryPointFilePath, Microsoft.Build.Construction.ProjectRootElement projectRootElement)
    {
        _projectRootElements[entryPointFilePath] = projectRootElement;
    }
}

sealed file class ProjectInstance(Microsoft.Build.Execution.ProjectInstance inner) : IProjectInstance
{
    public static ProjectInstance FromProjectRootElement(
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

    public Microsoft.Build.Execution.ProjectInstance Inner => inner;
    public IEnumerable<IProjectItemInstance> GetItems(string itemType) => inner.GetItems(itemType).Select(i => new ProjectItemInstance(i));
    public string GetPropertyValue(string propertyName) => inner.GetPropertyValue(propertyName);
    public string ExpandString(string value) => inner.ExpandString(value);
}

sealed file class ProjectItemInstance(Microsoft.Build.Execution.ProjectItemInstance inner) : IProjectItemInstance
{
    public string GetMetadataValue(string name) => inner.GetMetadataValue(name);
    public string ItemType => inner.ItemType;
}

sealed file class ProjectRootElement(Microsoft.Build.Construction.ProjectRootElement inner) : IProjectRootElement
{
    public static ProjectRootElement Create(XmlReader xmlReader, ProjectCollection projectCollection, string entryPointFilePath)
    {
        var inner = Microsoft.Build.Construction.ProjectRootElement.Create(xmlReader, projectCollection.Inner);
        projectCollection.StoreProjectRootElement(entryPointFilePath, inner);
        return new(inner);
    }

    public Microsoft.Build.Construction.ProjectRootElement Inner => inner;
    public string? FullPath { get => inner.FullPath; set => inner.FullPath = value; }
    public string GetRawXml() => inner.RawXml;
}
