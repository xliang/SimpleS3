using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Genbox.SimpleS3.Core.Abstracts;
using Genbox.SimpleS3.Core.Abstracts.Region;
using Genbox.SimpleS3.Extensions.ProfileManager.Abstracts;
using Genbox.SimpleS3.Extensions.ProfileManager.Internal.Helpers;

namespace Genbox.SimpleS3.Extensions.ProfileManager
{
    public class ConsoleProfileSetup : IProfileSetup
    {
        private readonly IInputValidator _inputValidator;
        private readonly IProfileManager _profileManager;
        private readonly IRegionConverter _regionConverter;
        private readonly IRegionData _regionData;

        public ConsoleProfileSetup(IProfileManager profileManager, IInputValidator inputValidator, IRegionConverter regionConverter, IRegionData regionData)
        {
            _profileManager = profileManager;
            _inputValidator = inputValidator;
            _regionConverter = regionConverter;
            _regionData = regionData;
        }

        public IProfile? SetupProfile(string profileName, bool persist = true)
        {
            try
            {
                //Register cancel event to ensure the user can quit the wizard
                Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            start:
                string? enteredKeyId = GetKeyId();

                if (enteredKeyId == null)
                    return null;

                byte[] accessKey = GetAccessKey();

                IRegionInfo? region = GetRegion();

                if (region == null)
                    return null;

                Console.WriteLine();
                Console.WriteLine("Please confirm the following information:");
                Console.WriteLine("Key id: " + enteredKeyId);
                Console.WriteLine("Region: " + region.Code + " -- " + region.Name);
                Console.WriteLine();

                ConsoleKey key;

                do
                {
                    Console.WriteLine("Is it correct? Y/N");

                    key = Console.ReadKey(true).Key;
                } while (key != ConsoleKey.Y && key != ConsoleKey.N);

                if (key == ConsoleKey.N)
                    goto start;

                IProfile profile = _profileManager.CreateProfile(profileName, enteredKeyId, accessKey, region.Code, persist);

                if (persist)
                {
                    if (!string.IsNullOrEmpty(profile.Location))
                        Console.WriteLine("Successfully saved the profile to " + profile.Location);
                    else
                        Console.WriteLine("Successfully saved profile");
                }

                //Clear the access key from memory
                Array.Clear(accessKey, 0, accessKey.Length);
                return profile;
            }
            finally
            {
                Console.CancelKeyPress -= ConsoleOnCancelKeyPress;
            }
        }

        private void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = false;
        }

        private string? GetKeyId()
        {
            string? enteredKeyId;
            bool validKeyId = true;

            Console.WriteLine();
            Console.WriteLine("Enter your key id - Example: AKIAIOSFODNN7EXAMPLE");

            do
            {
                if (!validKeyId)
                    Console.Error.WriteLine("Invalid key id. Try again.");

                enteredKeyId = ConsoleHelper.ReadString();

                if (enteredKeyId == null)
                    return null;

                enteredKeyId = enteredKeyId.Trim();
            } while (!(validKeyId = _inputValidator.TryValidateKeyId(enteredKeyId, out _)));

            return enteredKeyId;
        }

        private byte[] GetAccessKey()
        {
            char[]? enteredAccessKey = null;
            byte[]? utf8AccessKey = null;
            bool validAccessKey = true;

            Console.WriteLine();
            Console.WriteLine("Enter your access key - Example: wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY");

            do
            {
                if (!validAccessKey)
                {
                    Console.Error.WriteLine("Invalid access key. Try again.");
                    Array.Clear(enteredAccessKey!, 0, enteredAccessKey!.Length);
                    Array.Clear(utf8AccessKey!, 0, utf8AccessKey!.Length);
                }

                enteredAccessKey = ConsoleHelper.ReadSecret(40);

                //Now we get the trim offsets in order to give it to UTF8.GetBytes()
                (int start, int end) = GetTrimOffsets(enteredAccessKey);

                utf8AccessKey = Encoding.UTF8.GetBytes(enteredAccessKey, start, end);

                //Clear the entered access key from memory
                Array.Clear(enteredAccessKey, 0, enteredAccessKey.Length);

            } while (!(validAccessKey = _inputValidator.TryValidateAccessKey(utf8AccessKey, out _)));

            return utf8AccessKey;
        }

        private IRegionInfo? GetRegion()
        {
            Console.WriteLine();
            Console.WriteLine("Choose the default region. You can choose it by index or region code");

            HashSet<string> validRegionId = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int counter = 0; //used for validation further down

            Console.WriteLine("{0,-8}{1,-20}{2}", "Index", "Region Code", "Region Name");
            foreach (IRegionInfo regionInfo in _regionData.GetRegions())
            {
                validRegionId.Add(regionInfo.Code);
                Console.WriteLine("{0,-8}{1,-20}{2}", Convert.ChangeType(regionInfo.EnumValue, typeof(int), NumberFormatInfo.InvariantInfo), regionInfo.Code, regionInfo.Name);

                counter++;
            }

            string? enteredRegion = ConsoleHelper.ReadString();

            if (enteredRegion != null)
            {
                if (int.TryParse(enteredRegion, out int index) && index >= 0 && index <= counter)
                    return _regionConverter.GetRegion(index);

                if (validRegionId.Contains(enteredRegion))
                    return _regionConverter.GetRegion(enteredRegion);
            }

            Console.Error.WriteLine("Invalid region. Try again.");

            return null;
        }

        private (int, int) GetTrimOffsets(char[] arr)
        {
            int end;
            int start;

            //Start from the beginning and stop when we hit a non-whitespace char
            for (start = 0; start < arr.Length;)
            {
                if (!char.IsWhiteSpace(arr[start]))
                    break;

                start++;
            }

            //Start from the end and stop when we hit a non-whitespace char
            for (end = arr.Length - 1; end >= start; end--)
            {
                if (!char.IsWhiteSpace(arr[end]))
                    break;
            }

            return (start, end + 1);
        }
    }
}