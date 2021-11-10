namespace Genbox.SimpleS3.Cli.Core.Helpers
{
    public static class RemotePathHelper
    {
        public static string Combine(params string?[] components)
        {
            return PathHelper.Combine('\\', '/', components);
        }

        public static string GetFileName(string path)
        {
            int slashIndex = path.LastIndexOf('/');
            return path.Substring(slashIndex);
        }
    }
}