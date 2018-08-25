using System;

namespace Persimmon.TestAdapter
{
    internal static class Constant
    {
        public const string ExtensionUriString = "executor://persimmon.testadapter";
        public static readonly Uri ExtensionUri = new Uri(ExtensionUriString);
    }
}
