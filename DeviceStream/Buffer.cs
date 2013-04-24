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
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Threading;
using MackayFisher.Utilities;
using DeviceStream;
using GenThreadManagement;

namespace Buffers
{
    public interface IByteBuffer
    {
        MatchInfo ReadFromBuffer(byte[] bytes, int length, int minLength, TimeSpan? wait = null, bool consume = true);
        MatchInfo FindInBuffer(byte[] pattern, int maxSkip, bool consume, TimeSpan? wait, bool extractSkipped, out DataChunk skippedChunk);
    }

    class BufferSegment  
    {
        public byte[] Bytes;

        public int BytesUsed;
        public int StartByte;

        public int NextAvailableByte { get { return BytesUsed + StartByte; } }

        public BufferSegment(int size)
        {
            Bytes = new byte[size];
            BytesUsed = 0;
            StartByte = 0;
        }
    }

    public class MatchInfo
    {
        // match result - used by FindInBuffer
        public bool Matched = false;
        // bytes consumed from buffer
        public int BytesConsumed = 0;
        // bytes skipped in buffer but not necessarily consumed from buffer
        public int BytesSkipped = 0;
        
        // bytes read from buffer but not necessarily consumed from buffer - does not include bytes skipped
        public int BytesRead 
        { 
            get 
            {
                if (TotalBytesRead > 0)
                    return TotalBytesRead - BytesSkipped;
                else
                    return 0;
            } 
        }
        
        // bytes scanned but not necessarily consumed from buffer
        public int TotalBytesRead = 0;
        // timeout occurred before min read length reached
        public bool Timeout = false;
    }

    public class ByteBuffer : IByteBuffer
    {
        public static String BytesToHex(byte[] input, String separator, int length = -1, int start = 0)
        {
            String output = "";
            String hexOutput;
            int size = input.Length - start;
            if (length >= 0 && length < size)
                size = length;

            for (int i = 0; i < size; i++)
            {
                byte letter = input[i + start];

                int value = Convert.ToInt32(letter);
                // Convert the decimal value to a hexadecimal value in string form.
                hexOutput = String.Format("{0:X2}", value);
                if (output != "")
                    output = output + separator + hexOutput;
                else
                    output = hexOutput;
            }
            return output;
        }

        public static String BytesToAscii(byte[] input, String separator, int length = -1, int start = 0)
        {
            String output = "";
            int size = input.Length - start;
            if (length >= 0 && length < size)
                size = length;

            for (int i = 0; i < size; i++)
            {
                byte letter = input[i + start];
                char outLetter;

                int value = Convert.ToInt32(letter);
                if (value < 32 || value > 126)
                    outLetter = Convert.ToChar(248);
                else
                    outLetter = Convert.ToChar(value);

                if (i > 0)
                    output = output + separator + outLetter;
                else
                    output = Convert.ToString(outLetter);
            }

            return output;
        }

        private byte[] LocalBuffer;

        // position next read from stream will read to
        private int NextByteAvail;
        private int BytesUsedInt;

        public int BytesUsed
        {
            get
            {
                return BytesUsedInt;
            }
            private set
            {
                if (value < 0)
                    BytesUsedInt = 0;
                else
                    BytesUsedInt = value;
            }
        }

        private int BytesRead;

        private DateTime? ErrorTime;

        private SystemServices SystemServices;

        private DeviceStream.DeviceStream DeviceStream;

        // location next read from buffer will start
        private int FirstByteUsed { get{return NextByteAvail - BytesUsed;}}
        // space available at end of buffer (ignores wasted space at buffer start)
        public int BytesAvailable { get { return LocalBuffer.Length - NextByteAvail; } }

        public ByteBuffer(SystemServices systemServices,
            DeviceStream.DeviceStream deviceStream, int localBufferSize = 10000)
        {
            DeviceStream = deviceStream;
            SystemServices = systemServices;

            LocalBuffer = new byte[localBufferSize];

            NextByteAvail = 0;
            BytesUsed = 0;
        }

