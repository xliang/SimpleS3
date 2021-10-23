using System;
using Genbox.SimpleS3.Core.Abstracts.Response;

namespace Genbox.SimpleS3.Core.Common.Exceptions
{
    public class S3ResponseException : S3Exception
    {
        public S3ResponseException(IResponse response, string? message = null, Exception? innerException = null) : base(message, innerException)
        {
            Response = response;
        }

        public IResponse Response { get; }
    }
}