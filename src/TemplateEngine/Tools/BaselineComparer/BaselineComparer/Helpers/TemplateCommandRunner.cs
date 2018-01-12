using System;
using System.Collections.Generic;

namespace BaselineComparer.Helpers
{
    public static class TemplateCommandRunner
    {
        public static bool RunTemplateCommands(string newCommand, string creationBaseDir, IReadOnlyList<string> templateCommands)
        {
            TemplateDataCreator dataCreator = new TemplateDataCreator(newCommand, creationBaseDir, templateCommands);
            bool result = dataCreator.PerformTemplateCommands();

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
