using System;

namespace Persimmon.TestAdapter
{
    internal static class Constant
    {
        public const string VisualStudioPkgIdString = "c35f7015-465a-456a-801e-210ab907210d";

        public const string ExtensionUriString = "executor://persimmon.visualstudio.testexplorer";
        public static readonly Uri ExtensionUri = new Uri(ExtensionUriString);
    }
}
