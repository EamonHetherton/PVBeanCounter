/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace PVSettings
{
    public class SerialPortSettings : SettingsBase
    {
        public SerialPortSettings(SettingsBase root, XmlElement element)
            : base(root, element)
        {
        }

        public static List<String> SerialPortsList
        {
            get
            {
                List<String> list = new List<String>();
                String[] array = System.IO.Ports.SerialPort.GetPortNames();
                foreach (String s in array)
                    list.Add(s);

                return list;
            }
        }

        public static List<String> ParityList
        {
            get
            {
                List<String> list = new List<String>();

                list.Add(""); // represents null

                for (int i = 0; i < 5; i++)
                    list.Add(ToString((System.IO.Ports.Parity)i));

                return list;
            }
        }

        public static System.IO.Ports.Parity? ToParity(String parityStr)
        {
            if (parityStr == "" || parityStr == null)
                return null;

            if (parityStr == "Odd")
                return System.IO.Ports.Parity.Odd;
            if (parityStr == "Even")
                return System.IO.Ports.Parity.Even;
            if (parityStr == "Mark")
                return System.IO.Ports.Parity.Mark;
            if (parityStr == "Space")
                return System.IO.Ports.Parity.Space;

            return System.IO.Ports.Parity.None;
        }

        public static String ToString(System.IO.Ports.Parity? parity)
        {
            if (parity == null)
                return "";

            if (parity == System.IO.Ports.Parity.Odd)
                return "Odd";
            if (parity == System.IO.Ports.Parity.Even)
                return "Even";
            if (parity == System.IO.Ports.Parity.Mark)
                return "Mark";
            if (parity == System.IO.Ports.Parity.Space)
                return "Space";

            return "None";
        }

        public static System.IO.Ports.StopBits? ToStopBits(String stopBitsStr)
        {
            if (stopBitsStr == "" || stopBitsStr == null)
                return null;

            if (stopBitsStr == "None")
                return System.IO.Ports.StopBits.None;
            if (stopBitsStr == "One")
                return System.IO.Ports.StopBits.One;
            if (stopBitsStr == "OnePointFive")
                return System.IO.Ports.StopBits.OnePointFive;

            return System.IO.Ports.StopBits.Two;
        }

        public static String ToString(System.IO.Ports.StopBits? stopBits)
        {
            if (stopBits == null)
                return "";

            if (stopBits == System.IO.Ports.StopBits.None)
                return "None";
            if (stopBits == System.IO.Ports.StopBits.One)
                return "One";
            if (stopBits == System.IO.Ports.StopBits.OnePointFive)
                return "OnePointFive";

            return "Two";
        }

        public static List<String> StopBitsList
        {
            get
            {
                List<String> list = new List<String>();

                list.Add(""); // represents null

                for (int i = 0; i < 4; i++)
                    list.Add(ToString((System.IO.Ports.StopBits)i));

                return list;
            }
        }

        public static List<String> HandshakeList
        {
            get
            {
                List<String> list = new List<String>();

                list.Add(""); // represents null

                for (int i = 0; i < 4; i++)
                    list.Add(ToString((System.IO.Ports.Handshake)i));

                return list;
            }
        }

        public static List<String> BaudRateList
        {
            get
            {
                List<String> list = new List<String>();

                list.Add(""); // represents null
                list.Add("1200");
                list.Add("2400");
                list.Add("4800");
                list.Add("9600");
                list.Add("57600");

                return list;
            }
        }

        public static List<String> DataBitsList
        {
            get
            {
                List<String> list = new List<String>();

                list.Add(""); // represents null
                list.Add("5");
                list.Add("6");
                list.Add("7");
                list.Add("8");

                return list;
            }
        }

        public static System.IO.Ports.Handshake? ToHandshake(String handshakeStr)
        {
            if (handshakeStr == "" || handshakeStr == null)
                return null;

            if (handshakeStr == "RTS")
                return System.IO.Ports.Handshake.RequestToSend;
            if (handshakeStr == "RTS/XOnXOff")
                return System.IO.Ports.Handshake.RequestToSendXOnXOff;
            if (handshakeStr == "XOnXOff")
                return System.IO.Ports.Handshake.XOnXOff;

            return System.IO.Ports.Handshake.None;
        }

        public static String ToString(System.IO.Ports.Handshake? handshake)
        {
            if (handshake == null)
                return "";

            if (handshake == System.IO.Ports.Handshake.RequestToSend)
                return "RTS";
            if (handshake == System.IO.Ports.Handshake.RequestToSendXOnXOff)
                return "RTS/XOnXOff";
            if (handshake == System.IO.Ports.Handshake.XOnXOff)
                return "XOnXOff";

            return "None";
        }

        public String PortName
        {
            get
            {
                return GetValue("portname");
            }

            set
            {
                SetValue("portname", value, "PortName");
            }
        }

        public System.IO.Ports.Parity? Parity
        {
            get
            {
                String val = GetValue("parity");
                if (val == "")
                    return null;

                return ToParity(val);
            }

            set
            {
                if (value == null)
                    SetValue("parity", "", "Parity");
                else
                    SetValue("parity", ToString(value.Value), "Parity");
            }
        }

        public System.IO.Ports.Handshake? Handshake
        {
            get
            {
                String val = GetValue("handshake");
                if (val == "")
                    return null;

                return ToHandshake(val);
            }

            set
            {
                if (value == null)
                    SetValue("handshake", "", "Handshake");
                else
                    SetValue("handshake", ToString(value.Value), "Handshake");
            }
        }

        public System.IO.Ports.StopBits? StopBits
        {
            get
            {
                String val = GetValue("stopbits");
                if (val == "")
                    return null;

                return ToStopBits(val);
            }

            set
            {
                if (value == null)
                    SetValue("stopbits", "", "StopBits");
                else
                    SetValue("stopbits", ToString(value.Value), "StopBits");
            }
        }

        public int? BaudRate
        {
            get
            {
                String rffd = GetValue("baudrate");

                if (rffd == "")
                    return null;

                return Convert.ToInt32(rffd);
            }

            set
            {
                if (value == null)
                    SetValue("baudrate", "", "BaudRate");
                else
                    SetValue("baudrate", value.ToString(), "BaudRate");
            }
        }

        public int? DataBits
        {
            get
            {
                String rffd = GetValue("databits");

                if (rffd == "")
                    return null;

                return Convert.ToInt32(rffd);
            }

            set
            {
                if (value == null)
                    SetValue("databits", "", "DataBits");
                else
                    SetValue("databits", value.ToString(), "DataBits");
            }
        }
    }
}
