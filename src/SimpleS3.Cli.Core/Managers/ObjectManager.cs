using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using Genbox.SimpleS3.Cli.Core.Enums;
using Genbox.SimpleS3.Cli.Core.Exceptions;
using Genbox.SimpleS3.Cli.Core.Helpers;
using Genbox.SimpleS3.Core.Abstracts;
using Genbox.SimpleS3.Core.Extensions;
using Genbox.SimpleS3.Core.Internals.Helpers;
using Genbox.SimpleS3.Core.Network.Responses.Objects;
using Genbox.SimpleS3.Core.Network.Responses.S3Types;

namespace Genbox.SimpleS3.Cli.Core.Managers
{
    public class ObjectManager
    {
        private readonly ISimpleClient _client;

        public ObjectManager(ISimpleClient client)
        {
            _client = client;
        }

        public async Task CopyAsync(string source, string destination)
        {
            if (!ResourceHelper.TryParsePath(source, out (string bucket, string resource, LocationType locationType, ResourceType resourceType) src))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidPath, source);

            if (!ResourceHelper.TryParsePath(destination, out (string bucket, string resource, LocationType locationType, ResourceType resourceType) dst))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidPath, destination);

            if (src.locationType == LocationType.Local && dst.locationType == LocationType.Remote)
            {
                switch (src.resourceType)
                {
                    case ResourceType.File:
                        {
                            string objectKey;

                            switch (dst.resourceType)
                            {
                                //Source: Local file - Destination: Remote file
                                case ResourceType.File:
                                    objectKey = dst.resource;
                                    break;

                                //Source: Local file - Destination: Remote directory
                                case ResourceType.Directory:
                                    objectKey = RemotePathHelper.Combine(dst.resource, LocalPathHelper.GetFileName(src.resource));
                                    break;

                                //We don't support expand on the destination
                                default:
                                    throw new CommandException(ErrorType.Argument, CliErrorMessages.OperationNotSupported, source);
                            }

                            await RequestHelper.ExecuteRequestAsync(_client, c => c.PutObjectFileAsync(dst.bucket, objectKey, src.resource)).ConfigureAwait(false);
                            return;
                        }
                    case ResourceType.Directory:
                        {
                            switch (dst.resourceType)
                            {
                                //Source: Local directory - Destination: remote directory
                                case ResourceType.Directory:
                                    foreach (string file in Directory.GetFiles(src.resource))
                                    {
                                        string? directory = LocalPathHelper.GetDirectoryName(file);
                                        string name = LocalPathHelper.GetFileName(file);
                                        string objectKey = RemotePathHelper.Combine(dst.resource, directory, name);

                                        await RequestHelper.ExecuteRequestAsync(_client, c => c.PutObjectFileAsync(dst.bucket, objectKey, file)).ConfigureAwait(false);
                                    }

                                    return;

                                //We don't support files or expand on the destination
                                default:
                                    throw new CommandException(ErrorType.Argument, CliErrorMessages.OperationNotSupported, source);
                            }
                        }
                    default:
                        throw new CommandException(ErrorType.Argument, CliErrorMessages.OperationNotSupported, source);
                }
            }

