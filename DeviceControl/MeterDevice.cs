using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVBCInterfaces;
using PVSettings;
using Algorithms;
using DeviceDataRecorders;

namespace Device
{
    public abstract class MeterDevice<TLiveRecord, THistRecord, TDeviceParams> : PassiveDevice 
    {  
        public MeterDevice(DeviceControl.DeviceManagerBase deviceManager, DeviceManagerDeviceSettings deviceSettings,  
            string manufacturer, string model, string serialNo)
            : base(deviceManager, deviceSettings, manufacturer, model, serialNo)
        {
            //Feature = deviceSettings.Feature;
            DeviceIdentifier = deviceSettings.SerialNo;
        }

        public abstract bool ProcessOneLiveReading(TLiveRecord liveRecord);

        public abstract bool ProcessOneHistoryReading(THistRecord histRecord);
    }
}
