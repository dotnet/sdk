//------------------------------------------------------------------------------
// <copyright file="CreateTemplateCommand.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;

namespace dotnet_new3.VSIX
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CreateTemplateCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;
        public const int CommandId2 = 0x0101;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("89acd9aa-d946-458d-8ad9-f0fd6db0b67c");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package _package;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateTemplateCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private CreateTemplateCommand(Package package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            OleMenuCommandService commandService = ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            if (commandService == null)
            {
                return;
            }

            CommandID menuCommandID = new CommandID(CommandSet, CommandId);
            MenuCommand menuItem = new OleMenuCommand(Invoke, ChangeHandler, QueryStatus, menuCommandID);
            commandService.AddCommand(menuItem);

            CommandID menuCommandID2 = new CommandID(CommandSet, CommandId2);
            MenuCommand menuItem2 = new OleMenuCommand(Invoke, ChangeHandler, QueryStatus, menuCommandID2);
            commandService.AddCommand(menuItem2);
        }

        private static void ChangeHandler(object sender, EventArgs e)
        {
        }

        private void QueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand s = (OleMenuCommand)sender;
            DTE2 dte = ServiceProvider.GetService(typeof(SDTE)) as DTE2;

            bool on;
            if (dte != null)
            {
                UIHierarchy hierarchy = dte.ToolWindows.SolutionExplorer;
                Array items = hierarchy.SelectedItems as Array;
                Project proj = items?.OfType<UIHierarchyItem>().Select(x => x.Object).OfType<Project>().FirstOrDefault();
                Solution sln = items?.OfType<UIHierarchyItem>().Select(x => x.Object).OfType<Solution>().FirstOrDefault();
                on = proj != null || sln != null;

                if (on)
                {
                    string dir = proj?.Properties.Item("FullPath").Value.ToString() ??
                                 sln.Properties.Item("Path").Value.ToString();
                    on = !File.Exists(Path.Combine(dir, ".netnew.json"));
                }
            }
            else
            {
                on = false;
            }

            s.Enabled = on;
            s.Supported = on;
            s.Visible = on;
        }

        private void Invoke(object sender, EventArgs e)
        {
            DTE2 dte = ServiceProvider.GetService(typeof(SDTE)) as DTE2;

            if (dte != null)
            {
                UIHierarchy hierarchy = dte.ToolWindows.SolutionExplorer;
                Array items = hierarchy.SelectedItems as Array;
                Project proj = items?.OfType<UIHierarchyItem>().Select(x => x.Object).OfType<Project>().FirstOrDefault();
                Solution sln = items?.OfType<UIHierarchyItem>().Select(x => x.Object).OfType<Solution>().FirstOrDefault();

                if (proj != null)
                {
                    CreateProjectTemplate(proj);
                }
                else if (sln != null)
                {
                    CreateSolutionTemplate(sln);
                }
            }
        }

        private static void CreateSolutionTemplate(Solution solution)
        {
            string fullPath = solution.FullName;
            string dir = Path.GetDirectoryName(fullPath);
            string name = Path.GetFileNameWithoutExtension(fullPath);

            InfoCollectorDialog win = new InfoCollectorDialog(name);
            win.CenterInVs();
            if (win.ShowDialog().GetValueOrDefault())
            {
                const string solutionTemplate = @"{
    ""author"": """",
    ""classifications"": [ ],
    ""description"": """",
    ""name"": """",
    ""defaultName"": """",
    ""groupIdentity"": """",
    ""tags"": { },
    ""shortName"": """",
    ""sourceName"": """",
    ""guids"": [ ]
}";

                JObject o = JObject.Parse(solutionTemplate);
                o["author"] = win.AuthorTextBox.Text;
                o["name"] = win.FriendlyNameTextBox.Text;
                o["defaultName"] = win.DefaultNameTextBox.Text;
                o["sourceName"] = Path.GetFileNameWithoutExtension(solution.FullName);
                o["shortName"] = win.ShortNameTextBox.Text;
                JArray guids = (JArray)o["guids"];

                Regex rx = new Regex(@"\{?[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}?", RegexOptions.IgnoreCase);
                string contents = File.ReadAllText(solution.FullName);
                HashSet<Guid> distinctGuids = new HashSet<Guid>();

                foreach(Match match in rx.Matches(contents))
                {
                    distinctGuids.Add(Guid.Parse(match.Value));
                }

                rx = new Regex(@"(?<=\("")\{?[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}?(?=""\))", RegexOptions.IgnoreCase);

                foreach (Match match in rx.Matches(contents))
                {
                    distinctGuids.Remove(Guid.Parse(match.Value));
                }

                foreach (Guid g in distinctGuids)
                {
                    guids.Add(g);
                }

                File.WriteAllText(Path.Combine(dir, ".netnew.json"), o.ToString());
            }
        }

        private static void CreateProjectTemplate(Project proj)
        {
            string fullPath = proj.FullName;
            string dir = Path.GetDirectoryName(fullPath);
            string name = Path.GetFileNameWithoutExtension(fullPath);

            InfoCollectorDialog win = new InfoCollectorDialog(name);
            win.CenterInVs();
            if (win.ShowDialog().GetValueOrDefault())
            {
                const string solutionTemplate = @"{
    ""author"": """",
    ""classifications"": [ ],
    ""description"": """",
    ""name"": """",
    ""defaultName"": """",
    ""groupIdentity"": """",
    ""tags"": { },
    ""shortName"": """",
    ""sourceName"": """",
    ""guids"": [ ]
}";

                JObject o = JObject.Parse(solutionTemplate);
                o["author"] = win.AuthorTextBox.Text;
                o["name"] = win.FriendlyNameTextBox.Text;
                o["defaultName"] = win.DefaultNameTextBox.Text;
                o["sourceName"] = Path.GetFileNameWithoutExtension(proj.FullName);
                o["shortName"] = win.ShortNameTextBox.Text;
                JArray guids = (JArray)o["guids"];
                guids.Add(ExtractProjectGuid(fullPath));

                File.WriteAllText(Path.Combine(dir, ".netnew.json"), o.ToString());
            }
        }

        private static string ExtractProjectGuid(string fullPath)
        {
            XDocument doc = XDocument.Load(fullPath);
            XElement element = doc.Descendants().First(x => x.Name.LocalName == "ProjectGuid");
            return element.Value;
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static CreateTemplateCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider => _package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new CreateTemplateCommand(package);
        }
    }
}
