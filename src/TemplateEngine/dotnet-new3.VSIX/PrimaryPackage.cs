using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace dotnet_new3.VSIX
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class PrimaryPackage : Package
    {
        public const string PackageGuidString = "2a73a40e-64af-4590-98c4-ba98e4960c68";

        public static DTE DTE { get; private set; }

        protected override void Initialize()
        {
            base.Initialize();
            CreateTemplateCommand.Initialize(this);
            DTE = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
        }
    }
}
