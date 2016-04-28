using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace Persimmon.VisualStudio.TestExplorer
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [Guid(Constant.VisualStudioPkgIdString)]
    [ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]   // UICONTEXT_NoSolution
    [ProvideAutoLoad("F1536EF8-92EC-443C-9ED7-FDADF150DA82")]   // UICONTEXT_SolutionExists
    public sealed class TestAdapterPackage : Package
    {
        public TestAdapterPackage()
        {
            Trace.WriteLine("TestAdapterPackage: constructed.");

            Debug.Assert(false);
        }

        private Assembly LoadExtensionManager()
        {
            var dte = (DTE)this.GetService(typeof(DTE));
            var dteMajorVersion = int.Parse(dte.Version);
            var an = new AssemblyName("Microsoft.VisualStudio.ExtensionManager")
            {
                Version = new Version(dteMajorVersion, 0, 0),
                CultureInfo = CultureInfo.GetCultureInfo("neutral")
            };
            an.SetPublicKeyToken(new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a });
            return Assembly.Load(an);
        }

        private string GetInstallPath()
        {
            var assembly = this.LoadExtensionManager();
            var extensionManagerType = assembly.GetType(
                "Microsoft.VisualStudio.ExtensionManager.SVsExtensionManager");
            var extensionManager = this.GetService(extensionManagerType);
            var installedExtensionType = assembly.GetType(
                "Microsoft.VisualStudio.ExtensionManager.IInstalledExtension");
            var installPathProperty = installedExtensionType.GetProperty("InstallPath");
            return (string)installPathProperty.GetValue(extensionManager, null);
        }

        protected override void Initialize()
        {
            Trace.WriteLine("TestAdapterPackage: Initialize(): enter.");

            base.Initialize();

            var installPath = this.GetInstallPath();

            // TODO:reg gac

            Trace.WriteLine("TestAdapterPackage: Initialize(): exit.");
        }
    }
}