        private void LogMessage(String routine, String message, LogEntryType logEntryType = LogEntryType.MeterTrace)
        {
            SystemServices.LogMessage("ByteBuffer: " + routine, message, logEntryType);
        }

        private int ReadFromStreamToBuffer(int maxLength, out bool timeOut)
        {
            int bytesRead;
            DateTime startTime = DateTime.Now;
            timeOut = false;

            if (ErrorTime != null && (DateTime.Now - ErrorTime.Value).TotalSeconds < 30)
            {
                Thread.Sleep(2000);
                bytesRead = 0;
            }
            else
            {
                ErrorTime = null;
                bool error = false;
                try
                {
                    Thread.Sleep(100);
                    startTime = DateTime.Now;
                    bytesRead = DeviceStream.GetStream().Read(LocalBuffer, NextByteAvail, maxLength);
                    NextByteAvail += bytesRead;
                    BytesUsed += bytesRead;
                }
                catch (TimeoutException)
                {
                    if (SystemServices.LogMessageContent)
                        LogMessage("ReadFromStreamToBuffer", "Timeout after " + 
                            (DateTime.Now - startTime).TotalMilliseconds + " - Timeout: " + 
                            DeviceStream.GetStream().ReadTimeout, LogEntryType.MeterMessage);
                    timeOut = true;
                    bytesRead = 0;
                }
                catch (Exception e)
                {
                    bytesRead = 0;
                    LogMessage("ReadFromStreamToBuffer", "Exception reading from stream: " + e.Message, LogEntryType.ErrorMessage);
                    DeviceStream.DeviceError = true;
                    ErrorTime = DateTime.Now;
                    error = true;
                }

                if (error)
                {
                    if (DeviceStream.Reset())
                    {
                        DeviceStream.DeviceError = false;
                        try
                        {
                            Stream stream = DeviceStream.GetStream();
                        }
                        catch (Exception)
                        {
                            DeviceStream.DeviceError = true;
                        }
                    }
                }
            }

            return bytesRead;
        }

        private void ShiftOrReallocateBuffer(int readSize)
        {           
            if (LocalBuffer.Length < readSize)
            {
                // allocate new buffer
                byte[] newBuffer = new byte[readSize];
                int fromPos = FirstByteUsed;
                int toPos = 0;
                // copy content to start of new buffer
                for (int cnt = 0; cnt < BytesUsed; cnt++)
                    newBuffer[toPos++] = LocalBuffer[fromPos++];
                // replace old buffer
                LocalBuffer = newBuffer;
                NextByteAvail = BytesUsed;    
            }
            else if (FirstByteUsed > 0)
            {
                int fromPos = FirstByteUsed;
                int toPos = 0;
                // left shift buffer content
                for (int cnt = 0; cnt < BytesUsed; cnt++)
                    LocalBuffer[toPos++] = LocalBuffer[fromPos++];

                NextByteAvail = BytesUsed;
            }
        }

