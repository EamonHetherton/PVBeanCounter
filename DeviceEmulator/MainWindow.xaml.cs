using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO.Ports;
using DeviceStream;
using MackayFisher.Utilities;
using Conversations;
using GenThreadManagement;
using System.Xml.Linq;

namespace DeviceEmulator
{
    public enum EmulatorType
    {
        CMS,
        ModbusGrowatt,
        ModbusKLNE,
        KLNE,
        ModbusXantrex,
        XantrexASCII,
        ModbusSunnyroo,
        Fronius,
        CurrentCostEnviR,
        stc4005,
        GenericReader
    }

    public class Inverter
    {
        public Double EToday { get; private set; }
        public Double ETotal { get; private set; }
        public Double PowerAC { get; private set; }
        public Double PowerPV { get; private set; }
        public Double VoltageAC { get; private set; }
        public Double CurrentAC { get; private set; }
        public Double VoltagePV { get; private set; }
        public Double CurrentPV { get; private set; }


        private TimeSpan Etime;
        private DateTime LastTime;

        private Double EnergyScale;
        private Double EnergyTotalScale;
        private Double PowerScale;
        private Double VoltageScale;
        private Double CurrentScale;

        public Inverter(double energyScale, double energyTotalScale, double powerScale, double voltageScale, double currentScale)
        {
            EToday = 0.0; //kwh
            ETotal = 100.0 * energyTotalScale;  //kwh
            PowerAC = 0.0;  //watts
            Etime = TimeSpan.FromHours(6.5);
            LastTime = DateTime.Now.AddSeconds(-6.0);
            PowerScale = powerScale;
            EnergyScale = energyScale;
            EnergyTotalScale = energyTotalScale;
            VoltageScale = voltageScale;
            CurrentScale = currentScale;
            VoltageAC = 240.0 * VoltageScale;
            CurrentAC = 0.0;
            VoltagePV = 0.0;
            CurrentPV = 0.0;
        }

        private Double PowerFromTime(TimeSpan time, Double peakPower)
        {
            TimeSpan startTime = System.TimeSpan.FromHours(6.5);
            TimeSpan endTime = System.TimeSpan.FromHours(17.5);
            if (time <= startTime || time >= endTime)
                return 0.0;
            TimeSpan peakRunning = TimeSpan.FromHours((endTime - startTime).TotalHours / 2.0);
            TimeSpan running = time - startTime;
            if (running < peakRunning)
                return peakPower * running.TotalHours / peakRunning.TotalHours;
            else
                return peakPower * (peakRunning.TotalHours - (running.TotalHours - peakRunning.TotalHours)) / peakRunning.TotalHours;
        }

