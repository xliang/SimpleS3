using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Genbox.SimpleS3.Cli.Core.Enums;
using Genbox.SimpleS3.Cli.Core.Exceptions;
using Genbox.SimpleS3.Cli.Core.Helpers;
using Genbox.SimpleS3.Core.Abstracts;
using Genbox.SimpleS3.Core.Common.Validation;
using Genbox.SimpleS3.Core.Extensions;
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
            if (!ResourceHelper.TryParseResource(source, out (string? bucket, string resource, LocationType locationType, ResourceType resourceType) src))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidResource, source);

            if (!ResourceHelper.TryParseResource(destination, out (string? bucket, string resource, LocationType locationType, ResourceType resourceType) dst))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidResource, destination);

            if (src.locationType == LocationType.Local && dst.locationType == LocationType.Remote)
            {
                if (dst.bucket == null)
                    throw new CommandException(ErrorType.Argument, CliErrorMessages.BucketIsRequired, destination);

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
                if (src.bucket == null)
                    throw new CommandException(ErrorType.Argument, CliErrorMessages.BucketIsRequired, source);

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

        public async IAsyncEnumerable<S3DeleteError> DeleteAsync(string resource, bool includeVersions, bool force)
        {
            if (!ResourceHelper.TryParseResource(resource, out (string? bucket, string resource, LocationType locationType, ResourceType resourceType) parsed))
                throw new CommandException(ErrorType.Argument, CliErrorMessages.InvalidResource, resource);

            if (parsed.bucket == null)
                throw new CommandException(ErrorType.Argument, CliErrorMessages.BucketIsRequired, resource);

            if (parsed.locationType == LocationType.Local)
            {
                switch (parsed.resourceType)
                {
                    case ResourceType.File:
                        File.Delete(parsed.resource);
                        break;
                    case ResourceType.Directory:
                        Directory.Delete(parsed.resource, true);
                        break;
                    default:
                        throw new CommandException(ErrorType.Argument, CliErrorMessages.ArgumentOutOfRange, parsed.resourceType.ToString());
                }
            }
            else
            {
                switch (parsed.resourceType)
                {
                    case ResourceType.File:
                        await RequestHelper.ExecuteRequestAsync(_client, c => c.DeleteObjectAsync(parsed.bucket, parsed.resource)).ConfigureAwait(false);
                        break;
                    case ResourceType.Directory:
                        IAsyncEnumerable<S3DeleteError> errors =  RequestHelper.ExecuteAsyncEnumerable(_client, c => includeVersions ? c.DeleteAllObjectVersionsAsync(parsed.bucket, parsed.resource) : c.DeleteAllObjectsAsync(parsed.bucket, parsed.resource));

                        await foreach (S3DeleteError error in errors)
                        {
                            yield return error;
                        }
                        break;
                    default:
                        throw new CommandException(ErrorType.Argument, CliErrorMessages.OperationNotSupported, resource);
                }
            }
        }

        public IAsyncEnumerable<S3Object> ListAsync(string bucketName, bool includeOwner)
        {
            Validator.RequireNotNullOrEmpty(bucketName, nameof(bucketName));

            return RequestHelper.ExecuteAsyncEnumerable(_client, c => c.ListAllObjectsAsync(bucketName, includeOwner));
        }

        public IAsyncEnumerable<S3ObjectVersion> ListVersionsAsync(string bucketName)
        {
            Validator.RequireNotNullOrEmpty(bucketName, nameof(bucketName));

            return RequestHelper.ExecuteAsyncEnumerable(_client, c => c.ListAllObjectVersionsAsync(bucketName));
        }
    }
}