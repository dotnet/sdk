// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Text.Json;

namespace Microsoft.NET.Build.Tasks
{
    internal static class RegFreeComManifest
    {
        /// <summary>
        /// Generates a side-by-side application manifest to enable reg-free COM.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly.</param>
        /// <param name="comHostName">The name of the comhost library.</param>
        /// <param name="assemblyVersion">The version of the assembly.</param>
        /// <param name="clsidMapPath">The path to the clasidmap file.</param>
        /// <param name="comManifestPath">The path to which to write the manifest.</param>
        private static void CreateManifestFromClsidmap(string assemblyName, string comHostName, string assemblyVersion, string clsidMapPath, string comManifestPath)
        {
            XNamespace ns = "urn:shemas-microsoft-com:asm.v1";

            XElement manifest = new XElement(ns + "assembly", new XAttribute("mainfestVersion", "1.0"));
            manifest.Add(new XElement(ns + "assemblyIdentity",
                new XAttribute("type", "win32"),
                new XAttribute("name", $"{assemblyName}.X"),
                new XAttribute("version", assemblyVersion)));

            XElement fileElement = new XElement(ns + "file", new XAttribute("name", comHostName));

            JsonDocument clsidMap;
            using (Stream fileStream = File.OpenRead(clsidMapPath))
            {
                clsidMap = JsonDocument.Parse(clsidMapPath);
            }

            foreach (var clsid in clsidMap.RootElement.EnumerateObject())
            {
                fileElement.Add(new XElement(ns + "comClass", new XAttribute("clsid", clsid.Name), new XAttribute("threadingModel", "Both")));
            }

            manifest.Add(fileElement);

            XDocument manifestDocument = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), manifest);
            using (XmlWriter manifestWriter = XmlWriter.Create(comManifestPath))
            {
                manifestDocument.WriteTo(manifestWriter);
            }
        }
    }
}
