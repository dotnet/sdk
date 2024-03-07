// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// TBD - license to be updated

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using static System.Collections.Specialized.BitVector32;
using Microsoft.DotNet.Cli.Utils;
using System.Security.Permissions;
using Microsoft.DotNet.Cli.Sln.Internal.FileManipulation;
using NuGet.Versioning;
using System.Reflection;

namespace Microsoft.DotNet.Cli.Sln.Internal
{
    public class ProjConfigPlatform
    {
        internal string _slnConfig;
        private string _slnPlatform;
        internal string _build;
        private string _deploy;
        private string _projConfig;
        private string _projPlatform;

        public ProjConfigPlatform(string slnConfig, string slnPlatform,
            string build, string deploy, string projConfig, string projPlatform, string line)
        {
            _slnConfig = slnConfig;
            _slnPlatform = slnPlatform;
            _build = build;
            _deploy = deploy;
            _projConfig = projConfig;
            _projPlatform = projPlatform;
        }

        public ProjConfigPlatform(ProjConfigPlatform pcp, string newSlnConfig, string createNewProjectConfig, string build=null)
        {
            _slnConfig = newSlnConfig;
            _slnPlatform = pcp._slnPlatform;
            if (string.IsNullOrEmpty(build)) _build = pcp._build; else _build = build;
            _deploy = pcp._deploy;
            if (createNewProjectConfig.Equals("y"))
                _projConfig = newSlnConfig;
            else
                _projConfig = pcp._projConfig;
            _projPlatform = pcp._projPlatform;
        }

        public string PropertyKey()
        {
            return _slnConfig + "|" + _slnPlatform + "." + _build;
        }

        public string PropertyValue()
        {
            return _projConfig + "|" + _projPlatform;
        }
    }

    public class SlnConfigPlatform
    {
        internal string _config;
        internal string _platform;
        public SlnConfigPlatform(string config, string platform)
        {
            _config = config;
            _platform = platform;
        }

        public SlnConfigPlatform(SlnConfigPlatform scp, string newconfig)
        {
            _config = newconfig;
            _platform = scp._platform;
        }

        public string PropertyKey()
        {
            return _config + "|" + _platform;
        }
    }

    public partial class SlnFile
    {
        //private string ActiveSlnConfig = "";
        //private string ActiveSlnPlatform = "";
        private const string SlnConfigPlatformsSectionId = "SolutionConfigurationPlatforms";
        private const string ProjConfigPlatformsSectionId = "ProjectConfigurationPlatforms";

        private List<string> _slnConfigs = new List<string>();
        private List<string> _slnPlatforms = new List<string>();
        private List<SlnConfigPlatform> _slnConfigPlatforms = new List<SlnConfigPlatform>();
        internal void LoadConfigurations(SlnSection sec)
        {
            string Id = sec.Id;
            
            if (Id.Equals(SlnConfigPlatformsSectionId))
            {
                LoadConfigurations(sec._sectionLines);
            }
            else if (Id.Equals(ProjConfigPlatformsSectionId))
            {
                foreach (SlnProject project in _projects)
                {
                    List<string> projSectionLines = sec._sectionLines.FindAll(
                        delegate (string line)
                        {
                            return line.StartsWith(project.Id);
                        });

                    project.LoadConfigurations(projSectionLines);
                }
            }
        }
        public bool LoadConfigurations(List<string> sectionLines)
        {
            foreach (string line in sectionLines)
            {
                int i = line.IndexOf('|');
                int j = line.IndexOf('=');
                if (i != -1 && j != -1)
                {
                    string config = line.Substring(0, i).Trim();
                    string platform = line.Substring(i + 1, j - i - 1).Trim();
                    SlnConfigPlatform entry = new SlnConfigPlatform(config, platform);
                    _slnConfigPlatforms.Add(entry);

                    if (!_slnConfigs.Contains(config))
                        _slnConfigs.Add(config);

                    if (!_slnPlatforms.Contains(platform))
                        _slnPlatforms.Add(platform);
                }
            }
            return true;
        }

        public void WriteConfigutations(string file = null)
        {
            Write(file);
        }

        public void AddNewSlnConfig(SlnConfigPlatform scp, string newConfigName)
        {
            if (!_slnConfigs.Contains(newConfigName))
                _slnConfigs.Add(newConfigName);

            SlnConfigPlatform newscp = new SlnConfigPlatform(scp, newConfigName);
            _slnConfigPlatforms.Add(newscp);

            string key = newscp.PropertyKey();
            _sections.GetSection(SlnConfigPlatformsSectionId).Properties.AddProperty(key, key);
        }

