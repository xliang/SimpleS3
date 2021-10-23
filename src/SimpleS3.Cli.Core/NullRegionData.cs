using System.Collections.Generic;
using Genbox.SimpleS3.Core.Abstracts.Region;

namespace Genbox.SimpleS3.Cli.Core
{
    public class NullRegionData : IRegionData
    {
        public IEnumerable<IRegionInfo> GetRegions()
        {
            yield break;
        }
    }
}