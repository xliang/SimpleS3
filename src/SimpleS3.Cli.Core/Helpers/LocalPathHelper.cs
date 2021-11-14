using System.IO;

namespace Genbox.SimpleS3.Cli.Core.Helpers
{
    public static class LocalPathHelper
    {
        public static string GetFileName(string path)
        {
            return Path.GetFileName(path);
        }

        public static string? GetDirectoryName(string path)
        {
            return Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path)));
        }

        public static string Combine(params string[] components)
        {
            return PathHelper.Combine('/', '\\', components);
        }

        public static string GetRelativePath(string relativeTo, string path)
        {
            return Path.GetRelativePath(relativeTo, path);
        }
    }
}