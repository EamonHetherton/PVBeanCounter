using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using Conversations;
using DeviceStream;
using MackayFisher.Utilities;
using PVBCInterfaces;

namespace Algorithms
{
    public delegate void SetNumberValueDelegate(decimal value);
    public delegate void SetStringValueDelegate(string value);
    public delegate void SetBytesValueDelegate(byte[] value);

    public delegate Decimal GetNumberValueDelegate();
    public delegate String GetStringValueDelegate();
    public delegate byte[] GetBytesValueDelegate();

    /*
    public interface ICommunicationManager
    {
        ErrorLogger ErrorLogger { get; }
    }
    */

    public interface IAlgorithm
    {
        ProtocolSettings.ProtocolType ProtocolType { get; }

        Protocol Protocol { get; }

        //ICommunicationManager Manager { get; }

        UInt64 Address { get; set; }

        ByteVar InverterAddress { get; set; }
        ByteVar ModbusCommand { get; set; }
        ByteVar RegisterCount { get; set; }
        ByteVar FirstModbusAddress { get; set; }

        ByteVar DeviceDataSize { get; set; }
        ByteVar DeviceData { get; set; }
        ByteVar DeviceDataValueSize { get; set; }
        ByteVar DeviceDataValue { get; set; }

        EndianConverter16Bit EndianConverter16Bit { get;}
        EndianConverter32Bit EndianConverter32Bit { get;  }
        
        bool ExecuteAlgorithmType(String type, bool mandatory, bool dbWrite);
        bool ExecuteAlgorithm(String name, bool mandatory = false);

        DeviceBlock FindBlock(String blockName);
        VariableEntry FindVariable(String itemName);
        Register FindRegister(String blockType, String blockName, String itemName);
        Register GetRegister(DeviceBlock block, RegisterSettings settings);
    }

}
