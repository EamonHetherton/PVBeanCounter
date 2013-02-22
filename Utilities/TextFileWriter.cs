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
using System.Threading;

namespace MackayFisher.Utilities
{
    public class TextFileWriter
    {
        private String TextFileFullName;
        private String TextFileName;
        private System.IO.TextWriter textWriter;
        private Mutex FileAccessMutex;

        private String FileNameBase;
        private String NameExtension;
        private String Directory;
        private String lastError = "";

        private bool autoDateVersion = false;
        private bool autoNumberVersion = false;
        private bool isNewFile = false;

        public TextFileWriter(String fileNameBase, String directory = null, String nameExtension = ".txt")
        {
            textWriter = null;
            FileNameBase = fileNameBase;
            NameExtension = nameExtension;
            Directory = directory;
            FileAccessMutex = new Mutex();
        }

        public System.IO.TextWriter TextWriter { get { return textWriter; } }

        public bool IsOpen { get { return textWriter != null; } }
        public bool IsNewFile { get { return isNewFile; } }
        public String LastError { get { return lastError; } }

        public bool GetFileAccessMutex(int timeOut)
        {
            return FileAccessMutex.WaitOne(timeOut);
        }

        public void ReleaseFileAccessMutex()
        {
            FileAccessMutex.ReleaseMutex();
        }

        public bool AutoDateVersion 
        {
            get
            {
                return autoDateVersion;
            }
            set
            {
                if (!IsOpen)
                    autoDateVersion = value;
                else
                    throw new Exception("TextFile.AutoDateVersion - Attempt to change name format of an open file");
            }
        }

        public bool AutoNumberVersion
        {
            get
            {
                return autoNumberVersion;
            }
            set
            {
                if (!IsOpen)
                    autoNumberVersion = value;
                else
                    throw new Exception("TextFile.AutoNumberVersion - Attempt to change name format of an open file");
            }
        }

        private String GetCandidateName(int version)
        {
            String name = "";

            if (Directory == null)
                Directory = "";

            name = FileNameBase;
            if (autoDateVersion)
                name += "_" + DateTime.Today.ToString("yyyyMMdd");
            if (autoNumberVersion)
                name += "_" + version.ToString();

            name += NameExtension;

            return System.IO.Path.Combine(Directory, name);
        }

        private System.IO.DirectoryInfo CheckDirectoryExists()
        {
            System.IO.DirectoryInfo directoryInfo = new System.IO.DirectoryInfo(Directory);
            if (!directoryInfo.Exists)
                directoryInfo.Create();

            return directoryInfo;
        }

        public virtual bool OpenFile()
        {
            try
            {
                if (textWriter != null)
                    CloseFile();

                bool found = false;
                System.IO.FileInfo fileInfo;

                if (Directory != null && Directory != "")
                    CheckDirectoryExists();

                int version = 0;
                do
                {
                    TextFileName = GetCandidateName(version++);
                    fileInfo = new System.IO.FileInfo(TextFileName);

                    if (autoNumberVersion)
                        found = !fileInfo.Exists; // find a version that does not exist
                    else
                        found = true; // does not matter if it exists
                }
                while (!found);

                TextFileFullName = fileInfo.Name;
                if (fileInfo.Exists)
                {
                    textWriter = fileInfo.AppendText();
                    isNewFile = false;
                }
                else
                {
                    textWriter = fileInfo.CreateText();
                    isNewFile = true;
                }
            }
            catch (Exception e)
            {
                lastError = e.Message;
                return false;
            }

            lastError = "";
            return true;
        }

        public virtual void CloseFile()
        {
            if (textWriter != null)
            {
                textWriter.Close();                    
                textWriter = null;
            }
        }
    }
}
