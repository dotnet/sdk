// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Utilities
{
    public static class ProjectModification
    {
        public static void AddDisplayMessageToProject(XDocument project, string beforeTargets)
        {
            if (project.Root is null)
            {
                throw new InvalidOperationException($"The project file '{project}' does not have a root element.");
            }
            XNamespace ns = project.Root.Name.Namespace;

            XElement target = new(ns + "Target", new XAttribute("Name", "DisplayMessages"),
                new XAttribute("BeforeTargets", beforeTargets));
            project.Root.Add(target);

            target.Add(new XElement(ns + "Message", new XAttribute("Text", "Important text"),
                new XAttribute("Importance", "high")));
        }

        public static void AddDisplayMessageBeforeVsTestToProject(XDocument project)
        {
            AddDisplayMessageToProject(project, "VSTest");
        }

        public static void AddDisplayMessageBeforeRestoreToProject(XDocument project)
        {
            AddDisplayMessageToProject(project, "Restore");
        }
    }
}
