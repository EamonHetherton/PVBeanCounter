/*
* Copyright (c) 2012 Dennis Mackay-Fisher
*
* This file is part of PV Scheduler
* 
* PV Scheduler is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 3 or later 
* as published by the Free Software Foundation.
* 
* PV Scheduler is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace PVSettings
{
    public class EW4009MeterManagerSettings : MeterManagerSettings
    {
        public SerialPortSettings SerialPort;

        public EW4009MeterManagerSettings(ApplicationSettings root, XmlElement element)
            : base(root, element)
        {
            SerialPort = null;
            foreach (XmlElement e in element.ChildNodes)
            {
                if (e.Name == "serialport")
                {
                    SerialPort = new SerialPortSettings(root, e);
                    break;
                }
            }
            if (SerialPort == null)
            {
                XmlElement e2 = AddElement(element, "serialport");
                SerialPort = new SerialPortSettings(root, e2);
            }
        }

        public override int SampleFrequency
        {
            get 
            {
                String rffd = GetValue("samplefrequency");
                if (rffd == "")
                    return 6;
                else
                    return Convert.ToInt32(rffd);
            }
            set
            {
                if (value > 60)
                    value = 60;
                else if (value < 6)
                    value = 6;
                SetValue("samplefrequency", value.ToString(), "SampleFrequency");
            }
        }

        public override MeterManagerType ManagerTypeInternal { get { return MeterManagerType.EW4009; } }

        public String PortName
        {
            get { return SerialPort.PortName; }
            set
            {
                SerialPort.PortName = value;
                SetPropertyChanged("PortName");
            }
        }
        public String BaudRate { get { return SerialPort.BaudRate.ToString(); } set { SerialPort.BaudRate = value == "" ? (int?)null : Convert.ToInt32(value); } }
        public String DataBits { get { return SerialPort.DataBits.ToString(); } set { SerialPort.DataBits = value == "" ? (int?)null : Convert.ToInt32(value); } }
        public String StopBits { get { return SerialPortSettings.ToString(SerialPort.StopBits); } set { SerialPort.StopBits = SerialPortSettings.ToStopBits(value); } }
        public String Parity { get { return SerialPortSettings.ToString(SerialPort.Parity); } set { SerialPort.Parity = SerialPortSettings.ToParity(value); } }
        public String Handshake { get { return SerialPortSettings.ToString(SerialPort.Handshake); } set { SerialPort.Handshake = SerialPortSettings.ToHandshake(value); } }

    }
}
