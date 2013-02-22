using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Algorithms;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;
using Conversations;

namespace Device
{
    public class CompositeAlgorithm_xml : DeviceAlgorithm
    {
        public DynamicByteVar MessageData { get; private set; }
        // Add String Variables below

        public String Message { get; private set; }
        
        // public void SetMessage(string value) { Message = value; }
        
        // End String Variables

        // Add Numeric Variables below


        // End Numeric Variables

        // Add Bytes Variables below


        // End Bytes Variables


        public CompositeAlgorithm_xml(AlgorithmParams algorithmParams)
            : base(algorithmParams)
        {
        }

        public CompositeAlgorithm_xml(DeviceManagerDeviceSettings deviceSettings, Protocol protocol, ErrorLogger errorLogger)
            : base(deviceSettings, protocol, errorLogger)
        {
        }

        protected override void LoadVariables()
        {
            MessageData = (DynamicByteVar)Params.Protocol.GetSessionVariable("Message", null);
        }

        public override void ClearAttributes()
        {
            Message = "";
        }

        public bool ExtractReading(bool dbWrite, ref bool alarmFound, ref bool errorFound)
        {
            bool res = false;
            if (FaultDetected)
                return res;

            String stage = "Reading";
            try
            {
                res = LoadBlockType("Reading", true, true, ref alarmFound, ref errorFound);
                Message = MessageData.ToString();
            }
            catch (Exception e)
            {
                LogMessage("DoExtractReadings - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }

            return res;
        }

    }
}
