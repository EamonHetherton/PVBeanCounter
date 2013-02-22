using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MackayFisher.Utilities;

namespace Conversations
{
    public class SMABluetooth_Converse : Converse
    {
        bool initRun;

        public SMABluetooth_Converse(IUtilityLog log)
            : base(log, null)
        {
            initRun = false;

            //Variable var;
            /*
            //var = new BluetoothAddressVar("ADDR"); // used to store the blooth address - value loaded before conversations start
            //Variables.Add(var);
            var = new CRC("CRC", this); // calculates and supplies a CRC when sending a message - calculates the expected CRC for receive messages
            Variables.Add(var);
            var = new TimeVar("TIME", this); // supplies the system time as a protocol variable
            Variables.Add(var);
            var = new StringVar("PASSWORD", this); // supplies a password as a protocol variable
            ((StringVar)var).Value = "0000";
            Variables.Add(var);
            var = new StringVar("TMPL", this); // marks an ???? section in the protocol
            Variables.Add(var);
            var = new StringVar("TMMI", this); // marks an ???? section in the protocol
            Variables.Add(var);
            var = new TimeVar("TIMEFROM1", this); // supplies the FROM time for data retrieval as a protocol variable
            Variables.Add(var);
            var = new TimeVar("TIMETO1", this); // supplies the FROM time for data retrieval as a protocol variable
            Variables.Add(var);

            ByteVar byteVar = new ByteVar("ADD2", 6, this);
            Variables.Add(byteVar);

            byteVar = new ByteVar("INVCODE", 1, this);
            //Byte[] byteVal = new Byte[1];
            //byteVal[0] = 0;
            //byteVar.SetBytes(byteVal, 0, 1); // set an initial value - used for read length only
            Variables.Add(byteVar);

            byteVar = new ByteVar("SIGNAL", 1, this); // receives signal strength
            Variables.Add(byteVar);

            byteVar = new ByteVar("UNKNOWN", 4, this); // marks an unknown section in the protocol
            byteVar.IsUnknown = true;
            Byte[] byteVal = new Byte[4];
            byteVal[0] = 0x3f;
            byteVal[1] = 0x10;
            byteVal[2] = 0xfb;
            byteVal[3] = 0x39;
            byteVar.SetBytes(ref byteVal, 0, 4); // set an initial value - used for read length only
            Variables.Add(byteVar);
             */
        }

        public void GetInverterYield()
        {
            if (!initRun)
                DoConversation("init");

            DoConversation("setup");
            DoConversation("getlivevalues");
        }
    }
}
