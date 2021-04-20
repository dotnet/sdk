// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests.Utils
{
    internal static class BasicParametersParser
    {
        internal static string GetNameFromParameterString(string parameters)
        {
            string[] parametersArray = parameters.Split(null);

            int nameIndex = Array.IndexOf(parametersArray, "--name");
            if (nameIndex >= 0 && nameIndex + 1 < parameters.Length)
            {
                return parametersArray[nameIndex + 1];
            }
            return "test";
        }

        internal static string GetOutputFromParameterString(string parameters)
        {
            string[] parametersArray = parameters.Split(null);

            int outputIndex = Array.IndexOf(parametersArray, "--output");
            if (outputIndex >= 0 && outputIndex + 1 < parameters.Length)
            {
                return parametersArray[outputIndex + 1];
            }
            return TestUtils.CreateTemporaryFolder();
        }

        internal static Dictionary<string, string> ParseParameterString(string parameters)
        {
            Dictionary<string, string> parsedParameters = new Dictionary<string, string>();
            string[] parametersArray = parameters.Split(null);
            int i = 0;

            while (i < parametersArray.Length)
            {
                if (parametersArray[i] == "--name" || parametersArray[i] == "--output")
                {
                    i += 2;
                    continue;
                }
                if (!parametersArray[i].StartsWith("--"))
                {
                    i++;
                    continue;
                }

                if (i + 1 < parametersArray.Length && !parametersArray[i + 1].StartsWith("--"))
                {
                    parsedParameters[parametersArray[i].Substring(2)] = parametersArray[i + 1];
                    i += 2;
                    continue;
                }
                parsedParameters[parametersArray[i].Substring(2)] = string.Empty;
                i++;
            }
            return parsedParameters;
        }
    }

}
