using Genbox.SimpleS3.Core.Abstracts;
using Genbox.SimpleS3.Core.Abstracts.Enums;

namespace Genbox.SimpleS3.Cli.Core
{
    public class NullValidator : IInputValidator
    {
        public bool TryValidateKeyId(string? keyId, out ValidationStatus status)
        {
            status = ValidationStatus.Ok;
            return true;
        }

        public bool TryValidateAccessKey(byte[]? accessKey, out ValidationStatus status)
        {
            status = ValidationStatus.Ok;
            return true;
        }

        public bool TryValidateBucketName(string? bucketName, out ValidationStatus status)
        {
            status = ValidationStatus.Ok;
            return true;
        }

        public bool TryValidateObjectKey(string? objectKey, ObjectKeyValidationMode mode, out ValidationStatus status)
        {
            status = ValidationStatus.Ok;
            return true;
        }
    }
}