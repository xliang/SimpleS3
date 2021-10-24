using System;
using Genbox.SimpleS3.Core.Common.Marshal;
using Genbox.SimpleS3.Core.Enums;
using Genbox.SimpleS3.Core.Network.Responses.Interfaces;

namespace Genbox.SimpleS3.Core.Network.Responses.S3Types
{
    public class S3ObjectVersion : IHasObjectKey, IHasVersionId, IHasETag, IHasLastModified
    {
        public S3ObjectVersion(string objectKey, string? versionId, bool isLatest, DateTimeOffset lastModified, string etag, int size, S3Identity? owner, StorageClass storageClass)
        {
            ObjectKey = objectKey;
            VersionId = versionId;
            IsLatest = isLatest;
            LastModified = lastModified;
            ETag = etag;
            Size = size;
            Owner = owner;
            StorageClass = storageClass;
        }

        public bool IsLatest { get; }
        public string ObjectKey { get; internal set; }
        public DateTimeOffset? LastModified { get; }
        public S3Identity? Owner { get; }
        public int Size { get; }
        public StorageClass StorageClass { get; }
        public string? VersionId { get; }
        public string? ETag { get; }
    }
}