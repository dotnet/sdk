//------------------------------------------------------------------------------
// <copyright file="CreateTemplateCommand.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using dotnet_new3.VSIX.Properties;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

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
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            _package = package;

            OleMenuCommandService commandService = ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            if (commandService == null)
            {
                return;
            }

            CommandID menuCommandID = new CommandID(CommandSet, CommandId);
            MenuCommand menuItem = new OleMenuCommand(Invoke, ChangeHandler, QueryStatus, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        private void ChangeHandler(object sender, EventArgs e)
        {
        }

        private void QueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand s = (OleMenuCommand) sender;
            DTE2 dte = ServiceProvider.GetService(typeof(SDTE)) as DTE2;

            bool on;
            if (dte != null)
            {
                UIHierarchy hierarchy = dte.ToolWindows.SolutionExplorer;
                Array items = hierarchy.SelectedItems as Array;
                Project proj = items?.OfType<UIHierarchyItem>().Select(x => x.Object).OfType<Project>().FirstOrDefault();
                on = proj != null;

                if (on)
                {
                    string dir = proj.Properties.Item("FullPath").Value.ToString();
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

                if (proj != null)
                {
                    string fullPath = proj.FullName;
                    string dir = Path.GetDirectoryName(fullPath);
                    string name = Path.GetFileNameWithoutExtension(fullPath);
                    string ext = Path.GetExtension(fullPath).TrimStart('.').ToLowerInvariant();
                    string guid = proj.Properties.Item("AssemblyGuid").Value.ToString();

                    InfoCollectorDialog win = new InfoCollectorDialog(name);
                    if (win.ShowDialog().GetValueOrDefault())
                    {
                        string friendlyName = win.FriendlyName;
                        string defaultName = win.DefaultName;
                        string shortName = win.ShortName;

                        string output = Resources.TemplateFile;
                        output = output.Replace("{FriendlyName}", friendlyName);
                        output = output.Replace("{DefaultName}", defaultName);
                        output = output.Replace("{ShortName}", shortName);
                        output = output.Replace("{ProjectName}", name);
                        output = output.Replace("{Extension}", ext);
                        output = output.Replace("{ProjectGuid}", guid);
                        output = output.Replace("{UpperProjectGuid}", $"{{{guid}}}".ToUpperInvariant());

                        File.WriteAllText(Path.Combine(dir, ".netnew.json"), output);
                    }
                }
            }
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
