using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    public static class TemplateEngineHostHelper
    {
        private static readonly string DefaultHostVersion = "1.0.0";

        private static readonly Dictionary<string, string> DefaultPreferences = new Dictionary<string, string>
        {
            { "prefs:language", "C#" }
        };

        public static DefaultTemplateEngineHost CreateHost(string hostIdentifier, string hostVersion = null, Dictionary<string, string> preferences = null)
        {
            if (string.IsNullOrEmpty(hostIdentifier))
            {
                throw new Exception("hostIdentifier cannot be null");
            }

            if (string.IsNullOrEmpty(hostVersion))
            {
                hostVersion = DefaultHostVersion;
            }

            if (preferences == null)
            {
                preferences = DefaultPreferences;
            }

            try
            {
                string versionString = Dotnet.Version().CaptureStdOut().Execute().StdOut;
                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    preferences["dotnet-cli-version"] = versionString.Trim();
                }
            }
            catch
            { }

            var builtIns = new AssemblyComponentCatalog(new[]
            {
                typeof(RunnableProjectGenerator).GetTypeInfo().Assembly,    // RPG
                typeof(NupkgInstallUnitDescriptorFactory).GetTypeInfo().Assembly,   // edge
            });

            // use "dotnetcli" as a fallback host so the correct host specific files are read.
            DefaultTemplateEngineHost host = new DefaultTemplateEngineHost(hostIdentifier, hostVersion, CultureInfo.CurrentCulture.Name, preferences, builtIns, new[] { "dotnetcli" });

            // Consider having these around for diagnostic runs.
            //AddAuthoringLogger(host);
            //AddInstallLogger(host);

            return host;
        }

        private static void AddAuthoringLogger(DefaultTemplateEngineHost host)
        {
            Action<string, string[]> authoringLogger = (message, additionalInfo) =>
            {
                Console.WriteLine(string.Format("Authoring: {0}", message));
            };
            host.RegisterDiagnosticLogger("Authoring", authoringLogger);
        }

        private static void AddInstallLogger(DefaultTemplateEngineHost host)
        {
            Action<string, string[]> installLogger = (message, additionalInfo) =>
            {
                Console.WriteLine(string.Format("Install: {0}", message));
            };
            host.RegisterDiagnosticLogger("Install", installLogger);
        }


        // this is mostly a copy of FirstRun() from dotnet_new3.Program.cs
        public static void FirstRun(IEngineEnvironmentSettings environmentSettings, IInstaller installer)
        {
            string baseDir = Environment.ExpandEnvironmentVariables("%DN3%");

            if (baseDir.Contains("%"))
            {
                Assembly a = typeof(Program).GetTypeInfo().Assembly;
                string path = new Uri(a.CodeBase, UriKind.Absolute).LocalPath;
                path = Path.GetDirectoryName(path);
                Environment.SetEnvironmentVariable("DN3", path);
            }

            List<string> toInstallList = new List<string>();
            Paths paths = new Paths(environmentSettings);

            if (paths.FileExists(paths.Global.DefaultInstallPackageList))
            {
                toInstallList.AddRange(paths.ReadAllText(paths.Global.DefaultInstallPackageList).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }

            if (paths.FileExists(paths.Global.DefaultInstallTemplateList))
            {
                toInstallList.AddRange(paths.ReadAllText(paths.Global.DefaultInstallTemplateList).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }

            if (toInstallList.Count > 0)
            {
                for (int i = 0; i < toInstallList.Count; i++)
                {
                    toInstallList[i] = toInstallList[i].Replace("\r", "")
                                                        .Replace('\\', Path.DirectorySeparatorChar);
                }

                installer.InstallPackages(toInstallList);
            }
        }
    }
}
