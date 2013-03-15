/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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

namespace MackayFisher.Utilities
{
    public class ErrorLogger
    {
        private XmlFileWriter ErrorWriter = null;
        private SystemServices SystemServices;
        private String ManagerType;
        private String ManagerDesc;
        private String Directory;

        public ErrorLogger(SystemServices systemServices, String managerType, String managerDesc, String directory)
        {
            SystemServices = systemServices;
            ManagerType = managerType;
            ManagerDesc = managerDesc;
            Directory = directory;
        }

        ~ErrorLogger()
        {
            if (ErrorWriter != null)
                Close();
        }

        private void Open()
        {
            ErrorWriter = new XmlFileWriter("errors", ManagerType + "_" + ManagerDesc + "_Err", Directory);
            ErrorWriter.AutoDateVersion = true;
            ErrorWriter.AutoNumberVersion = true;
        }

        public void Close()
        {
            if (ErrorWriter == null)
                return;
            bool haveMutex = ErrorWriter.GetFileAccessMutex(1000);
            if (!haveMutex)
                SystemServices.LogMessage("ErrorLogger", "Close - Cannot acquire mutex", LogEntryType.ErrorMessage);
            ErrorWriter.CloseFile();
            if (haveMutex)
                ErrorWriter.ReleaseFileAccessMutex();
            ErrorWriter = null;
        }

        public void LogMessage(string messageType, string address, DateTime time, String message = "", int groupSize = 1, byte[] registers = null)
        {
            if (ErrorWriter == null)
                Open();
            bool haveMutex = false;
            try
            {
                haveMutex = ErrorWriter.GetFileAccessMutex(System.Threading.Timeout.Infinite);
                System.Xml.XmlWriter writer = ErrorWriter.XmlWriter;
                writer.WriteStartElement(messageType);
                writer.WriteElementString("time", System.Xml.XmlConvert.ToString(time, System.Xml.XmlDateTimeSerializationMode.Local));
                if (address != "")
                    writer.WriteElementString("addr", address);
                if (message != "")
                    writer.WriteElementString("msg", message);
                if (registers != null)
                    writer.WriteElementString("registers", SystemServices.BytesToHex(ref registers, groupSize, " ", "0x"));
                writer.WriteEndElement();
                writer.Flush();
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("ErrorLogger", "LogMessage - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (haveMutex)
                    ErrorWriter.ReleaseFileAccessMutex();
            }
        }

        public void LogError(string address, DateTime time, String message = "", int groupSize = 1, byte[] registers = null)
        {
            LogMessage("error", address, time, message, groupSize, registers);
        }
    }
}
