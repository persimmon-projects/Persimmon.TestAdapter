namespace System

open System.Reflection
open System.Runtime.InteropServices

[<assembly: AssemblyCompany("persimmon-projects")>]
[<assembly: AssemblyProduct("Persimmon 2.0")>]
[<assembly: AssemblyCopyright("Copyright (c) 2016 persimmon-projects")>]
[<assembly: AssemblyTrademark("Persimmon")>]

#if DEBUG
[<assembly: AssemblyConfiguration("DEBUG")>]
#else
[<assembly: AssemblyConfiguration("RELEASE")>]
#endif

[<assembly: AssemblyTitle("Persimmon.TestDiscoverer")>]
[<assembly: AssemblyDescription("Visual Studio test explorer for Persimmon 2.0 (Discoverer assembly)")>]
[<assembly: Guid("D5478524-6779-4A0C-A056-50CF603CBA91")>]

do ()
