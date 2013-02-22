using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Conversations
{
    public interface IConverseCheckSum16
    {
        UInt16 GetCheckSum16(List<byte[]> message);
    }
}
