/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GenericConnector
{
    public class GenDataReader : DbDataReader
    {
        private DbDataReader DbDataReader;

        public override int Depth
        {
            get { return DbDataReader.Depth; }
        }

        public override int FieldCount
        {
            get { return DbDataReader.FieldCount; }
        }

        public override bool HasRows
        {
            get { return DbDataReader.HasRows; }
        }

        public override bool IsClosed
        {
            get { return DbDataReader.IsClosed; }
        }

        public override int RecordsAffected
        {
            get { return DbDataReader.RecordsAffected; }
        }

        public override object this[int ordinal]
        {
            get { return DbDataReader[ordinal]; }
        }

        public override object this[string name]
        {
            get { return DbDataReader[name]; }
        }

        public GenDataReader(DbDataReader dataReader)
            : base()
        {
            DbDataReader = dataReader;
        }

        public override void Close()
        {
            DbDataReader.Close();
        }

        public override bool GetBoolean(int ordinal)
        {
            return DbDataReader.GetBoolean(ordinal);
        }

        public override byte GetByte(int ordinal)
        {
            return DbDataReader.GetByte(ordinal);
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            return DbDataReader.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
        }

        public override char GetChar(int ordinal)
        {
            return DbDataReader.GetChar(ordinal);
        }

        public bool? GetBoolFromChar(int ordinal)
        {
            if (DbDataReader.IsDBNull(ordinal))
                return null;
            char val;
            try
            {
                val = DbDataReader.GetChar(ordinal);
            }
            catch  // some databases do not support char data types; these should allow use of string
            {
                val = DbDataReader.GetString(ordinal)[0];
            }
            return (val == 'Y' || val == 'y' );
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            return DbDataReader.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
        }

        public override string GetDataTypeName(int ordinal)
        {
            return DbDataReader.GetDataTypeName(ordinal);
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return DbDataReader.GetDateTime(ordinal);
        }

        public DateTime GetDateTime(string name)
        {
            return GetDateTime(DbDataReader.GetOrdinal(name));
        }

        public override decimal GetDecimal(int ordinal)
        {
            return DbDataReader.GetDecimal(ordinal);
        }

        public override double GetDouble(int ordinal)
        {
            Type t = DbDataReader.GetFieldType(ordinal);
                
            if (t == typeof(Single))
                return (Double)DbDataReader.GetFloat(ordinal);
            else
                return DbDataReader.GetDouble(ordinal);
        }

        public double? GetNullableDouble(int ordinal)
        {
            if (DbDataReader.IsDBNull(ordinal))
                return null;
            return GetDouble(ordinal);
        }

        public Double GetDouble(String name)
        {
            return GetDouble(DbDataReader.GetOrdinal(name));
        }


        public override System.Collections.IEnumerator GetEnumerator()
        {
            return DbDataReader.GetEnumerator();
        }

        public override Type GetFieldType(int ordinal)
        {
            return DbDataReader.GetFieldType(ordinal);
        }

        public override float GetFloat(int ordinal)
        {
            return DbDataReader.GetFloat(ordinal);
        }

        public float? GetNullableFloat(int ordinal)
        {
            if (DbDataReader.IsDBNull(ordinal))
                return null;
            return GetFloat(ordinal);
        }

        public override Guid GetGuid(int ordinal)
        {
            return DbDataReader.GetGuid(ordinal);
        }

        public override short GetInt16(int ordinal)
        {
            Type t = DbDataReader.GetFieldType(ordinal);
            if (t == typeof(Int32))
                return (short)DbDataReader.GetInt32(ordinal);
            else if (t == typeof(byte))
                return DbDataReader.GetByte(ordinal);
            else if (t == typeof(Boolean))
            {
                bool b = DbDataReader.GetBoolean(ordinal);
                return (short)(b ? 1 : 0);
            }
            else
                return DbDataReader.GetInt16(ordinal);
        }

        public int GetInt16(string name)
        {
            return GetInt16(DbDataReader.GetOrdinal(name));
        }

        public override int GetInt32(int ordinal)
        {
            Type t = DbDataReader.GetFieldType(ordinal);
            if (t == typeof(Int16))
                return DbDataReader.GetInt16(ordinal);
            else if (t == typeof(byte))
                return DbDataReader.GetByte(ordinal);
            else if (t == typeof(Boolean))
            {
                bool b = DbDataReader.GetBoolean(ordinal);
                return (int)(b ? 1 : 0);
            }
            else
                return DbDataReader.GetInt32(ordinal);
        }

        public int? GetNullableInt32(int ordinal)
        {
            if (DbDataReader.IsDBNull(ordinal))
                return null;
            return GetInt32(ordinal);
        }

        public int GetInt32(string name)
        {
            return GetInt32(DbDataReader.GetOrdinal(name));
        }

        public override long GetInt64(int ordinal)
        {
            Type t = DbDataReader.GetFieldType(ordinal);
            if (t == typeof(Int16))
                return DbDataReader.GetInt16(ordinal);
            else if (t == typeof(byte))
                return DbDataReader.GetByte(ordinal);
            else if (t == typeof(Int32))
                return DbDataReader.GetInt32(ordinal);
            else if (t == typeof(Boolean))
            {
                bool b = DbDataReader.GetBoolean(ordinal);
                return (long)(b ? 1 : 0);
            }
            else
                return DbDataReader.GetInt64(ordinal);
        }

        public long? GetNullableInt64(int ordinal)
        {
            if (DbDataReader.IsDBNull(ordinal))
                return null;
            return GetInt64(ordinal);
        }

        public long GetInt64(string name)
        {
            return GetInt64(DbDataReader.GetOrdinal(name));
        }

        public override string GetName(int ordinal)
        {
            return DbDataReader.GetName(ordinal);
        }

        public override int GetOrdinal(string name)
        {
            return DbDataReader.GetOrdinal(name);
        }

        public override System.Data.DataTable GetSchemaTable()
        {
            return DbDataReader.GetSchemaTable();
        }

        public override string GetString(int ordinal)
        {
            if (DbDataReader.IsDBNull(ordinal))
                return null;
            else
                return DbDataReader.GetString(ordinal);
        }

        public string GetString(string name)
        {
            int ordinal = DbDataReader.GetOrdinal(name);
            if (DbDataReader.IsDBNull(ordinal))
                return null;
            else
                return DbDataReader.GetString(ordinal);
        }

        public override object GetValue(int ordinal)
        {
            return DbDataReader.GetValue(ordinal);
        }

        public override int GetValues(object[] values)
        {
            return DbDataReader.GetValues(values);
        }

        public override bool IsDBNull(int ordinal)
        {
            return DbDataReader.IsDBNull(ordinal);
        }

        public bool IsDBNull(String name)
        {
            int ordinal = DbDataReader.GetOrdinal(name);
            return DbDataReader.IsDBNull(ordinal);
        }

        public override bool NextResult()
        {
            return DbDataReader.NextResult();
        }

        public override bool Read()
        {
            return DbDataReader.Read();
        }
    }
}
