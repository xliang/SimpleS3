using Genbox.SimpleS3.Core.Abstracts.Response;
using Genbox.SimpleS3.Core.Common.Exceptions;

namespace Genbox.SimpleS3.Core.Extensions
{
    public static class ResponseExtensions
    {
        public static void ThrowIfNotSuccess(this IResponse response)
        {
            if (!response.IsSuccess)
                throw new S3ResponseException(response);
        }
    }
}
