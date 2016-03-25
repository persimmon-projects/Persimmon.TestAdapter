namespace System

open System.Reflection
open System.Runtime.InteropServices

[<assembly: AssemblyCompany("persimmon-projects")>]
[<assembly: AssemblyProduct("Persimmon")>]
[<assembly: AssemblyCopyright("Copyright (c) 2016 persimmon-projects")>]
[<assembly: AssemblyTrademark("Persimmon")>]

#if DEBUG
[<assembly: AssemblyConfiguration("DEBUG")>]
#else
[<assembly: AssemblyConfiguration("RELEASE")>]
#endif

[<assembly: AssemblyVersion("1.0.3")>]
[<assembly: AssemblyFileVersion("1.0.3")>]
[<assembly: AssemblyInformationalVersion("1.0.3")>]

[<assembly: AssemblyTitleAttribute("Persimmon.VisualStudio.TestDiscoverer")>]
[<assembly: AssemblyDescriptionAttribute("")>]
[<assembly: GuidAttribute("D5478524-6779-4A0C-A056-50CF603CBA91")>]

do ()