        public MatchInfo ReadFromBuffer(byte[] bytes, int length, int minLength, TimeSpan? wait = null, bool consume = true)
        {
            MatchInfo info = new MatchInfo();
            info.BytesConsumed = 0;
            info.BytesSkipped = 0;
            info.Matched = true;
            info.Timeout = false;
            info.TotalBytesRead = 0;
            bool timeOut;

            if (minLength > length)
                minLength = length;

            int count = 0;
            DateTime startTime = DateTime.Now;

            int immediate;

            if (BytesUsed >= length)
                immediate = length;
            else
                immediate = BytesUsed;

            int fromPos = FirstByteUsed;
            int toPos = 0;

            for (int cnt = 0; cnt < immediate; cnt++)
                bytes[toPos++] = LocalBuffer[fromPos++];

            count = immediate;

            if (consume)
            {
                BytesUsed -= count;
                BytesRead += count;
                // when buffer is empty, set next byte avail to start of buffer
                if (BytesUsed == 0)
                    NextByteAvail = 0;
            }

            if (count < minLength)
            {

                int remaining = length - count;

                // ensure buffer has room to read in the required extra bytes
                if (!consume && remaining > BytesAvailable)
                {
                    ShiftOrReallocateBuffer(BytesUsed + remaining);

                    if (remaining > BytesAvailable)
                    {
                        LogMessage("ReadFromBuffer", "Error adjusting buffer for non-consuming read: " + length, LogEntryType.ErrorMessage);
                        info.Matched = false;
                        return info;
                    }
                }

                do
                {
                    int readCount;

                    readCount = ReadFromStreamToBuffer(remaining, out timeOut);

                    if (consume)
                        fromPos = FirstByteUsed;
                    else
                        fromPos = FirstByteUsed + count;
                    toPos = count;

                    for (int cnt = 0; cnt < readCount; cnt++)
                        bytes[toPos++] = LocalBuffer[fromPos++];

                    count += readCount;

                    if (consume)
                    {
                        BytesRead += readCount;
                        BytesUsed -= readCount;
                        if (BytesUsed == 0)
                            NextByteAvail = 0;
                    }

                    remaining -= readCount;

                    if (count >= minLength)
                        break;

                    if (timeOut)
                    {
                        info.Timeout = true;
                        info.Matched = false;
                        break;
                    }
                }
                while (count < length);
            }

            info.BytesConsumed = count;
            info.TotalBytesRead = count;

            if (SystemServices.LogMessageContent)
            {
                LogMessage("ReadFromBuffer", "Matched: " + info.Matched + " - Timeout: " + info.Timeout +
                    " - BytesRead: " + info.BytesRead + " - Bytes[" + count + "] next line", LogEntryType.MeterMessage);
                FormatDump(bytes, 0, count, SystemServices);
            }
            return info;
        }

        public MatchInfo FindInBuffer(byte[] pattern, int maxSkip, bool consume, TimeSpan? wait, bool extractSkipped, out DataChunk skippedChunk)
        {
            int totalCount = 0;
            int maxScan = maxSkip + pattern.Length;
            DateTime startTime = DateTime.Now;
            int patternPos = 0;
            bool matchstarted = false;

            MatchInfo info = new MatchInfo();

            DataChunk skipped;

            if (extractSkipped)
                skipped = new DataChunk(maxSkip);
            else
                skipped = null;

            skippedChunk = skipped;

            int bytesRequired = pattern.Length + maxSkip;

            if (bytesRequired > BytesAvailable + BytesUsed)
                ShiftOrReallocateBuffer(bytesRequired);

            bool skippedExceeded = false;

            while (totalCount < maxScan && !info.Matched && !skippedExceeded)
            {
                int remaining = maxScan - totalCount;
                if ((BytesUsed - totalCount) == 0)
                {
                    int readCount;
                    bool timeOut;

                    readCount = ReadFromStreamToBuffer(remaining, out timeOut);

                    if (timeOut)
                    {  
                        info.Timeout = true;
                        LogMessage("FindInBuffer", "Timeout: " + wait.Value.ToString(), LogEntryType.MeterMessage);
                        
                        break;
                    }
                }
                else if ((BytesUsed - totalCount) < 0)
                {
                    LogMessage("FindInBuffer", "Logic error - totalCount exceeds buffer content", LogEntryType.ErrorMessage);
                    totalCount = 0;
                    info.Matched = false;
                    info.BytesSkipped = 0;
                    info.BytesConsumed = 0;
                    break;
                }

                if ((BytesUsed - totalCount) > 0)
                {
                    while ((FirstByteUsed + totalCount) < NextByteAvail)
                    {
                        matchstarted = (pattern[patternPos] == LocalBuffer[FirstByteUsed + totalCount++]);
                        
                        if (matchstarted)
                        {
                            if (++patternPos == pattern.Length)
                            {
                                info.Matched = true;
                                break;
                            }
                        }
                        else
                        {
                            if (totalCount > maxSkip)
                            {
                                skippedExceeded = true;
                                break;
                            }
                            info.BytesSkipped = totalCount;
                            if (extractSkipped)
                            {                               
                                // increment skipped size
                                skipped.ChunkSize = totalCount;
                                // copy up to mismatch character
                                int offset = (totalCount - 1) - patternPos;
                                while (offset < totalCount)
                                {
                                    skipped.ChunkBytes[offset] = LocalBuffer[FirstByteUsed + offset];
                                    offset++;
                                }
                            }
                            //reset to start of pattern
                            patternPos = 0;
                        }
                    }
                }
            }

            if (consume)
            {
                BytesUsed -= totalCount;
                if (BytesUsed == 0)
                    NextByteAvail = 0;

                info.BytesConsumed = totalCount; // total bytes matched or skipped
            }

            BytesRead += info.BytesConsumed;
            info.TotalBytesRead = totalCount;

            if (SystemServices.LogMessageContent)
            {
                if (info.BytesSkipped > 0)
                {
                    LogMessage("FindInBuffer", "Skipped[" + info.BytesSkipped + "] next line", LogEntryType.MeterMessage);
                    FormatDump(skippedChunk.ChunkBytes, 0, skippedChunk.ChunkSize, SystemServices);
                }

                LogMessage("FindInBuffer", "Matched: " + info.Matched + " - Timeout: " + info.Timeout + 
                    " - BytesRead: " + info.BytesRead + " - TotalBytesRead: " + info.TotalBytesRead + " - Pattern[" + pattern.Length + "] next line", LogEntryType.MeterMessage);

                FormatDump(pattern, 0, pattern.Length, SystemServices);
            }

            return info;
        }

