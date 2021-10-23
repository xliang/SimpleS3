using System.Text;
using Genbox.SimpleS3.Core.Abstracts;
using Genbox.SimpleS3.Core.Abstracts.Request;

namespace Genbox.SimpleS3.Cli.Core
{
    public class NullUrlBuilder : IUrlBuilder
    {
        public void AppendHost<TReq>(StringBuilder sb, TReq request) where TReq : IRequest { }

        public void AppendUrl<TReq>(StringBuilder sb, TReq request) where TReq : IRequest { }
    }
}