using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GenThreadManagement;
using PVSettings;
using PVBCInterfaces;

namespace DeviceControl
{
    public class DeviceManager_EnergyConsolidation : DeviceManagerTyped<Device.EnergyConsolidationDevice>
    {
        public DeviceManager_EnergyConsolidation(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
        }

        public override bool DoWork()
        {
            throw new NotSupportedException("Consolidation Device Managers do not run in a dedicated thread");
        }

        protected override Device.EnergyConsolidationDevice NewDevice(DeviceManagerDeviceSettings dmDevice)
        {
            return new Device.EnergyConsolidationDevice(this, dmDevice);
        }

        public Device.EnergyConsolidationDevice GetPVOutputConsolidationDevice(String systemId)
        {
            foreach (Device.EnergyConsolidationDevice d in DeviceList)
                if (d.DeviceManagerDeviceSettings.ConsolidationType == ConsolidationType.PVOutput
                && d.DeviceManagerDeviceSettings.PVOutputSystem == systemId)
                    return d;

            return null;
        }


        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(300); } }
    }
}
