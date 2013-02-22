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

namespace Conversations
{
    public class EndianConverter
    {
        // byte order represented with 0 being the byte array position of the low order byte
        // common storage formats
        public static readonly byte[] BigEndian64Bit = { 7, 6, 5, 4, 3, 2, 1, 0 };
        public static readonly byte[] LittleEndian64Bit = { 0, 1, 2, 3, 4, 5, 6, 7 };
        public static readonly byte[] BigEndian32Bit = { 3, 2, 1, 0 };
        public static readonly byte[] LittleEndian32Bit = { 0, 1, 2, 3 };
        public static readonly byte[] BigEndian16Bit = { 1, 0 };
        public static readonly byte[] LittleEndian16Bit = { 0, 1 };

        // unusual formats
        // BigLittleEndian32Bit is an "on the wire" format found in Modbus transfers of 32 bit data
        public static readonly byte[] BigLittleEndian32Bit = { 1, 0, 3, 2 }; // 16 bit values are internal BigEndian. but the 16 bit "words" are LittleEndian
        public static readonly byte[] LittleBigEndian32Bit = { 2, 3, 0, 1 }; // 16 bit values are internal LittleEndian. but the 16 bit "words" are BigEndian

        private byte MapLengthInternal;
        private byte[] InternalMap;
        private byte[] ExternalMap;

        protected bool TranslationRequired;
        private byte[] InputTranslate;  // position in external array to load the internal array
        private byte[] OutputTranslate; // position in the internal array to load the external array


        public EndianConverter(byte[] externalMap, byte expectedMapSize)
        {
            if (externalMap.Length != expectedMapSize)
                throw new Exception("EndianConverter - Incompatible map length - Expected: " + expectedMapSize + " - Found: " + externalMap.Length);          
            
            ExternalMap = externalMap;
            MapLengthInternal = expectedMapSize;

            if (MapLengthInternal == 2)
                InternalMap = System.BitConverter.IsLittleEndian ? LittleEndian16Bit : BigEndian16Bit;
            else if (MapLengthInternal == 4)
                InternalMap = System.BitConverter.IsLittleEndian ? LittleEndian32Bit : BigEndian32Bit;
            else if (MapLengthInternal == 8)
                InternalMap = System.BitConverter.IsLittleEndian ? LittleEndian64Bit : BigEndian64Bit;
            else
                throw new Exception("EndianConverter - Internal Error - Non-standard map length - Found: " + MapLengthInternal);

            CheckTranslation();
        }

        public EndianConverter(byte[] internalMap, byte[] externalMap, byte expectedMapSize)
        {
            if (externalMap.Length != expectedMapSize)
                throw new Exception("EndianConverter - Incompatible external map length - Expected: " + expectedMapSize + " - Found: " + externalMap.Length);
            if (internalMap.Length != expectedMapSize)
                throw new Exception("EndianConverter - Incompatible internal map length - Expected: " + expectedMapSize + " - Found: " + internalMap.Length);         
            ExternalMap = externalMap;
            MapLengthInternal = expectedMapSize;
            InternalMap = internalMap;

            CheckTranslation();
        }

        private void CheckTranslation()
        {
            TranslationRequired = false;
            InputTranslate = new byte[MapLengthInternal];
            OutputTranslate = new byte[MapLengthInternal];
            
            for (byte i = 0; i < MapLengthInternal; i++)
            {
                byte externalPos = ExternalMap[i];
                if (externalPos >= MapLengthInternal)
                    throw new Exception("EndianConverter.BuildTranslationMaps - External position out of bounds: " + externalPos);
                TranslationRequired |= externalPos != InternalMap[i];
                
                byte j;
                for (j = 0; j < MapLengthInternal; j++)
                    if (externalPos == InternalMap[j])
                    {
                        InputTranslate[i] = j;
                        OutputTranslate[j] = i;
                        break;
                    }
                if (j == MapLengthInternal)
                    throw new Exception("EndianConverter.BuildTranslationMaps - Unmapped External position: " + externalPos);
            }
        }

        public byte[] ExternalToInternal(byte[] externalBytes, int start = 0)
        {
            byte[] intBytes = new byte[MapLengthInternal];
            if (TranslationRequired)
            {
                for (int i = 0; i < MapLengthInternal; i++)
                    intBytes[InputTranslate[i]] = externalBytes[start + i];
            }
            else for (int i = 0; i < MapLengthInternal; i++)
                    intBytes[i] = externalBytes[start + i];

            return intBytes;
        }

        public byte[] InternalToExternal(byte[] internalBytes)
        {
            if (TranslationRequired)
            {
                byte[] extBytes = new byte[MapLengthInternal];

                for (int i = 0; i < MapLengthInternal; i++)
                    extBytes[OutputTranslate[i]] = internalBytes[i];

                return extBytes;
            }
            else
                return internalBytes;
        }

        public static decimal GetDecimalFromBCD(ref byte[] inArray, int length, int start = 0)
        {
            decimal val = 0;
            byte lowMask = 0x0F;
            byte highMask = 0xF0;
            int pos = start;
            for (int i = 0; i < length; i++)
            {
                byte b = inArray[pos++];
                val *= 10;                
                val += (b & highMask) >> 4;
                val *= 10;
                val += b & lowMask;
            }

            return val;
        }

