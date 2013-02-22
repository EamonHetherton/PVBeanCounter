using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using InTheHand.Net;
using Buffers;
using MackayFisher.Utilities;
using GenThreadManagement;

namespace DeviceStream
{
    public class BluetoothStream : DeviceStream
    {
        private BluetoothClient BluetoothClient = null;

        //public override String DeviceName { get; set; }

        public BluetoothStream(GenThreadManagement.GenThreadManager genThreadManager, SystemServices systemServices)
            : base(genThreadManager, systemServices)
        {
        }

        protected override String StreamType { get { return "BluetoothStream"; } }

        public override String DeviceName { get { return "Bluetooth"; } }

        public BluetoothDeviceInfo GetDeviceInfo(String deviceName)
        {
            if (BluetoothClient == null)
                BluetoothClient = new BluetoothClient();

            BluetoothDeviceInfo[] peers = BluetoothClient.DiscoverDevices();

            int i;
            for (i = 0; i < peers.Length; i++)
            {
                if (peers[i].DeviceName.StartsWith(DeviceName))
                    break;
            }

            if (i < peers.Length)
                return peers[i];
            else
                return null;
        }

        public override Stream GetStream()
        {
            if (Stream == null)
            {
                BluetoothDeviceInfo devInfo = GetDeviceInfo(DeviceName);

                if (devInfo == null)
                    return null;

                BluetoothEndPoint ep = new BluetoothEndPoint(devInfo.DeviceAddress, BluetoothService.SerialPort);
                BluetoothClient.Connect(ep);
                Stream peerStream = BluetoothClient.GetStream();

                if (BluetoothClient.Connected)
                {
                    Stream = peerStream;
                    return Stream;
                }
                else
                    return null;
            }
            else
                return Stream;
        }
    }
}