        public bool AddNewSlnConfig(string newconfigname, string copyfromoldconfig = null, string createnewprojectconfig = null)
         {
            if(_slnConfigs.Contains(newconfigname))
            {
                throw new GracefulException("ConfigureAddConfigAlreadyExists");
            }

            if (!string.IsNullOrEmpty(copyfromoldconfig) && !_slnConfigs.Contains(copyfromoldconfig))
            {
                throw new GracefulException("ConfigureAddCopyFromDoesNotExists");
            }

            List<SlnConfigPlatform> slnconfigplatforms = _slnConfigPlatforms.FindAll(scp => scp._config.Equals(copyfromoldconfig));
            if (slnconfigplatforms.Count <= 0) 
            {
                return false;
            }

            foreach (SlnConfigPlatform scp in slnconfigplatforms)
                AddNewSlnConfig(scp, newconfigname);

            foreach (SlnProject project in _projects)
            {
                List<ProjConfigPlatform> projconfigplatforms =
                    project._projConfigPlatforms.FindAll(pcp => pcp._slnConfig.Equals(copyfromoldconfig));

                if (projconfigplatforms.Count <= 0)
                    continue;

                foreach (ProjConfigPlatform pcp in projconfigplatforms)
                {
                    project._propSet = _sections.GetSection(ProjConfigPlatformsSectionId).NestedPropertySets.GetPropertySet(project.Id);
                    project.AddNewSlnConfig(pcp, newconfigname, copyfromoldconfig, createnewprojectconfig);
                }
            }
             return true;
         }

        public bool AddNewSlnPlatform(string newconfigname, string copyfromoldconfig = "", bool createnewprojectconfig = true)
        {
            return true;
        }

        public bool AddNewProjConfig(string project, string newconfigname, string copyfromoldconfig = "", bool createnewslnconfig = true)
        {
            return true;
        }

        public bool AddNewProjPlatform(string project, string newconfigname, string copyfromoldconfig = "", bool createnewslnconfig = true)
        {
            return true;
        }

    }

    public partial class SlnProject
    {
        internal SlnPropertySet _propSet;
        private List<string> _projConfigs = new List<string>();
        private List<string> _projPlatforms = new List<string>();
        internal List<ProjConfigPlatform> _projConfigPlatforms = new List<ProjConfigPlatform>();
        private const string ProjConfigPlatformsSectionId = "ProjectConfigurationPlatforms";
        public void AddNewSlnConfig(ProjConfigPlatform pcp, string newConfigName, string copyFromOldConfig, string createNewProjectConfig)
        {
            if (!_projConfigs.Contains(newConfigName))
                _projConfigs.Add(newConfigName);

            if (string.IsNullOrEmpty(copyFromOldConfig))
            {
                //Add 2 entries, one for Activeconfig & Build.0
                ProjConfigPlatform newpcp = new ProjConfigPlatform(pcp, newConfigName, createNewProjectConfig, "ActiveCfg");
                _projConfigPlatforms.Add(newpcp);
                _propSet.AddProperty(newpcp.PropertyKey(), newpcp.PropertyValue());

                newpcp = new ProjConfigPlatform(pcp, newConfigName, createNewProjectConfig, "Build.0");
                _projConfigPlatforms.Add(newpcp);
                _propSet.AddProperty(newpcp.PropertyKey(), newpcp.PropertyValue());
            }
            else
            {
                ProjConfigPlatform newpcp = new ProjConfigPlatform(pcp, newConfigName, createNewProjectConfig);
                _projConfigPlatforms.Add(newpcp);
                _propSet.AddProperty(newpcp.PropertyKey(), newpcp.PropertyValue());
            }
        }

        internal bool LoadConfigurations(List<string> sectionLines)
        {
            foreach (string line in sectionLines)
            {
                int p = line.IndexOf('.');
                if (p != -1)
                {
                    int i = line.IndexOf('|', p);
                    int j = line.IndexOf('.', i);
                    int l = line.IndexOf('=', j);
                    int m = line.IndexOf('|', l);
                    int n = line.Length;
                    if (i != -1 && j != -1 && l != -1)
                    {
                        string slnConfig = line.Substring(p + 1, i - p - 1).Trim();
                        string slnPlatform = line.Substring(i + 1, j - i - 1).Trim();
                        string build = line.Substring(j + 1, l - j - 1).Trim();
                        string deploy = "";
                        string projConfig = line.Substring(l + 1, m - l - 1).Trim();
                        string projPlatform = line.Substring(m + 1, n - m - 1).Trim();

                        ProjConfigPlatform entry = new ProjConfigPlatform(slnConfig, slnPlatform, build, deploy, projConfig, projPlatform, line);
                        _projConfigPlatforms.Add(entry);

                        if (!_projConfigs.Contains(projConfig))
                            _projConfigs.Add(projConfig);

                        if (!_projPlatforms.Contains(projPlatform))
                            _projPlatforms.Add(projPlatform);
                    }
                }
            }
            return false;
        }

    }

    public partial class SlnPropertySet
    {
        internal void AddProperty(string key, string value)
        {
            _values.Add(key, value);
        }
    }
}
