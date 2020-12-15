using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests.Utils
{
    internal static class BootstrapperExtensions
    {
        internal static void InstallTestTemplate(this Bootstrapper bootStrapper, params string[] templates)
        {
            string codebase = typeof(BootstrapperExtensions).GetTypeInfo().Assembly.Location;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            string dir = Path.GetDirectoryName(asmPath);
            string baseDir = Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates");

            foreach (string template in templates)
            {
                string path = Path.Combine(baseDir, template) + Path.DirectorySeparatorChar;
                bootStrapper.Install(path);
            }
        }
    }
}