        public void UpdateEnergy(SystemServices services)
        {
            DateTime now = DateTime.Now;
            TimeSpan elapsed = now - LastTime;
            Etime = Etime.Add(elapsed);
            LastTime = now;
            PowerPV = PowerFromTime(Etime, 10000.0) * PowerScale;
            PowerAC = PowerPV * 0.95;
            //Double energy = (PowerAC / 1000.0) * elapsed.TotalHours * EnergyScale / PowerScale;
            Double energy = (PowerAC / 1000.0) * elapsed.TotalHours * EnergyScale / PowerScale;
            EToday += energy;
            ETotal += (PowerAC / 1000.0) * elapsed.TotalHours * EnergyTotalScale / PowerScale;
            if (PowerPV > 0.0)
            {
                VoltagePV = 300.0 * VoltageScale;
                CurrentPV = ((PowerPV / PowerScale) / (VoltagePV / VoltageScale)) * CurrentScale;
                CurrentAC = ((PowerAC / PowerScale) / (VoltageAC / VoltageScale)) * CurrentScale;
            }
            else
            {
                CurrentAC = 0.0;
                VoltagePV = 0.0;
                CurrentPV = 0.0;
                PowerAC = 0.0;
                PowerPV = 0.0;
            }
            services.LogMessage("UpdateEnergy", "Power: " + PowerAC + 
                " - Duration: " + elapsed.TotalSeconds + " - Energy: " + (energy / EnergyScale) + " - EnergyTotal: " + (EToday / EnergyScale), LogEntryType.Information);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static String[,] EmulatorMatrix = 
            { { "CMS Inverter", "CMS_Emulation_2.txt" }
            , { "Modbus Growatt", "Modbus_Growatt_Emulation.txt" }
            , { "Modbus KLNE", "Modbus_KLNE_Emulation.txt" }
            , { "KLNE Inverter", "KLNE_Emulation.txt" }
            , { "Modbus Xantrex", "Modbus_Xantrex_Emulation.txt" }
            , { "Xantrex ASCII", "Xantrex_ASCII_Emulation.txt" }
            , { "Modbus Sunnyroo", "Modbus_Sunnyroo_Emulation.txt" }
            , { "Fronius", "Fronius_Emulation.txt" }
            , { "CC EnviR", "CC_Sample.xml" }
            , { "stc-4005", "stc-4005_Emulation.txt" }
            , { "GenericReader", "GenericReader.txt" } };

        SerialStream Stream;
        Converse Converse;
        Boolean ReaderStarted;
        int BaudRate = 9600;
        Parity Parity = Parity.None;
        int DataBits = 8;
        StopBits StopBits = StopBits.One;
        Handshake Handshake = Handshake.None;
        GenThreadManagement.GenThreadManager GenThreadManager;

        bool UsePortReader = false;
        EmulatorType EmulatorType = EmulatorType.CMS;

        Thread EmulatorThread;
        bool DoEmulator;

        // CC128 variables
        XElement rootElement;
        XElement liveElement;
        XElement histElements;
        IEnumerator<XElement> histList = null;
        int Watts1_0 = 150;
        int Watts2_0 = 420;
        int Watts3_0 = 276;
        int Watts1_1 = 456;
        int Watts2_1 = 1425;
        int Watts3_1 = 847;
        float Tmpr = 17.4F;
        // CC128

        public static System.Threading.Mutex ExecutionMutex = new System.Threading.Mutex();

        public List<String> Emulators;

        public List<String> EmulatorList
        {
            get { return Emulators; }
        }

        public String EmulatorName { get; set; }

        public String PortName { get; set; }

        public List<String> SerialPortsList
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

        SystemServices Services;
        String LogFileName = @"C:\PVRecords\emulatorLog.txt";

        public MainWindow()
        {
            InitializeComponent();
            Services = new SystemServices();
            GenThreadManager = new GenThreadManagement.GenThreadManager(Services);
            Services.LogMessageContent = true;
            Services.LogMeterTrace = true;
            Services.LogTrace = true;
            Converse = null;
            liveElement = null;
            histElements = null;
            rootElement = null;
            EmulatorThread = null;
            butSendHist.Visibility = System.Windows.Visibility.Hidden;
            buttonStop.IsEnabled = false;

            Emulators = new List<String>(EmulatorMatrix.GetLength(0));

            for (int i = 0; i < EmulatorMatrix.GetLength(0); i++)
            {
                Emulators.Add(EmulatorMatrix[i, 0]);
            }

            MainGrid.DataContext = this;
        }

        public void StartPortReader()
        {
            Stream = new SerialStream(GenThreadManager, Services, PortName, BaudRate, Parity, DataBits, StopBits, Handshake, 20000);
            Converse.SetDeviceStream(Stream);
            Stream.Open();
            Stream.StartBuffer();
            
            ReaderStarted = true;
        }

        public void StopPortReader()
        {
            if (ReaderStarted)
            {
                Stream.Close();
                Converse.SetDeviceStream(null);
                Stream = null;
                
                ReaderStarted = false;
            }
        }

        private void LoadConverse()
        {
            String configFile = "";
            UsePortReader = false;
            EmulatorType = DeviceEmulator.EmulatorType.CMS;
            Converse = null;
            liveElement = null;
            rootElement = null;

            if (EmulatorName == "CMS Inverter")
            {
                EmulatorType = DeviceEmulator.EmulatorType.CMS;
                Converse = (Converse)new Phoenixtec_Converse(Services);
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                ConversationLoader conversations = new ConversationLoader(configFile, Services);
                conversations.LoadConversations(Converse);
                Converse.NoTimeout = false;
                UsePortReader = true;
            }
            else if (EmulatorName == "Modbus Growatt")
            {
                EmulatorType = DeviceEmulator.EmulatorType.ModbusGrowatt;
                ModbusConverseCalculations calc = new ModbusConverseCalculations();
                Converse = (Converse)new Converse(Services,calc);
                Converse.SetCheckSum16Endian(true);
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                ConversationLoader conversations = new ConversationLoader(configFile, Services);
                conversations.LoadConversations(Converse);
                Converse.NoTimeout = false;
                UsePortReader = true;
            }
            else if (EmulatorName == "Modbus KLNE")
            {
                EmulatorType = DeviceEmulator.EmulatorType.ModbusKLNE;
                ModbusConverseCalculations calc = new ModbusConverseCalculations();
                Converse = (Converse)new Converse(Services, calc);
                Converse.SetCheckSum16Endian(true);
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                ConversationLoader conversations = new ConversationLoader(configFile, Services);
                conversations.LoadConversations(Converse);
                Converse.NoTimeout = false;
                UsePortReader = true;
            }
            else if (EmulatorName == "KLNE Inverter")
            {
                EmulatorType = DeviceEmulator.EmulatorType.KLNE;
                Converse = (Converse)new Phoenixtec_Converse(Services);
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                ConversationLoader conversations = new ConversationLoader(configFile, Services);
                conversations.LoadConversations(Converse);
                Converse.NoTimeout = false;
                UsePortReader = true;
            }
            else if (EmulatorName == "Modbus Xantrex")
            {
                EmulatorType = DeviceEmulator.EmulatorType.ModbusXantrex;
                ModbusConverseCalculations calc = new ModbusConverseCalculations();
                Converse = (Converse)new Converse(Services, calc);
                Converse.SetCheckSum16Endian(true);
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                ConversationLoader conversations = new ConversationLoader(configFile, Services);
                conversations.LoadConversations(Converse);
                Converse.NoTimeout = false;
                UsePortReader = true;
            }
            else if (EmulatorName == "Modbus Sunnyroo")
            {
                EmulatorType = DeviceEmulator.EmulatorType.ModbusSunnyroo;
                ModbusConverseCalculations calc = new ModbusConverseCalculations();
                Converse = (Converse)new Converse(Services, calc);
                Converse.SetCheckSum16Endian(true);
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                ConversationLoader conversations = new ConversationLoader(configFile, Services);
                conversations.LoadConversations(Converse);
                Converse.NoTimeout = false;
                UsePortReader = true;
            }
            else if (EmulatorName == "Fronius")
            {
                EmulatorType = DeviceEmulator.EmulatorType.Fronius;
                FroniusConverseCalculations calc = new FroniusConverseCalculations();
                Converse = (Converse)new Converse(Services, calc);
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                ConversationLoader conversations = new ConversationLoader(configFile, Services);
                conversations.LoadConversations(Converse);
                Converse.NoTimeout = false;
                UsePortReader = true;
            }
            else if (EmulatorName == "Xantrex ASCII")
            {
                EmulatorType = DeviceEmulator.EmulatorType.XantrexASCII;
                Converse = (Converse)new Converse(Services,null);
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                ConversationLoader conversations = new ConversationLoader(configFile, Services);
                conversations.LoadConversations(Converse);
                Converse.NoTimeout = false;
                UsePortReader = true;
            }
            else if (EmulatorName == "CC EnviR")
            {
                EmulatorType = DeviceEmulator.EmulatorType.CurrentCostEnviR;               
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                rootElement = XElement.Load(configFile, LoadOptions.None);
                liveElement = rootElement.Element("live").Element("msg");
                UsePortReader = false;
            }
            else if (EmulatorName == "GenericReader")
            {
                EmulatorType = DeviceEmulator.EmulatorType.GenericReader;
                ModbusConverseCalculations calc = new ModbusConverseCalculations();
                Converse = (Converse)new Converse(Services, calc);
                Converse.SetCheckSum16Endian(true);
                configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                ConversationLoader conversations = new ConversationLoader(configFile, Services);
                conversations.LoadConversations(Converse);
                Converse.NoTimeout = false;
                UsePortReader = true;
            }
        }

        public void RunEmulator()
        {
            ExecutionMutex.WaitOne();
            bool success = true;
            DoEmulator = true;
            DateTime lastTime = DateTime.Now;
            EndianConverter32Bit e32 = new EndianConverter32Bit(EndianConverter.BigEndian32Bit);

            try
            {
                Services.OpenLogFile(LogFileName);

                LoadConverse();
                if (UsePortReader)
                    StartPortReader();
                else
                {
                    Stream = new SerialStream(GenThreadManager, Services, PortName, BaudRate, Parity, DataBits, StopBits, Handshake, 20000);
                    Stream.Open();
                }

                if (EmulatorType == DeviceEmulator.EmulatorType.CMS)
                {
                    // bool started = false;
                    Inverter inv = new Inverter(100.0, 10.0, 1.0, 10.0, 10.0);

                    while (DoEmulator)
                    {
                        if (Converse.DoConversation("ReadQuery1"))
                        {
                            ByteVar varControl = (ByteVar)Converse.GetSessionVariable("Control");
                            ByteVar varFunction = (ByteVar)Converse.GetSessionVariable("Function");
                            ByteVar varSize = (ByteVar)Converse.GetSessionVariable("Size");
                            ByteVar varData = (ByteVar)Converse.GetSessionVariable("Data");
                            varData.Resize(varSize.GetByte());
                            Converse.DoConversation("ReadQuery2");


                            if (varControl.GetByte() == 0 && varFunction.GetByte() == 4) // Initialise
                                continue;
                            if (varControl.GetByte() == 0 && varFunction.GetByte() == 0) // Get Serial
                            {
                                Converse.DoConversation("SendSerialNo");
                                continue;
                            }
                            if (varControl.GetByte() == 0 && varFunction.GetByte() == 1) // Get Serial
                            {
                                Converse.DoConversation("AckAddress");
                                continue;
                            }
                            if (varControl.GetByte() == 1 && varFunction.GetByte() == 3) // Get details
                            {
                                Converse.DoConversation("SendDetails");
                                continue;
                            }
                            if (varControl.GetByte() == 1 && varFunction.GetByte() == 0) // Get format
                            {
                                Converse.DoConversation("SendFormat");
                                continue;
                            }
                            if (varControl.GetByte() == 1 && varFunction.GetByte() == 2) // Eun Device
                            {
                                inv.UpdateEnergy(Services);

                                ByteVar var = (ByteVar)Converse.GetSessionVariable("Temperature", null);
                                var.SetBytes(250, 0);

                                var = (ByteVar)Converse.GetSessionVariable("EnergyToday", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.EToday), 0);

                                var = (ByteVar)Converse.GetSessionVariable("VoltsPV", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.VoltagePV), 0);

                                var = (ByteVar)Converse.GetSessionVariable("CurrentAC", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.CurrentAC), 0);

                                var = (ByteVar)Converse.GetSessionVariable("VoltsAC", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.VoltageAC), 0);

                                var = (ByteVar)Converse.GetSessionVariable("Frequency", null);
                                var.SetBytes(5000, 0);

                                var = (ByteVar)Converse.GetSessionVariable("PowerAC", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.PowerAC), 0);

                                var = (ByteVar)Converse.GetSessionVariable("ImpedanceAC", null);
                                var.SetBytes(0, 0);

                                var = (ByteVar)Converse.GetSessionVariable("EnergyTotal", null);
                                byte[] intern = System.BitConverter.GetBytes(System.Convert.ToUInt32(inv.ETotal));
                                byte[] ext = e32.InternalToExternal(intern);
                                var.SetBytes(ref ext, 0, 4);

                                var = (ByteVar)Converse.GetSessionVariable("Hours", null);
                                intern = System.BitConverter.GetBytes(2500);
                                ext = e32.InternalToExternal(intern);
                                var.SetBytes(ref ext, 0, 4);

                                var = (ByteVar)Converse.GetSessionVariable("Mode", null);
                                var.SetBytes(0, 0);

                                var = (ByteVar)Converse.GetSessionVariable("ErrorMode", null);
                                var.SetBytes(0, 0);

                                Converse.DoConversation("RunDevice");
                                continue;
                            }
                        }
                    }
                }
                else if (EmulatorType == DeviceEmulator.EmulatorType.GenericReader)
                {
                    while (DoEmulator)
                    {

                        Converse.DoConversation("Read");
                    }
                }
                else if (EmulatorType == DeviceEmulator.EmulatorType.Fronius)
                {
                    // bool started = false;

                    Inverter inv = new Inverter(1.0, 1.0, 1.0, 1.0, 1.0);

                    while (DoEmulator)
                    {
                        if (Converse.DoConversation("ReadQuery"))
                        {
                            byte[] size = Converse.GetSessionVariable("Size", null).GetBytes();
                            byte[] option = Converse.GetSessionVariable("Option", null).GetBytes();
                            byte[] number = Converse.GetSessionVariable("Number", null).GetBytes();
                            byte[] cmd = Converse.GetSessionVariable("Command", null).GetBytes();
                            if (cmd[0] == 1)
                            {
                                Converse.DoConversation("Identity");
                            }
                            else if (cmd[0] == 2)
                            {
                                Converse.DoConversation("Model");
                            }
                            else if (cmd[0] == 16)
                            {
                                inv.UpdateEnergy(Services);
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("PowerAC", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.PowerAC), 0);
                                Converse.DoConversation("Power");
                            }
                            else if (cmd[0] == 17)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("EnergyTotal", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.ETotal), 0);
                                Converse.DoConversation("EnergyTotal");
                            }
                            else if (cmd[0] == 18)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("EnergyToday", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.EToday), 0);
                                Converse.DoConversation("EnergyToday");
                            }
                            else if (cmd[0] == 20)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("CurrentAC", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.CurrentAC), 0);
                                Converse.DoConversation("CurrentAC");
                            }
                            else if (cmd[0] == 21)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("VoltsAC", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.VoltageAC), 0);
                                Converse.DoConversation("VoltsAC");
                            }
                            else if (cmd[0] == 22)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("Frequency", null);
                                var.SetBytes(50, 0);
                                Converse.DoConversation("Frequency");
                            }
                            else if (cmd[0] == 23)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("CurrentPV", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.CurrentPV), 0);
                                Converse.DoConversation("CurrentPV");
                            }
                            else if (cmd[0] == 24)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("VoltsPV", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.VoltagePV), 0);
                                Converse.DoConversation("VoltsPV");
                            }
                            else if (cmd[0] == 42)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("Hours", null);
                                var.SetBytes(2000, 0);
                                Converse.DoConversation("TimeTotal");
                            }
                            else if (cmd[0] == 224)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("Temperature", null);
                                var.SetBytes(55, 0);
                                Converse.DoConversation("Temperature");
                            }
                            else if (cmd[0] == 15)
                            {
                                ByteVar var = (ByteVar)Converse.GetSessionVariable("Mode", null);
                                var.SetBytes(0, 0);
                                Converse.DoConversation("Status");
                            }
                        }
                    }
                }
                else if (EmulatorType == DeviceEmulator.EmulatorType.ModbusGrowatt)
                {
                    // bool started = false;

                    Inverter inv = new Inverter(10.0, 10.0, 10.0, 10.0, 10.0);

                    while (DoEmulator)
                    {
                        if (Converse.DoConversation("ReadQuery"))
                        {
                            byte[] cmd = Converse.GetSessionVariable("Command", null).GetBytes();
                            byte[] start = Converse.GetSessionVariable("StartRegister", null).GetBytes();
                            if (cmd[0] == 3)
                            {
                                if (start[1] == 23)  // Big Endian input stream
                                {
                                    Converse.DoConversation("SerialNo");
                                }
                            }
                            else if (cmd[0] == 4)
                            {
                                if (start[1] == 0)
                                {
                                    inv.UpdateEnergy(Services);
                                    ByteVar var = (ByteVar)Converse.GetSessionVariable("EnergyToday", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.EToday), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("EnergyTotal", null);
                                    byte[] intern = System.BitConverter.GetBytes(System.Convert.ToUInt32(inv.ETotal));
                                    byte[] ext = e32.InternalToExternal(intern);
                                    var.SetBytes(ref ext, 0, 4);
                                    var = (ByteVar)Converse.GetSessionVariable("PowerAC", null);
                                    intern = System.BitConverter.GetBytes(System.Convert.ToUInt32(inv.PowerAC));
                                    ext = e32.InternalToExternal(intern);
                                    var.SetBytes(ref ext, 0, 4);
                                    var = (ByteVar)Converse.GetSessionVariable("Freq", null);
                                    var.SetBytes(5000, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("VoltageAC1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.VoltageAC), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("CurrentAC1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.CurrentAC), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("PowerAC1", null);
                                    intern = System.BitConverter.GetBytes(System.Convert.ToUInt32(inv.PowerAC));
                                    ext = e32.InternalToExternal(intern);
                                    var.SetBytes(ref ext, 0, 4);
                                    var = (ByteVar)Converse.GetSessionVariable("PowerPV", null);
                                    intern = System.BitConverter.GetBytes(System.Convert.ToUInt32(inv.PowerPV));
                                    ext = e32.InternalToExternal(intern);
                                    var.SetBytes(ref ext, 0, 4);
                                    var = (ByteVar)Converse.GetSessionVariable("PowerPV1", null);
                                    var.SetBytes(ref ext, 0, 4);
                                    var = (ByteVar)Converse.GetSessionVariable("VoltagePV1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.VoltagePV), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("CurrentPV1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.CurrentPV), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("DUMMY1", null);
                                    var.Initialise();
                                    var = (ByteVar)Converse.GetSessionVariable("DUMMY2", null);
                                    var.Initialise();
                                    var = (ByteVar)Converse.GetSessionVariable("DUMMY3", null);
                                    var.Initialise();
                                    Converse.DoConversation("Reading");
                                }
                            }
                        }
                    }
                }
                else if (EmulatorType == DeviceEmulator.EmulatorType.ModbusSunnyroo)
                {
                    // bool started = false;

                    Inverter inv = new Inverter(1.0, 1.0, 0.1, 1.0, 10.0);

                    while (DoEmulator)
                    {
                        if (Converse.DoConversation("ReadQuery"))
                        {
                            byte[] cmd = Converse.GetSessionVariable("Command", null).GetBytes();
                            byte[] start = Converse.GetSessionVariable("StartRegister", null).GetBytes();
                            if (cmd[0] == 3)
                            {
                                if (start[1] == 177)
                                {
                                    Converse.DoConversation("Identity");
                                }
                                else if (start[1] == 32)
                                {
                                    inv.UpdateEnergy(Services);
                                    ByteVar var = (ByteVar)Converse.GetSessionVariable("EnergyTotal", null);
                                    byte[] intern = System.BitConverter.GetBytes(System.Convert.ToUInt32(inv.ETotal));
                                    byte[] ext = e32.InternalToExternal(intern);
                                    var.SetBytes(ref ext, 0, 4);
                                    var = (ByteVar)Converse.GetSessionVariable("PowerAC1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.PowerAC), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("Freq", null);
                                    var.SetBytes(5000, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("VoltageAC1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.VoltageAC), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("CurrentAC1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.CurrentAC), 0);

                                    var = (ByteVar)Converse.GetSessionVariable("VoltagePV1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.VoltagePV), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("CurrentPV1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.CurrentPV), 0);

                                    Converse.DoConversation("Reading");
                                }
                                else if (start[1] == 0)
                                {
                                    Converse.DoConversation("Status");
                                }
                            }
                        }
                    }
                }
                else if (EmulatorType == DeviceEmulator.EmulatorType.ModbusKLNE)
                {
                    // bool started = false;
                    Inverter inv = new Inverter(10.0, 10.0, 10.0, 10.0, 10.0);

                    while (DoEmulator)
                    {
                        if (Converse.DoConversation("ReadQuery"))
                        {
                            byte[] cmd = Converse.GetSessionVariable("Command").GetBytes();
                            byte[] start = Converse.GetSessionVariable("StartRegister").GetBytes();

                            if (cmd[0] == 4)
                            {
                                if (start[1] == 0)
                                {
                                    Converse.DoConversation("Identity");
                                }
                                if (start[1] == 10)
                                {
                                    inv.UpdateEnergy(Services);
                                    //$VoltsPV1(BYTE[2]) $VoltsPV2(BYTE[2]) $CurrentPV1(BYTE[2]) $CurrentPV2(BYTE[2]) $VoltsAC1(BYTE[2]) $VoltsAC2(BYTE[2]) $VoltsAC3(BYTE[2]) 
                                    //$CurrentAC1(BYTE[2]) $CurrentAC2(BYTE[2]) $CurrentAC3(BYTE[2]) $Frequency(BYTE[2]) $PowerAC(BYTE[4]) $EnergyToday(BYTE[2]) $EnergyTotal(BYTE[4]) 
                                    //00 00 $TimeTotal(BYTE[4]) 00 00 $Temperature(BYTE[2]) 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 $ErrorCode(BYTE[2])
                                    ByteVar var = (ByteVar)Converse.GetSessionVariable("VoltsPV1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.VoltagePV), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("VoltsPV2", null);
                                    var.SetBytes(0, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("CurrentPV1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.CurrentPV), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("CurrentPV2", null);
                                    var.SetBytes(0, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("VoltsAC1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.VoltageAC), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("VoltsAC2", null);
                                    var.SetBytes(0, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("VoltsAC3", null);
                                    var.SetBytes(0, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("CurrentAC1", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.CurrentAC), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("CurrentAC2", null);
                                    var.SetBytes(0, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("CurrentAC3", null);
                                    var.SetBytes(0, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("Frequency", null);
                                    var.SetBytes(500, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("PowerAC", null);
                                    byte[] intern = System.BitConverter.GetBytes(System.Convert.ToUInt32(inv.PowerAC));
                                    byte[] ext = e32.InternalToExternal(intern);
                                    var.SetBytes(ref ext, 0, 4);
                                    var = (ByteVar)Converse.GetSessionVariable("EnergyToday", null);
                                    var.SetBytes(System.Convert.ToUInt16(inv.EToday), 0);
                                    var = (ByteVar)Converse.GetSessionVariable("EnergyTotal", null);
                                    intern = System.BitConverter.GetBytes(System.Convert.ToUInt32(inv.ETotal));
                                    ext = e32.InternalToExternal(intern);
                                    var.SetBytes(ref ext, 0, 4);
                                    var = (ByteVar)Converse.GetSessionVariable("TimeTotal", null);
                                    intern = System.BitConverter.GetBytes(25000);
                                    ext = e32.InternalToExternal(intern);
                                    var.SetBytes(ref ext, 0, 4);
                                    var = (ByteVar)Converse.GetSessionVariable("Temperature", null);
                                    var.SetBytes(250, 0);
                                    var = (ByteVar)Converse.GetSessionVariable("ErrorCode", null);
                                    var.SetBytes(0, 0);

                                    Converse.DoConversation("Reading");
                                }
                            }
                        }
                    }
                }
                else if (EmulatorType == DeviceEmulator.EmulatorType.KLNE)
                {
                    // bool started = false;
                    Inverter inv = new Inverter(10.0, 10.0, 1.0, 10.0, 10.0);

                    while (DoEmulator)
                    {
                        if (Converse.DoConversation("ReadQuery1"))
                        {
                            ByteVar varControl = (ByteVar)Converse.GetSessionVariable("Control");
                            ByteVar varFunction = (ByteVar)Converse.GetSessionVariable("Function");
                            ByteVar varSize = (ByteVar)Converse.GetSessionVariable("Size");
                            ByteVar varData = (ByteVar)Converse.GetSessionVariable("Data");
                            varData.Resize(varSize.GetByte());
                            Converse.DoConversation("ReadQuery2");


                            if (varControl.GetByte() == 0 && varFunction.GetByte() == 3) // Initialise
                                continue;
                            if (varControl.GetByte() == 0 && varFunction.GetByte() == 0) // Get Serial
                            {
                                Converse.DoConversation("SendSerialNo");
                                continue;
                            }
                            if (varControl.GetByte() == 0 && varFunction.GetByte() == 1) // Get Serial
                            {
                                Converse.DoConversation("AckAddress");
                                continue;
                            }
                            if (varControl.GetByte() == 1 && varFunction.GetByte() == 3) // Get details
                            {
                                Converse.DoConversation("SendDetails");
                                continue;
                            }
                            if (varControl.GetByte() == 1 && varFunction.GetByte() == 0) // Get format
                            {
                                Converse.DoConversation("SendFormat");
                                continue;
                            }
                            if (varControl.GetByte() == 1 && varFunction.GetByte() == 2) // Get data
                            {
                                inv.UpdateEnergy(Services);

                                ByteVar var = (ByteVar)Converse.GetSessionVariable("Temperature", null);
                                var.SetBytes(250, 0);

                                var = (ByteVar)Converse.GetSessionVariable("EnergyToday", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.EToday), 0);

                                var = (ByteVar)Converse.GetSessionVariable("VoltsPV", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.VoltagePV), 0);

                                var = (ByteVar)Converse.GetSessionVariable("CurrentAC", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.CurrentAC), 0);

                                var = (ByteVar)Converse.GetSessionVariable("VoltsAC", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.VoltageAC), 0);

                                var = (ByteVar)Converse.GetSessionVariable("Frequency", null);
                                var.SetBytes(5000, 0);

                                var = (ByteVar)Converse.GetSessionVariable("PowerAC", null);
                                var.SetBytes(System.Convert.ToUInt16(inv.PowerAC), 0);

                                var = (ByteVar)Converse.GetSessionVariable("EnergyTotal", null);
                                byte[] intern = System.BitConverter.GetBytes(System.Convert.ToUInt32(inv.ETotal));
                                byte[] ext = e32.InternalToExternal(intern);
                                var.SetBytes(ref ext, 0, 4);

                                var = (ByteVar)Converse.GetSessionVariable("Hours", null);
                                intern = System.BitConverter.GetBytes(2500);
                                ext = e32.InternalToExternal(intern);
                                var.SetBytes(ref ext, 0, 4);

                                var = (ByteVar)Converse.GetSessionVariable("Mode", null);
                                var.SetBytes(0, 0);

                                Converse.DoConversation("SendData");
                                continue;
                            }
                        }
                    }
                }
                else if (EmulatorType == DeviceEmulator.EmulatorType.ModbusXantrex)
                {
                    // bool started = false;
                    Inverter inv = new Inverter(10.0, 10.0, 10.0, 10.0, 10.0);
                    int energyId = 0;

                    while (DoEmulator)
                    {
                        if (Converse.DoConversation("ReadCommand"))
                        {
                            byte[] cmd = Converse.GetSessionVariable("Command").GetBytes();

                            if (cmd[0] == 3)
                            {
                                if (Converse.DoConversation("RegisterRequest"))
                                {
                                    byte[] start = Converse.GetSessionVariable("StartRegister").GetBytes();
                                    String cmdStr = SystemServices.BytesToHex(ref start);
                                    if (cmdStr == "0x0000")
                                    {
                                        ByteVar var = (ByteVar)Converse.GetSessionVariable("Model");
                                        var.SetBytes("Fireball XL5", (byte)0);
                                        var = (ByteVar)Converse.GetSessionVariable("SerialNo");
                                        var.SetBytes("XL5-007", (byte)0);
                                        Converse.DoConversation("Identity");
                                    }
                                    else if (cmdStr == "0x0082")
                                    {
                                        ByteVar var = (ByteVar)Converse.GetSessionVariable("ErrorValue");
                                        byte[] val = SystemServices.HexToBytes("0x0102030405060708");
                                        var.SetBytes(ref val, 0, 8);
                                        var = (ByteVar)Converse.GetSessionVariable("ErrorDesc");
                                        var.SetBytes("The Big Bad Error - BOO", (byte)0);
                                        Converse.DoConversation("ErrorDetail");
                                    }
                                    else if (cmdStr == "0x00CF")
                                    {
                                        inv.UpdateEnergy(Services);
                                        ByteVar var = (ByteVar)Converse.GetSessionVariable("Status");
                                        var.SetBytes(System.Convert.ToUInt16(0), 0);
                                        Converse.DoConversation("Status");
                                    }
                                    else if (cmdStr == "0x0201")
                                    {
                                        ByteVar var = (ByteVar)Converse.GetSessionVariable("VoltsPV1");
                                        var.SetBytes(System.Convert.ToUInt32(inv.VoltagePV), 0);
                                        var = (ByteVar)Converse.GetSessionVariable("CurrentPV1");
                                        var.SetBytes(System.Convert.ToUInt32(inv.CurrentPV), 0);
                                        Converse.DoConversation("InverterDC");
                                    }
                                    else if (cmdStr == "0x0701")
                                    {
                                        ByteVar var = (ByteVar)Converse.GetSessionVariable("VoltsAC1");
                                        var.SetBytes(System.Convert.ToUInt32(inv.VoltageAC), 0);
                                        var = (ByteVar)Converse.GetSessionVariable("CurrentAC1");
                                        var.SetBytes(System.Convert.ToUInt32(inv.CurrentAC), 0);
                                        var = (ByteVar)Converse.GetSessionVariable("Frequency");
                                        var.SetBytes(500, 0);
                                        var = (ByteVar)Converse.GetSessionVariable("PowerAC");
                                        var.SetBytes(System.Convert.ToUInt32(inv.PowerAC), 0);
                                        Converse.DoConversation("InverterAC");
                                    }
                                    else if (cmdStr == "0x0803")
                                    {
                                        ByteVar var = (ByteVar)Converse.GetSessionVariable("HistoryId");
                                        var.SetBytes(System.Convert.ToUInt16(0), 0);
                                        var = (ByteVar)Converse.GetSessionVariable("Energy");
                                        var.SetBytes(System.Convert.ToUInt32(energyId == 4 ? inv.EToday : inv.ETotal), 0);
                                        var = (ByteVar)Converse.GetSessionVariable("PeakPower");
                                        var.SetBytes(System.Convert.ToUInt32(inv.PowerAC), 0);
                                        var = (ByteVar)Converse.GetSessionVariable("HarvestTime");
                                        var.SetBytes(System.Convert.ToUInt32(10.0), 0);
                                        Converse.DoConversation("EnergyHistory");
                                    }
                                    else if (cmdStr == "0x0900")
                                    {
                                        ByteVar var = (ByteVar)Converse.GetSessionVariable("Temperature");
                                        var.SetBytes(System.Convert.ToUInt16(250), 0);
                                        Converse.DoConversation("Temperature");
                                    }
                                    else if (cmdStr == "0x0080")
                                    {
                                        ByteVar var = (ByteVar)Converse.GetSessionVariable("ErrorCount");
                                        var.SetBytes(System.Convert.ToUInt16(1), 0);
                                        Converse.DoConversation("ErrorCount");
                                    }
                                }
                            }
                            else if (cmd[0] == 16) // 0x10
                            {
                                if (Converse.DoConversation("SetRegisters_1"))
                                {
                                    // retrieve the datasize to follow and resize the receive buffer to suit
                                    ByteVar sizeVar = (ByteVar)Converse.GetSessionVariable("DataSize");
                                    ByteVar dataVar = (ByteVar)Converse.GetSessionVariable("Data");
                                    dataVar.Resize(sizeVar.GetByte());

                                    // retrieve the rest of the message
                                    if (Converse.DoConversation("SetRegisters_2"))
                                    {
                                        byte[] start = Converse.GetSessionVariable("StartRegister").GetBytes();
                                        String cmdStr = SystemServices.BytesToHex(ref start);
                                        if (cmdStr == "0x0800") // Energy History Initiate
                                        {
                                            energyId = dataVar.GetUInt16();
                                            Converse.DoConversation("SetRegistersConfirm");
                                        }
                                        if (cmdStr == "0x0081") // Initialise Error Index
                                        {
                                            Converse.DoConversation("SetRegistersConfirm");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else if (EmulatorType == DeviceEmulator.EmulatorType.XantrexASCII)
                {
                    // bool started = false;
                    Inverter inv = new Inverter(10.0, 10.0, 10.0, 10.0, 10.0);
                    int errCount = 0;

                    while (DoEmulator)
                    {
                        ByteVar response = ((ByteVar)Converse.GetSessionVariable("Response"));
                        if (Converse.DoConversation("ReadRequest"))
                        {
                            String cmd = ((DynamicByteVar)Converse.GetSessionVariable("Request")).ToString();

                            if (cmd == "IDN?")
                            {
                                String val = "M:Xantrex 1.0 X:1000 S:12345612";
                                response.Resize((UInt16)val.Length);
                                response.SetBytes(val);
                                Converse.DoConversation("SendResponse");
                            }
                            else if (cmd == "POUT?")
                            {
                                String val = inv.PowerAC.ToString();
                                response.Resize((UInt16)val.Length);
                                response.SetBytes(val);
                                Converse.DoConversation("SendResponse");
                            }
                            else if (cmd == "MEASTEMP?")
                            {
                                String val = "C:27.4 F:78.5";
                                response.Resize((UInt16)val.Length);
                                response.SetBytes(val);
                                Converse.DoConversation("SendResponse");
                            }
                            else if (cmd == "MEASIN?")
                            {
                                String val = "V:" + inv.VoltagePV + " I:"
                                    + inv.CurrentPV + " P:" + inv.PowerPV;
                                response.Resize((UInt16)val.Length);
                                response.SetBytes(val);
                                Converse.DoConversation("SendResponse");
                            }
                            else if (cmd == "MEASOUT?")
                            {
                                String val = "V:" + inv.VoltageAC + " I:"
                                    + inv.CurrentAC + " P:" + inv.PowerAC + " F:50.1";
                                response.Resize((UInt16)val.Length);
                                response.SetBytes(val);
                                Converse.DoConversation("SendResponse");
                            }
                            else if (cmd == "KWHTODAY?")
                            {
                                String val = inv.EToday.ToString("F3");
                                response.Resize((UInt16)val.Length);
                                response.SetBytes(val);
                                Converse.DoConversation("SendResponse");
                            }
                            else if (cmd == "KWHLIFE?")
                            {
                                String val = inv.ETotal.ToString("F3");
                                response.Resize((UInt16)val.Length);
                                response.SetBytes(val);
                                Converse.DoConversation("SendResponse");
                                inv.UpdateEnergy(Services);
                            }
                            else if (cmd == "FAULTACTIVE?")
                            {
                                if (errCount < 5)
                                {
                                    String val = "0x220D22";
                                    byte[] bytes = SystemServices.HexToBytes(val);
                                    response.Resize((UInt16)bytes.Length);
                                    response.SetBytes(ref bytes, 0, bytes.Length);
                                    Converse.DoConversation("SendResponse");
                                    errCount++;
                                }
                                else
                                {
                                    String val = "0x220D3030312C204552520D22";  // "cr001, ERRcr"cr
                                    byte[] bytes = SystemServices.HexToBytes(val);
                                    response.Resize((UInt16)bytes.Length);
                                    response.SetBytes(ref bytes, 0, bytes.Length);
                                    Converse.DoConversation("SendResponse");
                                    if (errCount == 5)  // send this twice
                                        errCount++;
                                    else
                                        errCount = 0;
                                }
                            }
                        }
                    }
                }
                else if (EmulatorType == DeviceEmulator.EmulatorType.CurrentCostEnviR)
                {
                    XElement time = liveElement.Element("time");
                    XElement sensor = liveElement.Element("sensor");
                    XElement tmpr = liveElement.Element("tmpr");
                    XElement ch = liveElement.Element("ch1");
                    XElement watts1 = ch.Element("watts");
                    ch = liveElement.Element("ch2");
                    XElement watts2 = ch.Element("watts");
                    ch = liveElement.Element("ch3");
                    XElement watts3 = ch.Element("watts");
                    do
                    {
                        time.Value = DateTime.Now.ToString("HH:mm:ss");
                        sensor.Value = "0";
                        tmpr.Value = Math.Round(Tmpr, 1).ToString();
                        watts1.Value = Watts1_0.ToString();
                        watts2.Value = Watts2_0.ToString();
                        watts3.Value = Watts3_0.ToString();
                        String xmlText = liveElement.ToString();
                        byte[] bytes = SystemServices.StringToBytes(xmlText);
                        success = Stream.Write(bytes, 0, bytes.Length);

                        sensor.Value = "1";
                        watts1.Value = Watts1_1.ToString();
                        watts2.Value = Watts2_1.ToString();
                        watts3.Value = Watts3_1.ToString();

                        xmlText = liveElement.ToString();
                        bytes = SystemServices.StringToBytes(xmlText);
                        success = Stream.Write(bytes, 0, bytes.Length);

                        if (success)
                        {
                            Watts1_0 += (int)(Watts1_0 * 0.01);
                            Watts2_0 += (int)(Watts1_0 * 0.02);
                            Watts3_0 += (int)(Watts1_0 * 0.015);
                            Watts1_1 += (int)(Watts1_1 * 0.015);
                            Watts2_1 += (int)(Watts1_1 * 0.025);
                            Watts3_1 += (int)(Watts1_1 * 0.01);
                            Tmpr *= 1.01F;

                            lastTime = lastTime.AddSeconds(6.0);
                            DateTime curTime = DateTime.Now;
                            if (lastTime > curTime)
                                Thread.Sleep(lastTime - curTime);
                            else
                                lastTime = DateTime.Now;
                        }
                    }
                    while (success && DoEmulator);
                }

                if (UsePortReader)
                    StopPortReader();
                else
                    Stream.Close();

                Services.CloseLogFile();
            }
            catch (Exception e)
            {
                Services.LogMessage("RunEmulator", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                ExecutionMutex.ReleaseMutex();
            }
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            butSendHist.IsEnabled = false;
            buttonStart.IsEnabled = false;
            buttonStop.IsEnabled = true;

            EmulatorThread = new Thread(new ThreadStart(RunEmulator));
            EmulatorThread.Name = "Emulator";
            EmulatorThread.Start();
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {            
            DoEmulator = false;
            EmulatorThread = null;
            butSendHist.IsEnabled = true;
            buttonStart.IsEnabled = true;
            buttonStop.IsEnabled = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DoEmulator = false;
            EmulatorThread = null;
        }

        private void SendOneHistory()
        {
            bool first;
            if (histList == null)
            {
                histList = histElements.Elements().GetEnumerator();
                first = true;
            }
            else
                first = false;

            if (histList.MoveNext())
            {
                XElement hist = histList.Current;
                String xmlText = hist.ToString();
                byte[] bytes = SystemServices.StringToBytes(xmlText);
                Stream.Write(bytes, 0, bytes.Length);
            }
            else if (!first) // avoid recursion on empty list
            {
                histList = null;
                SendOneHistory();
            }
            else
                histList = null;
        }

        private void butSendHist_Click(object sender, RoutedEventArgs e)
        {
            ExecutionMutex.WaitOne();
            Services.OpenLogFile(LogFileName);
            Stream = new SerialStream(GenThreadManager, Services, PortName, BaudRate, Parity, DataBits, StopBits, Handshake, 20000);
            Stream.Open();

            SendOneHistory();

            Stream.Close();
            Services.CloseLogFile();
            ExecutionMutex.ReleaseMutex();
        }

        private void comboBoxType_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            if (EmulatorName == "CC EnviR")
            {
                butSendHist.Visibility = System.Windows.Visibility.Visible;
                histList = null;
                EmulatorType = DeviceEmulator.EmulatorType.CurrentCostEnviR;
                String configFile = EmulatorMatrix[(int)EmulatorType, 1];
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
                XElement root = XElement.Load(configFile, LoadOptions.None);
                histElements = root.Element("history");
            }
        }
    }
}
