using System;
using System.IO;
using System.Reflection;

namespace Common.Extentions
{
    public static class PathHelper
    {
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                var finalPath = Path.GetDirectoryName(path);
                return finalPath;
            }
        }
    }
}
