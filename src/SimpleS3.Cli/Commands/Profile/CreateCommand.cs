using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace Genbox.SimpleS3.Cli.Commands.Profile
{
    [Command(Description = "Create a new profile")]
    internal class CreateCommand : CommandBase
    {
        [Argument(0)]
        [Required]
        public string ProfileName { get; set; } = null!;

        protected override async Task ExecuteAsync(CommandLineApplication app, CancellationToken token)
        {
            Console.WriteLine("Successfully created " + ProfileName);
        }
    }
}