            if (src.locationType == LocationType.Remote && dst.locationType == LocationType.Local)
            {
                switch (src.resourceType)
                {
                    case ResourceType.File:
                        {
                            string localFile;

                            switch (dst.resourceType)
                            {
                                //Source: remote file - Destination: local file
                                case ResourceType.File:
                                    localFile = dst.resource;
                                    break;

                                //Source: remote file - Destination: local directory
                                case ResourceType.Directory:
                                    localFile = LocalPathHelper.Combine(dst.resource, RemotePathHelper.GetFileName(src.resource));
                                    break;

                                //We don't support expand on the destination
                                default:
                                    throw new CommandException(ErrorType.Argument, CliErrorMessages.OperationNotSupported, source);
                            }

                            GetObjectResponse resp = await RequestHelper.ExecuteRequestAsync(_client, c => c.GetObjectAsync(src.bucket, src.resource)).ConfigureAwait(false);
                            await resp.Content.CopyToFileAsync(localFile);
                            return;
                        }
                    case ResourceType.Directory:
                        {
                            switch (dst.resourceType)
                            {
                                //Source: remote directory - Destination: local directory
                                case ResourceType.Directory:
                                    await foreach (S3Object s3Object in RequestHelper.ExecuteAsyncEnumerable(_client, c => c.ListAllObjectsAsync(src.bucket, config: req =>
                                    {
                                        req.Prefix = src.resource;
                                    })))
                                    {
                                        string destFolder = dst.resource;
                                        string destFile = LocalPathHelper.Combine(destFolder, dst.resource, s3Object.ObjectKey);

                                        GetObjectResponse resp = await RequestHelper.ExecuteRequestAsync(_client, c => c.GetObjectAsync(src.bucket, s3Object.ObjectKey)).ConfigureAwait(false);
                                        await resp.Content.CopyToFileAsync(destFile).ConfigureAwait(false);
                                    }

                                    return;

                                //We don't support file or expand on the destination
                                default:
                                    throw new CommandException(ErrorType.Argument, CliErrorMessages.OperationNotSupported, source);
                            }
                        }
                    default:
                        throw new CommandException(ErrorType.Argument, CliErrorMessages.OperationNotSupported, source);
                }
            }
        }

        public async Task MoveAsync(string source, string destination)
        {
            await CopyAsync(source, destination).ConfigureAwait(false);
            IAsyncEnumerable<S3DeleteError> errors = DeleteAsync(source, false, false);

            await foreach (S3DeleteError error in errors)
            {
                throw new CommandException(ErrorType.Error, CliErrorMessages.FailedToDelete, error.ObjectKey);
            }
        }

        public async IAsyncEnumerable<S3DeleteError> DeleteAsync(string path, bool includeVersions, bool force)
        {
            if (!ResourceHelper.TryParsePath(path, out (string bucket, string resource, LocationType locationType, ResourceType resourceType) parsed))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidPath, path);

            if (parsed.locationType != LocationType.Remote)
                throw new CommandException(ErrorType.Argument, CliErrorMessages.S3SyntaxRequired, path);

            switch (parsed.resourceType)
            {
                case ResourceType.File:
                    await RequestHelper.ExecuteRequestAsync(_client, c => c.DeleteObjectAsync(parsed.bucket, parsed.resource)).ConfigureAwait(false);
                    break;
                case ResourceType.Directory:
                    IAsyncEnumerable<S3DeleteError> errors = RequestHelper.ExecuteAsyncEnumerable(_client, c => includeVersions ? c.DeleteAllObjectVersionsAsync(parsed.bucket, parsed.resource) : c.DeleteAllObjectsAsync(parsed.bucket, parsed.resource));

                    await foreach (S3DeleteError error in errors)
                    {
                        yield return error;
                    }
                    break;
                default:
                    throw new CommandException(ErrorType.Argument, CliErrorMessages.OperationNotSupported, path);
            }
        }

        public IAsyncEnumerable<S3Object> ListAsync(string path, bool includeOwner)
        {
            if (!ResourceHelper.TryParsePath(path, out (string bucket, string resource, LocationType locationType, ResourceType resourceType) parsed))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidPath, path);

            if (parsed.locationType != LocationType.Remote)
                throw new CommandException(ErrorType.Argument, CliErrorMessages.S3SyntaxRequired, path);

            return RequestHelper.ExecuteAsyncEnumerable(_client, c => c.ListAllObjectsAsync(parsed.bucket, req =>
            {
                if (includeOwner)
                    req.FetchOwner = true;

                if (parsed.resource != string.Empty)
                    req.Prefix = parsed.resource;
            }));
        }

        public IAsyncEnumerable<S3ObjectVersion> ListVersionsAsync(string path)
        {
            if (!ResourceHelper.TryParsePath(path, out (string bucket, string resource, LocationType locationType, ResourceType resourceType) parsed))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidPath, path);

            if (parsed.locationType != LocationType.Remote)
                throw new CommandException(ErrorType.Argument, CliErrorMessages.S3SyntaxRequired, path);

            return RequestHelper.ExecuteAsyncEnumerable(_client, c => c.ListAllObjectVersionsAsync(path, req =>
            {
                if (parsed.resource != string.Empty)
                    req.Prefix = parsed.resource;
            }));
        }

        public async Task SyncAsync(string source, string destination, int concurrentUploads)
        {
            if (!ResourceHelper.TryParsePath(source, out (string bucket, string resource, LocationType locationType, ResourceType resourceType) src))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidPath, source);

            if (!ResourceHelper.TryParsePath(destination, out (string bucket, string resource, LocationType locationType, ResourceType resourceType) dst))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidPath, destination);

            if (src.resourceType != ResourceType.Directory)
                throw new CommandException(ErrorType.Argument, CliErrorMessages.ArgumentMustBeDirectory, source);

            if (dst.resourceType != ResourceType.Directory)
                throw new CommandException(ErrorType.Argument, CliErrorMessages.ArgumentMustBeDirectory, destination);

            //Generate file list of source
            List<FileDateInfo> sourceList;

            if (src.locationType == LocationType.Local)
                sourceList = GetLocal(src.bucket, src.resource).ToList();
            else if (src.locationType == LocationType.Remote)
                sourceList = await GetRemote(src.bucket, src.resource).ToListAsync();
            else
                throw new CommandException(ErrorType.Error, CliErrorMessages.ArgumentOutOfRange, "locationType");

            //Generate file list of destination. It is a dictionary for fast lookup.
            Dictionary<string, FileDateInfo> destinationList;

            if (dst.locationType == LocationType.Local)
                destinationList = GetLocal(dst.bucket, dst.resource).ToDictionary(x => x.ComparisonKey, x => x);
            else if (dst.locationType == LocationType.Remote)
                destinationList = await GetRemote(dst.bucket, dst.resource).ToDictionaryAsync(x => x.ComparisonKey, x => x);
            else
                throw new CommandException(ErrorType.Error, CliErrorMessages.ArgumentOutOfRange, "locationType");

            //We use 3 lists instead of 1 with a state for each file to avoid having to do filter operations on it later.
            //We also use index references here to avoid having to copy the structs.
            List<int> newFiles = new List<int>();
            List<int> modifiedFiles = new List<int>();

            //We use source as the authority
            for (int i = 0; i < sourceList.Count; i++)
            {
                FileDateInfo srcFile = sourceList[i];
                string srcKey = srcFile.ComparisonKey;

                if (destinationList.TryGetValue(srcKey, out FileDateInfo dstFile))
                {
                    //The destination had the source file. Determine if it is modified.
                    if (srcFile.LastModified > dstFile.LastModified)
                        modifiedFiles.Add(i);

                    destinationList.Remove(srcKey);
                }
                else
                {
                    //The file does not exist in destination
                    newFiles.Add(i);
                }
            }

            //What is left in destinationList at this point are files that are not in source.
            //We remove files first as to not use extra disk space by uploading new files first.

            if (dst.locationType == LocationType.Local)
            {
                //delete
                if (destinationList.Count > 0)
                    foreach (KeyValuePair<string, FileDateInfo> info in destinationList)
                    {
                        File.Delete(info.Value.Filename);
                    }

                //modified
                if (modifiedFiles.Count > 0)
                    await ParallelHelper.ExecuteAsync(modifiedFiles, async (i, token) =>
                    {
                        GetObjectResponse resp = await _client.GetObjectAsync(src.bucket, RemotePathHelper.Combine(src.resource, sourceList[i].ComparisonKey), token: token);
                        await resp.Content.CopyToFileAsync(LocalPathHelper.Combine(dst.bucket, dst.resource, sourceList[i].ComparisonKey));
                    }, concurrentUploads);

                //new
                if (newFiles.Count > 0)
                    await ParallelHelper.ExecuteAsync(newFiles, async (i, token) =>
                    {
                        GetObjectResponse resp = await _client.GetObjectAsync(src.bucket, RemotePathHelper.Combine(src.resource, sourceList[i].ComparisonKey), token: token);
                        await resp.Content.CopyToFileAsync(LocalPathHelper.Combine(dst.bucket, dst.resource, sourceList[i].ComparisonKey));
                    }, concurrentUploads);
            }
            else if (dst.locationType == LocationType.Remote)
            {
                //delete
                if (destinationList.Count > 0)
                    await _client.DeleteObjectsAsync(dst.bucket, destinationList.Select(x => RemotePathHelper.Combine(dst.resource, x.Value.ComparisonKey)));

                //modified
                if (modifiedFiles.Count > 0)
                    await ParallelHelper.ExecuteAsync(modifiedFiles, (i, token) => _client.PutObjectFileAsync(dst.bucket, RemotePathHelper.Combine(dst.resource, sourceList[i].ComparisonKey), sourceList[i].Filename, token: token), concurrentUploads);

                //new
                if (newFiles.Count > 0)
                    await ParallelHelper.ExecuteAsync(newFiles, (i, token) => _client.PutObjectFileAsync(dst.bucket, RemotePathHelper.Combine(dst.resource, sourceList[i].ComparisonKey), sourceList[i].Filename, token: token), concurrentUploads);
            }
        }

        private async IAsyncEnumerable<FileDateInfo> GetRemote(string bucket, string resource)
        {
            await foreach (S3Object obj in _client.ListAllObjectsAsync(bucket, req => req.Prefix = resource))
            {
                yield return new FileDateInfo(obj.ObjectKey.Remove(0, resource.Length), obj.ObjectKey, obj.LastModified!.Value);
            }
        }

        private IEnumerable<FileDateInfo> GetLocal(string bucket, string resource)
        {
            string fullPath = LocalPathHelper.Combine(bucket, resource);

            FileSystemEnumerable<FileDateInfo> enu = new FileSystemEnumerable<FileDateInfo>(fullPath, (ref FileSystemEntry entry) =>
            {
                string path = entry.ToSpecifiedFullPath();
                return new FileDateInfo(path.Remove(0, fullPath.Length), path, entry.LastWriteTimeUtc);
            }, new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.Offline | FileAttributes.ReparsePoint,
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            })
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory
            };

            return enu;
        }

        private readonly struct FileDateInfo
        {
            public FileDateInfo(string comparisonKey, string filename, DateTimeOffset lastModified)
            {
                ComparisonKey = comparisonKey;
                Filename = filename;
                LastModified = lastModified;
            }

            public string ComparisonKey { get; }

            public string Filename { get; }

            public DateTimeOffset LastModified { get; }
        }
    }
}