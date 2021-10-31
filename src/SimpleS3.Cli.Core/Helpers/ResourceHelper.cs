using System;
using System.IO;
using Genbox.SimpleS3.Cli.Core.Enums;
using Genbox.SimpleS3.Core.Common.Extensions;

namespace Genbox.SimpleS3.Cli.Core.Helpers
{
    public static class ResourceHelper
    {
        public static bool TryParsePath(string path, out (string bucket, string resource, LocationType locationType, ResourceType resourceType) data)
        {
            if (string.IsNullOrEmpty(path))
            {
                data = default;
                return false;
            }

            LocationType locationType = path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase) ? LocationType.Remote : LocationType.Local;
            ResourceType resourceType;

            if (locationType == LocationType.Local)
            {
                if (path.Contains('*'))
                    resourceType = ResourceType.Expand;
                else
                {
                    if (Directory.Exists(path) || File.Exists(path))
                    {
                        FileAttributes attr = File.GetAttributes(path);
                        resourceType = attr.HasFlag(FileAttributes.Directory) ? ResourceType.Directory : ResourceType.File;
                    }
                    else
                    {
                        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
                            resourceType = ResourceType.Directory;
                        else
                            resourceType = ResourceType.File;
                    }
                }

                data = (string.Empty, path, locationType, resourceType);
            }
            else
            {
                int indexOfSlash = path.IndexOf('/', 5);

                string parsedResource;
                if (indexOfSlash != -1)
                {
                    parsedResource = path.Substring(indexOfSlash + 1);

                    if (parsedResource.EndsWith('*'))
                    {
                        resourceType = ResourceType.Expand;
                        parsedResource = parsedResource.TrimEnd('*');
                    }
                    else if (parsedResource.EndsWith('/') || parsedResource.Length == 0)
                        resourceType = ResourceType.Directory;
                    else
                        resourceType = ResourceType.File;
                }
                else
                {
                    indexOfSlash = path.Length;
                    parsedResource = string.Empty;
                    resourceType = ResourceType.Directory;
                }

                string parsedBucket = path.Substring(5, indexOfSlash - 5);

                data = (parsedBucket, parsedResource, locationType, resourceType);
            }

            return true;
        }
    }
}