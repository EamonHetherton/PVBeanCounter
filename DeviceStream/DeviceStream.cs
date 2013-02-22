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
    public class DataChunk
    {
        public int ChunkMaxSize;
        public int ChunkSize;
        public byte[] ChunkBytes;

        public DataChunk(int size)
        {
            ChunkBytes = new byte[size];
            ChunkSize = 0;
            ChunkMaxSize = size;
        }
    }

    public abstract class DeviceStream
    {
        protected Stream Stream;
        private IByteBuffer ByteBuffer;
        public SystemServices SystemServices;
        private GenThreadManagement.GenThreadManager GenThreadManager;
        // private int GenThreadId = -1;

        public abstract String DeviceName { get; }

        public bool DeviceError { get; set; }

        public DeviceStream(GenThreadManagement.GenThreadManager genThreadManager, SystemServices systemServices)
        {
            Stream = null;
            ByteBuffer = null;
            SystemServices = systemServices;
            DeviceError = false;
            // use original threaded byte buffer
            GenThreadManager = genThreadManager;
        }

        public DeviceStream(SystemServices systemServices)
        {
            Stream = null;
            ByteBuffer = null;
            SystemServices = systemServices;
            DeviceError = false;
            // use new synchronous byte buffer
            GenThreadManager = null;
        }

        protected void LogMessage(String routine, String message, LogEntryType logEntryType = LogEntryType.MeterTrace)
        {
            SystemServices.LogMessage(StreamType + ": "+ routine, message, logEntryType);
        }

        protected virtual String StreamType { get { return "DeviceStream"; } }

        public virtual void PurgeStreamBuffers()
        {
            // do nothing is the default
        }

        public virtual Stream GetStream()
        {
            return Stream;
        }

        public static String BytesToString(byte[] input, long size)
        {
            long outSize = size;
            if (size > input.Length)
                size = input.Length;
            char[] output = new char[outSize];
            int i;
            for (i = 0; i < outSize; i++)
                output[i] = (char)input[i];

            StringBuilder sb = new StringBuilder(output.Length);
            sb.Append(output);

            return sb.ToString();
        }

        public bool Write(byte[] bytes, int start, int length)
        {
            if (DeviceError)
                return false;
            else
                try
                {
                    if (SystemServices.LogMessageContent)
                        Buffers.ByteBuffer.FormatDump(bytes, start, length, SystemServices);

                    Stream stream = GetStream();
                    stream.Write(bytes, start, length);
                    return true;
                }
                catch (Exception e)
                {
                    if (!DeviceError)
                    {
                        LogMessage("Write", "Exception writing to stream: " + e.Message, LogEntryType.ErrorMessage);
                        DeviceError = true;
                    }
                    else
                    {
                        if (Reset())
                            DeviceError = false;
                    }
                    return false;
                }
        }

        public virtual void StartBuffer()
        {
            ByteBuffer byteBuffer = new ByteBuffer(SystemServices, this);
            // Original ByteBuffer below
            //ByteBufferThread byteBuffer = new ByteBufferThread(GenThreadManager, SystemServices, this, 10, 1000, 30);
            //GenThreadId = GenThreadManager.AddThread(byteBuffer, DeviceName);
            //GenThreadManager.StartThread(GenThreadId);
            ByteBuffer = byteBuffer;
        }

        public virtual void StopBuffer()
        {
            // Original ByteBuffer below
            //GenThreadManager.StopThread(GenThreadId);
        }

        public MatchInfo ReadFromBuffer(int length, int minLength, out byte[] bytes, TimeSpan? wait = null, bool consume = true)
        {
            bytes = new byte[length];
            MatchInfo info = ByteBuffer.ReadFromBuffer(bytes, length, minLength, wait, consume);
            return info;
        }

        public MatchInfo FindInBuffer(byte[] pattern, int maxSkip, bool extractSkipped, out DataChunk skipped, TimeSpan? wait = null, bool consume = true)
        {
            DataChunk chunk;
            
            MatchInfo info = ByteBuffer.FindInBuffer(pattern, maxSkip, consume, wait, extractSkipped, out chunk);
                
            skipped = chunk;
            return info;    
        }

        public virtual bool Reset()
        {
            return false;
        }
    }
}
