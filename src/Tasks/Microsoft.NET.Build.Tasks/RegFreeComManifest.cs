// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Build.Tasks
{
    internal static class RegFreeComManifest
    {
        public static void CreateManifestFromClsidmap(string assemblyName, string comHostName, string assemblyVersion, string clsidMapPath, string comManifestPath)
        {
            XNamespace ns = "urn:shemas-microsoft-com:asm.v1";

            XElement manifest = new XElement(ns + "assembly", new XAttribute("mainfestVersion", "1.0"));
            manifest.Add(new XElement(ns + "assemblyIdentity",
                new XAttribute("type", "win32"),
                new XAttribute("name", $"{assemblyName}.X"),
                new XAttribute("version", assemblyVersion)));

            XElement fileElement = new XElement(ns + "file", new XAttribute("name", comHostName));

            string clsidMapText = File.ReadAllText(clsidMapPath);
            JObject clsidMap = JObject.Parse(clsidMapText);

            foreach (JProperty property in clsidMap.Properties())
            {
                string guid = property.Name;
                fileElement.Add(new XElement(ns + "comClass", new XAttribute("clsid", guid), new XAttribute("threadingModel", "Both")));
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