        private static int GetScale(int value)
        {
            return ((int)Math.Log10(value) + 1);            
        }

        private static String PadString(String inStr, int length, char pad = ' ', bool padRight = true)
        {
            String outStr = inStr;

            while (outStr.Length < length)
                if (padRight)
                    outStr += pad;
                else
                    outStr = pad + outStr;

            return outStr;
        }

        public static void FormatDump(byte[] buffer, int startByte, int length, SystemServices systemServices)
        {
            const int bytesPerLine = 40;

            int lines = (length + bytesPerLine - 1) / bytesPerLine;

            int start = startByte;
            int scale = GetScale(length);
            String format = "D" + scale.ToString();

            String header = PadString("", scale * 2 + 1); // spaces over position range - "     " for "00-19"

            if (lines > 1)
            {
                for (int i = 0; i < bytesPerLine; i++)
                    header += PadString(i.ToString(), 3, ' ', false);
                header += " ";
                for (int i = 0; i < bytesPerLine; i++)
                    header += (i % 10).ToString();
            }
            else
            {
                header = PadString("", scale * 2 + 1); // spaces over position range - "     " for "00-19"
                for (int i = 0; i < length; i++)
                    header += PadString(i.ToString(), 3, ' ', false);
                header += " ";
                for (int i = 0; i < length; i++)
                    header += (i % 10).ToString();
            }

            systemServices.LogMessage("", header, LogEntryType.Format);

            int pos = 0;
            int line = 0;
            while (line < lines)
            {
                int len;
                if ((pos + bytesPerLine) <= length)
                    len = bytesPerLine;
                else
                    len = length - pos;

                String lineRange = pos.ToString(format) + "-" + (pos+len-1).ToString(format);
                String hex = ByteBuffer.BytesToHex(buffer, " ", len, start);
                String ascii = ByteBuffer.BytesToAscii(buffer, "", len, start);
                if (line > 0 && len < bytesPerLine)
                {
                    hex = PadString(hex, (bytesPerLine * 3)-1);
                    ascii = PadString(ascii, bytesPerLine);
                }

                systemServices.LogMessage("", lineRange + " " + hex + " " + ascii, LogEntryType.Format);

                line++;
                pos += bytesPerLine;
                start += bytesPerLine;
            }
        }
    }
}
