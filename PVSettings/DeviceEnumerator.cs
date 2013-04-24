/*
* Copyright (c) 2013 Dennis Mackay-Fisher
*
* This file is part of PV Bean Counter
* 
* PV Bean Counter is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 3 or later 
* as published by the Free Software Foundation.
* 
* PV Bean Counter is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace PVSettings
{
    // DeviceEnumerator presents all devices from all DeviceManagers in a single enumeration
    // Used for unique name checking
    public class DeviceEnumerator : IEnumerator<DeviceManagerDeviceSettings>
    {
        private ObservableCollection<DeviceManagerSettings> DeviceManagers;

        private IEnumerator<DeviceManagerSettings> DeviceManagerEnumerator;
        private IEnumerator<DeviceManagerDeviceSettings> AllDevicesEnumerator;

        public DeviceEnumerator(ObservableCollection<DeviceManagerSettings> deviceManagers)
        {
            DeviceManagers = deviceManagers;
            Reset();
        }

        public void Reset()
        {
            DeviceManagerEnumerator = DeviceManagers.GetEnumerator();
            DeviceManagerEnumerator.Reset();
            AllDevicesEnumerator = null;
        }

        public bool MoveNext()
        {
            while (true)
            {
                while (AllDevicesEnumerator == null)
                {
                    if (DeviceManagerEnumerator.MoveNext())
                    {
                        AllDevicesEnumerator = DeviceManagerEnumerator.Current.DeviceList.GetEnumerator();
                        AllDevicesEnumerator.Reset();
                    }
                    else
                        return false;
                }

                if (AllDevicesEnumerator.MoveNext())
                    return true;
                else
                    AllDevicesEnumerator = null;
            }
        }

        public DeviceManagerDeviceSettings Current
        {
            get
            {
                if (AllDevicesEnumerator == null)
                    return null;
                return AllDevicesEnumerator.Current;
            }
        }

        Object IEnumerator.Current
        {
            get { return Current; }
        }

        public void Dispose()
        {
            DeviceManagerEnumerator = null;
            AllDevicesEnumerator = null;
        }
    }

}
