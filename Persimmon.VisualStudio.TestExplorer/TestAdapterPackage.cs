using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;

namespace Persimmon.VisualStudio.TestExplorer
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </remarks>
    // TRAP: PackageRegistration.RegisterUsing is no effect. Must use UseCodebase element into csproj.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [Guid(Constant.VisualStudioPkgIdString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
    public sealed class TestAdapterPackage : Package
    {
        private static readonly byte[] msPublicKeyToken_ = { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a };

        /// <summary>
        /// Default constructor of the package.
        /// </summary>
        /// <remarks>
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </remarks>
        public TestAdapterPackage()
        {
            Trace.WriteLine("TestAdapterPackage: constructed.");
        }
        private Assembly LoadExtensionManager()
        {
            var envDteName = new AssemblyName("EnvDTE")
            {
                Version = new Version(8, 0, 0, 0),
                CultureInfo = CultureInfo.InvariantCulture.Parent
            };
            envDteName.SetPublicKeyToken(msPublicKeyToken_);
            var envDteAssembly = Assembly.Load(envDteName);
            var dteType = envDteAssembly.GetType("EnvDTE.DTE");
            dynamic dte = this.GetService(dteType);

            double dteMajorVersion = double.Parse(dte.Version);
            var extensionManagerName = new AssemblyName("Microsoft.VisualStudio.ExtensionManager")
            {
                Version = new Version((int)dteMajorVersion, 0, 0, 0),
                CultureInfo = CultureInfo.InvariantCulture.Parent
            };
            extensionManagerName.SetPublicKeyToken(msPublicKeyToken_);

            return Assembly.Load(extensionManagerName);
        }

        private string GetInstallPath()
        {
            var extensionManagerAssembly = this.LoadExtensionManager();
            var extensionManagerType = extensionManagerAssembly.GetType(
                "Microsoft.VisualStudio.ExtensionManager.SVsExtensionManager");
            dynamic extensionManager = this.GetService(extensionManagerType);
            
            dynamic installedExtension = extensionManager.GetInstalledExtension(
                Constant.VisualStudioPkgIdString);

            var installedExtensionType = extensionManagerAssembly.GetType(
                "Microsoft.VisualStudio.ExtensionManager.IInstalledExtension");
            var installPathProperty = installedExtensionType.GetProperty("InstallPath");

            var installPath = (string)installPathProperty.GetValue(installedExtension, null);

            return installPath;
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
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
