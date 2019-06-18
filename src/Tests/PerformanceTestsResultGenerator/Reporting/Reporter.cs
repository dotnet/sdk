// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

using RuntimeEnvironment = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment;

namespace Reporting
{
    public class Reporter
    {
        private Run run;
        private Os os;
        private Build build;
        private List<Test> tests = new List<Test>();
        protected IEnvironment environment;

        private Reporter() { }

        public void AddTest(Test test)
        {
            if (tests.Any(t => t.Name.Equals(test.Name)))
                throw new Exception($"Duplicate test name, {test.Name}");
            tests.Add(test);
        }

        /// <summary>
        /// Get a Reporter. Relies on environment variables.
        /// </summary>
        /// <param name="environment">Optional environment variable provider</param>
        /// <returns>A Reporter instance or null if the environment is incorrect.</returns>
        public static Reporter CreateReporter(IEnvironment environment = null)
        {
            var ret = new Reporter();
            ret.environment = environment == null ? new EnvironmentProvider() : environment;
            if (!ret.CheckEnvironment())
            {
                return null;
            }
                        
            ret.Init();
            return ret;
        }

        private void Init()
        {
            run = new Run
            {
                CorrelationId = environment.GetEnvironmentVariable("HELIX_CORRELATION_ID"),
                PerfRepoHash = environment.GetEnvironmentVariable("PERFLAB_PERFHASH"),
                Name = environment.GetEnvironmentVariable("PERFLAB_RUNNAME"),
                Queue = environment.GetEnvironmentVariable("PERFLAB_QUEUE"),
            };
            Boolean.TryParse(environment.GetEnvironmentVariable("PERFLAB_HIDDEN"), out bool hidden);
            run.Hidden = hidden;
            var configs = environment.GetEnvironmentVariable("PERFLAB_CONFIGS");
            if (!String.IsNullOrEmpty(configs)) // configs should be optional.
            {
                foreach (var kvp in configs.Split(';'))
                {
                    var split = kvp.Split('=');
                    run.Configurations.Add(split[0], split[1]);
                } 
            }

            os = new Os()
            {
                Name = $"{RuntimeEnvironment.OperatingSystem} {RuntimeEnvironment.OperatingSystemVersion}",
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Locale = CultureInfo.CurrentUICulture.ToString()
            };

            build = new Build
            {
                Repo = environment.GetEnvironmentVariable("PERFLAB_REPO"),
                Branch = environment.GetEnvironmentVariable("PERFLAB_BRANCH"),
                Architecture = environment.GetEnvironmentVariable("PERFLAB_BUILDARCH"),
                Locale = environment.GetEnvironmentVariable("PERFLAB_LOCALE"),
                GitHash = environment.GetEnvironmentVariable("PERFLAB_HASH"),
                BuildName = environment.GetEnvironmentVariable("PERFLAB_BUILDNUM"),
                TimeStamp = DateTime.Parse(environment.GetEnvironmentVariable("PERFLAB_BUILDTIMESTAMP")),
            };
        }
        public string GetJson()
        {
            var jsonobj = new
            {
                build,
                os,
                run,
                tests
            };
            var settings = new JsonSerializerSettings();
            var resolver = new DefaultContractResolver();
            resolver.NamingStrategy = new CamelCaseNamingStrategy() { ProcessDictionaryKeys = false };
            settings.ContractResolver = resolver;
            return JsonConvert.SerializeObject(jsonobj, Formatting.Indented, settings);
        }

        private bool CheckEnvironment()
        {
            return environment.GetEnvironmentVariable("PERFLAB_INLAB")?.Equals("1") ?? false;
        }
    }
}
