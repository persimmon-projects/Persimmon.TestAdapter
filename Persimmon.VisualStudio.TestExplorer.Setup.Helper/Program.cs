using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

using Microsoft.Win32;

namespace Persimmon.VisualStudio.TestExplorer.Setup.Helper
{
    /// <summary>
    /// VSIX automatic installer.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Execution modes.
        /// </summary>
        private enum ExecutionModes
        {
            /// <summary>
            /// Install.
            /// </summary>
            Install,

            /// <summary>
            /// Uninstall.
            /// </summary>
            Uninstall
        }

        /// <summary>
        /// Install target VSIXs.
        /// </summary>
        /// <param name="vsixInstallerPath">VSIXInstaller.exe path</param>
        /// <param name="vsixPaths">VSIX package paths</param>
        private static void InstallVsixs(string vsixInstallerPath, IEnumerable<string> vsixPaths)
        {
            foreach (var arguments in vsixPaths.Select(path =>
                string.Format("/quiet /admin \"{0}\"", path)))
            {
                Trace.WriteLine(string.Format(
                    "Persimmon.VisualStudio.TestExplorer: Install, Arguments=\"{0}\"",
                    arguments));

                var psi = new ProcessStartInfo(vsixInstallerPath, arguments)
                {
                    UseShellExecute = false
                };
                using (var process = Process.Start(psi))
                {
                    process.WaitForExit();
                }
            }
        }

        /// <summary>
        /// Retreive VSIX package id from VSIX package file.
        /// </summary>
        /// <param name="vsixPath">VSIX package path</param>
        /// <returns>VSIX package id</returns>
        private static string GetVsixIdentityFromPackage(string vsixPath)
        {
            using (var stream = File.OpenRead(vsixPath))
            {
                var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                var manifestEntry = zip.Entries.First(entry => entry.Name == "extension.vsixmanifest");
                using (var manifestStream = manifestEntry.Open())
                {
                    var packageManifest = XElement.Load(manifestStream);

                    XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";
                    return packageManifest.
                        Elements(ns + "Metadata").
                        Elements(ns + "Identity").
                        Attributes("Id").
                        First(attribute => attribute.Name == "Id").
                        Value;
                }
            }
        }

        /// <summary>
        /// Uninstall target VSIXs.
        /// </summary>
        /// <param name="vsixInstallerPath">VSIXInstaller.exe path</param>
        /// <param name="vsixPaths">VSIX package paths</param>
        private static void UninstallVsixs(string vsixInstallerPath, IEnumerable<string> vsixPaths)
        {
            foreach (var arguments in vsixPaths.Select(path =>
                string.Format("/admin /uninstall:{0}", GetVsixIdentityFromPackage(path))))
            {
                Trace.WriteLine(string.Format(
                    "Persimmon.VisualStudio.TestExplorer: Install, Arguments=\"{0}\"",
                    arguments));

                var psi = new ProcessStartInfo(vsixInstallerPath, arguments)
                {
                    UseShellExecute = false
                };
                using (var process = Process.Start(psi))
                {
                    process.WaitForExit();
                }
            }
        }

        /// <summary>
        /// Iterate VSIXInstaller paths.
        /// </summary>
        /// <returns>Enumerator (VS version, path)</returns>
        private static IEnumerable<KeyValuePair<double, string>> GetVsixInstallerPaths()
        {
            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (var vs = hklm.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio", false))
                {
                    foreach (var subKeyName in vs.GetSubKeyNames())
                    {
                        double version;
                        if (double.TryParse(subKeyName, out version))
                        {
                            using (var subKey = vs.OpenSubKey(subKeyName, false))
                            {
                                var installDir = subKey.GetValue("InstallDir") as string;
                                if (installDir != null)
                                {
                                    var path = Path.Combine(installDir, "VSIXInstaller.exe");
                                    if (File.Exists(path))
                                    {
                                        yield return new KeyValuePair<double, string>(version, path);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Arguments (arg0: Execution mode)</param>
        public static void Main(string[] args)
        {
            // Mode
            var mode = (ExecutionModes) Enum.Parse(typeof(ExecutionModes), args[0]);

            // Target VSIX package paths
            var vsixBasePath = Path.GetFullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            var vsixPaths = Directory.EnumerateFiles(vsixBasePath, "*.vsix");

            // Select newest VSIXInstaller.exe
            var vsixInstallerPath = GetVsixInstallerPaths().
                OrderByDescending(entry => entry.Key).
                First().
                Value;

            Trace.WriteLine(string.Format(
                "Persimmon.VisualStudio.TestExplorer: DetectInstaller=\"{0}\"",
                vsixInstallerPath));
            Trace.WriteLine(string.Format(
                "Persimmon.VisualStudio.TestExplorer: Targets={0}",
                string.Join(",", vsixPaths.Select(path => string.Format("\"{0}\"", path)))));

            if (mode == ExecutionModes.Install)
            {
                InstallVsixs(vsixInstallerPath, vsixPaths);
            }
            else
            {
                UninstallVsixs(vsixInstallerPath, vsixPaths);
            }
        }
    }
}
