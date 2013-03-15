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
using System.Xml;

namespace MackayFisher.Utilities
{
    public class XmlFileWriter : TextFileWriter
    {
        private XmlWriter xmlWriter = null;
        private XmlWriterSettings xmlWriterSettings = null;
        private String RootName;

        public XmlFileWriter(String rootName, String fileNameBase, String directory = null, String nameExtension = ".xml") : base(fileNameBase, directory, nameExtension)
        {
            RootName = rootName;
            xmlWriterSettings = new XmlWriterSettings();
            xmlWriterSettings.Indent = true;
            xmlWriterSettings.IndentChars = "    ";
            xmlWriterSettings.NewLineHandling = NewLineHandling.Replace;
        }

        public XmlWriterSettings XmlWriterSettings { get { return xmlWriterSettings; } set { xmlWriterSettings = value; } }

        public override void CloseFile()
        {
            if (xmlWriter != null)
            {
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndDocument();
                xmlWriter.Close();
                xmlWriter = null;
            }
            base.CloseFile();            
        }

        public void WriteXmlDocument(XmlDocument document)
        {
            TextWriter.Write(document.InnerText);
        }

        public XmlWriter XmlWriter
        {
            get
            {
                if (!IsOpen)
                    OpenFile();

                if (xmlWriter == null)
                {
                    if (xmlWriterSettings == null)
                        xmlWriter = XmlWriter.Create(TextWriter);
                    else
                        xmlWriter = XmlWriter.Create(TextWriter, xmlWriterSettings);
                    xmlWriter.WriteStartDocument();
                    xmlWriter.WriteStartElement(RootName);
                }

                return xmlWriter;
            }
        }
    }
}
