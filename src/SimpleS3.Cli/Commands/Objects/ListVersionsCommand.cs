using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Genbox.SimpleS3.Core.Network.Responses.S3Types;
using McMaster.Extensions.CommandLineUtils;

namespace Genbox.SimpleS3.Cli.Commands.Objects
{
    [Command("listversions", Description = "List the object versions in a bucket")]
    internal class ListVersionsCommand : OnlineCommandBase
    {
        [Argument(0, Description = "Bucket name")]
        [Required]
        public string BucketName { get; set; } = null!;

        protected override async Task ExecuteAsync(CommandLineApplication app, CancellationToken token)
        {
            IAsyncEnumerator<S3ObjectVersion> list = ObjectManager.ListVersionsAsync(BucketName).GetAsyncEnumerator(token);

            bool hasData = await list.MoveNextAsync().ConfigureAwait(false);

            if (!hasData)
            {
                Console.WriteLine();
                Console.WriteLine("There were no object versions.");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("{0,-20}{1,-12}{2,-18}{3,-38}{4,-20}{5,-10}{6}", "Modified on", "Size", "Storage class", "ETag", "Owner", "Is latest", "Name");

                do
                {
                    S3ObjectVersion obj = list.Current;

                    string? ownerInfo = null;

                    if (obj.Owner != null)
                        ownerInfo = obj.Owner.Name;

                    Console.WriteLine("{0,-20}{1,-12}{2,-18}{3,-38}{4,-20}{5,-10}{6}", obj.LastModified!.Value.ToString("yyy-MM-dd hh:mm:ss", DateTimeFormatInfo.InvariantInfo), obj.Size, obj.StorageClass, obj.ETag!, ownerInfo, obj.IsLatest, obj.ObjectKey);
                } while (await list.MoveNextAsync().ConfigureAwait(false));
            }
        }
    }
}