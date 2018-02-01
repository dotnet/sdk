using System;
using System.Collections.Generic;

namespace BaselineComparer.Helpers
{
    public static class TemplateCommandRunner
    {
        public static bool RunTemplateCommand(string newCommand, string creationBaseDir, string templateCommand, string customHiveBasePath, bool isTemporaryHive)
        {
            List<string> templateCommandList = new List<string>() { templateCommand };

            TemplateDataCreator dataCreator = new TemplateDataCreator(newCommand, creationBaseDir, templateCommandList, customHiveBasePath);
            bool result = dataCreator.PerformTemplateCommands(isTemporaryHive);

            if (result)
            {
                Console.WriteLine("...data created successfully");
            }
            else
            {
                Console.WriteLine("...Error creating data.");
            }

            return result;
        }
    }
}
