using System.Collections.Generic;
using Genbox.ProviderTests;
using Genbox.SimpleS3.Cli.Core.Enums;
using Genbox.SimpleS3.Cli.Core.Helpers;
using Genbox.SimpleS3.Cli.Core.Managers;
using Genbox.SimpleS3.Core.Abstracts;
using Genbox.SimpleS3.Core.Network.Responses.Objects;
using Genbox.SimpleS3.Utility.Shared;
using System.IO;
using System.Threading.Tasks;
using Genbox.SimpleS3.Cli.Core.Structs;
using Xunit;

namespace Genbox.SimpleS3.Cli.Core.Tests
{
    public class ObjectTests
    {
        [Theory]
        [MultipleProviders(S3Provider.AmazonS3,
            new[] { "Resources\\data-src.txt", "s3://{0}/Remote/data-dst.txt" },
            new[] { "Resources\\data-src.txt", "s3://{0}/Remote/Subfolder/" },
            new[] { "Resources\\Subfolder\\", "s3://{0}/Remote/Subfolder/" },
            new[] { "s3://{0}/Remote/data-dst.txt", "Local\\data-src.txt" },
            new[] { "s3://{0}/Remote/data-dst.txt", "Local\\Subfolder\\" },
            new[] { "s3://{0}/Remote/Subfolder/", "Local\\Subfolder\\" })]
        public async Task Copy(S3Provider _, string bucket, ISimpleClient client, string[] input)
        {
            //Insert the bucket name into the source and destination templates
            string source = string.Format(input[0], bucket);
            string destination = string.Format(input[1], bucket);

            IAsyncEnumerable<CompletedAction> results = new ObjectManager(client, new DefaultFileProvider()).CopyAsync(source, destination);

            //Check if the files are really in destination
            await foreach (CompletedAction completedAction in results)
            {
                if (ResourceHelper.TryParsePath(completedAction.Destination, out PathData data))
                {
                    if (data.LocationType == LocationType.Local)
                    {
                        Assert.True(File.Exists(data.Resource));
                    }
                    else
                    {
                        HeadObjectResponse resp = await client.HeadObjectAsync(data.Bucket, data.Resource);
                        Assert.Equal(200, resp.StatusCode);
                    }
                }
            }
        }
    }
}