// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.IDE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TemplateWizard;

namespace Microsoft.TemplateEngine.VisualStudioWizard
{
    public abstract class TemplateEngineWizard : IWizard
    {
        protected TemplateEngineWizard()
        {
        }

        protected abstract Bootstrapper CreateBoostrapper();


        public void BeforeOpeningFile(ProjectItem projectItem)
        {
        }

        public void ProjectFinishedGenerating(Project project)
        {
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
        }

        public void RunFinished()
        {
        }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            DTE dte = ServiceProvider.GlobalProvider.GetService(typeof(SDTE)) as DTE;

            if (dte == null)
            {
                throw new WizardCancelledException("Unable to get DTE");
            }
            
            Bootstrapper bootstrapper = CreateBoostrapper();

            if (!replacementsDictionary.TryGetValue("$WizardData$", out string wizardData))
            {
                throw new WizardCancelledException("Wizard data not found: Expected a string containing the identity of the template to create");
            }

            wizardData = wizardData.Trim();
            IFilteredTemplateInfo match = bootstrapper.ListTemplates(true, x =>
            {
                if (string.Equals(x.Identity, wizardData, StringComparison.OrdinalIgnoreCase))
                {
                    return new MatchInfo { Kind = MatchKind.Exact, Location = MatchLocation.Name };
                }

                return null;
            }).FirstOrDefault(x => x.IsMatch);

            if (match == null)
            {
                throw new WizardCancelledException($"Wizard data malformed: no template found with identity {wizardData}");
            }

            ITemplateInfo info = match.Info;
            // TODO: Determine where to get the baseline name from, as needed. baselineName is a placeholder so template engine builds.
            string baselineName = null;
            ICreationResult result = bootstrapper.CreateAsync(info, replacementsDictionary[""], replacementsDictionary[""], ExtractParametersFromReplacementsDictionary(replacementsDictionary), SkipUpdatesCheck, baselineName).Result;

            foreach (ICreationPath path in result.PrimaryOutputs)
            {
                dte.Solution.AddFromFile(path.Path);
            }
        }

        protected virtual bool SkipUpdatesCheck { get { return true; } }

        protected virtual IReadOnlyDictionary<string, string> ExtractParametersFromReplacementsDictionary(IReadOnlyDictionary<string, string> replacementsDictionary)
        {
            return new Dictionary<string, string>();
        }

        public bool ShouldAddProjectItem(string filePath) => false;
    }
}
