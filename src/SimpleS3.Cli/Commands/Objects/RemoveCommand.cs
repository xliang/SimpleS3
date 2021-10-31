using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Genbox.SimpleS3.Core.Network.Responses.S3Types;
using McMaster.Extensions.CommandLineUtils;

namespace Genbox.SimpleS3.Cli.Commands.Objects
{
    [Command("rm", Description = "Deletes one or more objects")]
    internal class RemoveCommand : OnlineCommandBase
    {
        [Argument(0, Description = "The object you want to delete. E.g. s3://bucket/object or s3://bucket/prefix/ to delete a whole prefix")]
        [Required]
        public string Resource { get; set; } = null!;

        [Option("-i|--include-versions", Description = "Also remove all versions of objects")]
        public bool IncludeVersions { get; set; }

        [Option("-f|--force", Description = "Force removal of resources")]
        public bool Force { get; set; }

        protected override async Task ExecuteAsync(CommandLineApplication app, CancellationToken token)
        {
            await base.ExecuteAsync(app, token);

            IAsyncEnumerable<S3DeleteError> errors = ObjectManager.DeleteAsync(Resource, IncludeVersions, Force);

            bool hasError = false;

            await foreach (S3DeleteError error in errors.WithCancellation(token))
            {
                hasError = true;
                Console.WriteLine("Failed to delete " + error.ObjectKey);
            }

            if (!hasError)
                Console.WriteLine($"Successfully deleted {Resource}");
        }
    }
}