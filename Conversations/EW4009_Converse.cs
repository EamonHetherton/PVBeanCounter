using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MackayFisher.Utilities;


namespace Conversations
{
    public class EW4009_Converse : Converse
    {
        public EW4009_Converse(IUtilityLog log)
            : base(log, null)
        {
            EndianConverter16Bit = new EndianConverter16Bit(EndianConverter.LittleEndian16Bit);            
        }
    }
}
