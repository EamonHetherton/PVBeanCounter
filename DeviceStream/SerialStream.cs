using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using Buffers;
using MackayFisher.Utilities;
using GenThreadManagement;

namespace DeviceStream
{
    public class SerialStream : DeviceStream
    {
        private SerialPort SerialPort;
        private bool IsOpen;

        private String PortName;
        private int BaudRate;
        private int DataBits;
        private StopBits StopBits;
        private Parity Parity;
        private Handshake Handshake;
        private int ReadTimeout;
        private int WriteTimeout;

        public SerialStream(GenThreadManagement.GenThreadManager genThreadManager, SystemServices systemServices, String portName, int baudRate = 57600, Parity parity = Parity.None,
            int dataBits = 8, StopBits stopBits = StopBits.One, Handshake handshake = Handshake.None, int readTimeout = 3000, int writeTimeout = 20000)
            : base(genThreadManager, systemServices)
        {
            SerialPort = new SerialPort();
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            StopBits = stopBits;
            Parity = parity;
            Handshake = handshake;
            ReadTimeout = readTimeout;
            WriteTimeout = writeTimeout;
            IsOpen = false;

            ConfigurePort();
        }

        public override string DeviceName
        {
            get { return PortName; }
        }

        private void ConfigurePort()
        {
            SerialPort.PortName = PortName;
            SerialPort.BaudRate = BaudRate;
            SerialPort.Parity = Parity;
            SerialPort.DataBits = DataBits;
            SerialPort.StopBits = StopBits;
            SerialPort.Handshake = Handshake;
            SerialPort.ReadTimeout = ReadTimeout;
            SerialPort.WriteTimeout = WriteTimeout;
        }

        protected override String StreamType { get { return "SerialStream"; } }

        private void OpenStream()
        {
            LogMessage("DeviceStream.GetStream", "Opening stream");
            SerialPort.Open();

            IsOpen = true;
            Stream = SerialPort.BaseStream;

            LogMessage("DeviceStream.GetStream", "Stream open");
        }

        public override Stream GetStream()
        {
            if (!IsOpen)
                OpenStream();

            return Stream;
        }

        public void Open()
        {
            if (!IsOpen)
                OpenStream();
            Stream = SerialPort.BaseStream;
        }

        public override void PurgeStreamBuffers()
        {
            SerialPort.DiscardOutBuffer();
            // pause to allow all pending data to arrive
            Thread.Sleep(300);
            SerialPort.DiscardInBuffer();
            LogMessage("PurgeStreamBuffers", "SerialPort buffers purged: " + PortName, LogEntryType.Trace);
        }

        public void Close()
        {
            if (IsOpen)
            {
                LogMessage("DeviceStream.Close", "Closing stream");

                base.StopBuffer();
                SerialPort.Close();
                SerialPort = null;
                IsOpen = false;
                LogMessage("DeviceStream.Close", "Stream closed");
            }
        }

        public override bool Reset()
        {
            ResetToClosed();
            try
            {
                // pause to allow port to settle!!!
                Thread.Sleep(3000);
                IsOpen = false;
                SerialPort = new System.IO.Ports.SerialPort();
                ConfigurePort();
                Open();
                IsOpen = true;
            }
            catch (Exception e)
            {
                LogMessage("Reset", "Exception opening SerialPort: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
            return true;
        }

        public void ResetToClosed()
        {
            if (IsOpen)
            {
                try
                {
                    Stream = null;
                    SerialPort.Close();
                }
                catch (Exception e)
                {
                    LogMessage("Reset", "Exception closing SerialPort: " + e.Message, LogEntryType.ErrorMessage);
                }
                try
                {
                    SerialPort.Dispose();
                }
                catch (Exception e)
                {
                    LogMessage("Reset", "Exception disposing SerialPort: " + e.Message, LogEntryType.ErrorMessage);
                }
                IsOpen = false;
                try
                {
                    Stream = null;
                    SerialPort = null;
                }
                catch (Exception e)
                {
                    LogMessage("Reset", "Exception nulling SerialPort: " + e.Message, LogEntryType.ErrorMessage);
                }
            }
        }
    }
}