        public static byte[] GetBCDFromDecimal(decimal value, int outputBytes, int decimals, bool includeSign)
        {
            String strDigits = (Math.Abs(Math.Truncate((double)value * Math.Pow(10.0, decimals)))).ToString().Trim();
            int requiredSize = strDigits.Length + (includeSign ? 1 : 0);
            int requiredBytes = (requiredSize + 1) / 2;

            const byte positive = 12; // 0xC
            const byte negative = 13; // 0xD
            byte sign;
            if (value < 0)
            {
                if (!includeSign)
                    throw new Exception("EndianConverter.GetBCDFromDecimal - Negative value in unsigned output - Value: " + value);                
                sign = negative;
            }
            else
                sign = positive;

            if (requiredBytes > outputBytes)
                throw new Exception("EndianConverter.GetBCDFromDecimal - Requested output size too small - Requested: " + outputBytes + " - Required: " + requiredBytes);

            byte[] bytes = new byte[outputBytes];
            for (int i = 0; i < outputBytes; i++)
                bytes[i] = 0;
           

            int offset = '0';

            bool insertSign = includeSign;
            bool useLow = true;
            int curByte = outputBytes - 1;
            int digits = strDigits.Length;
            
            while (digits > 0)
            {
                byte b;

                if (insertSign)
                {
                    b = sign;
                    insertSign = false;
                }
                else
                {
                    b = (byte)(((int)strDigits[--digits]) - offset);
                }

                if (useLow)
                {
                    bytes[curByte] = b;
                    useLow = false;
                }
                else
                {
                    bytes[curByte] = (byte)((b << 4) | bytes[curByte]);
                    useLow = true;
                    curByte--;
                }                
            }

            return bytes;
        }
    }

    public class EndianConverter16Bit : EndianConverter
    {
        public EndianConverter16Bit(byte[] externalMap) 
            : base(externalMap, 2)
        {
        }

        public EndianConverter16Bit(byte[] internalMap, byte[] externalMap)
            : base(internalMap, externalMap, 2)
        {
        }

        public UInt16 GetUInt16FromBytes(ref byte[] inArray, int start = 0)
        {
            if (TranslationRequired)
            {
                byte[] temp = ExternalToInternal(inArray, start);

                return System.BitConverter.ToUInt16(temp, 0);
            }
            else
                return System.BitConverter.ToUInt16(inArray, start);
        }

        public Int16 GetInt16FromBytes(ref byte[] inArray, int start = 0)
        {
            if (TranslationRequired)
            {
                byte[] temp = ExternalToInternal(inArray, start);

                return System.BitConverter.ToInt16(temp, 0);
            }
            else
                return System.BitConverter.ToInt16(inArray, start);
        }

        public byte[] GetExternalBytes(UInt16 inValue)
        {
            byte[] temp = System.BitConverter.GetBytes(inValue);
            return InternalToExternal(temp);
        }

        public byte[] GetExternalBytes(Int16 inValue)
        {
            byte[] temp = System.BitConverter.GetBytes(inValue);
            return InternalToExternal(temp);
        }
    }

    public class EndianConverter32Bit : EndianConverter
    {
        public EndianConverter32Bit(byte[] externalMap)
            : base(externalMap, 4)
        {
        }

        public EndianConverter32Bit(byte[] internalMap, byte[] externalMap)
            : base(internalMap, externalMap, 4)
        {
        }

        public UInt32 GetUInt32FromBytes(ref byte[] inArray, int start = 0)
        {
            if (TranslationRequired)
            {
                byte[] temp = ExternalToInternal(inArray, start);

                return System.BitConverter.ToUInt32(temp, 0);
            }
            else
                return System.BitConverter.ToUInt32(inArray, start);
        }

        public Int32 GetInt32FromBytes(ref byte[] inArray, int start = 0)
        {
            if (TranslationRequired)
            {
                byte[] temp = ExternalToInternal(inArray, start);

                return System.BitConverter.ToInt32(temp, 0);
            }
            else
                return System.BitConverter.ToInt32(inArray, start);
        }

        public byte[] GetExternalBytes(UInt32 inValue)
        {
            byte[] temp = System.BitConverter.GetBytes(inValue);
            return InternalToExternal(temp);
        }

        public byte[] GetExternalBytes(Int32 inValue)
        {
            byte[] temp = System.BitConverter.GetBytes(inValue);
            return InternalToExternal(temp);
        }
    }

    public class EndianConverter64Bit : EndianConverter
    {
        public EndianConverter64Bit(byte[] externalMap)
            : base(externalMap, 8)
        {
        }

        public EndianConverter64Bit(byte[] internalMap, byte[] externalMap)
            : base(internalMap, externalMap, 8)
        {
        }

        public UInt64 GetUInt64FromBytes(byte[] inArray, int start = 0)
        {
            if (TranslationRequired)
            {
                byte[] temp = ExternalToInternal(inArray, start);

                return System.BitConverter.ToUInt64(temp, 0);
            }
            else
                return System.BitConverter.ToUInt64(inArray, start);
        }

        public Int64 GetInt64FromBytes(byte[] inArray, int start = 0)
        {
            if (TranslationRequired)
            {
                byte[] temp = ExternalToInternal(inArray, start);

                return System.BitConverter.ToInt64(temp, 0);
            }
            else
                return System.BitConverter.ToInt64(inArray, start);
        }

        public byte[] GetExternalBytes(UInt64 inValue)
        {
            byte[] temp = System.BitConverter.GetBytes(inValue);
            return InternalToExternal(temp);
        }

        public byte[] GetExternalBytes(Int64 inValue)
        {
            byte[] temp = System.BitConverter.GetBytes(inValue);
            return InternalToExternal(temp);
        }
    }
}
