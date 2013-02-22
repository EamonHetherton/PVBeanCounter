/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
using GenericConnector;
using MackayFisher.Utilities;
using PVInverterManagement;

namespace DatabaseUtilities
{
    public struct DBParameters
    {
        public String Host;
        public String DatabaseName;
        public String UserName;
        public String Password;
        public String DatabaseType;
        public String ProviderType;
        public String ProviderName;
        public String OleDbName;
        public String ConnectionString;
        public String DbDirectory;
    }

    public class Database
    {
        private SystemServices SystemServices;

        private GenDatabase DB;

        private DBParameters DBParameters;

        public Database(DBParameters parameters, SystemServices systemServices)
        {
            DBParameters = parameters;
            DB = null;

            SystemServices = systemServices;
        }

        public void Open()
        {
            DB = new GenDatabase(DBParameters.Host, DBParameters.DatabaseName, DBParameters.UserName, DBParameters.Password,
                DBParameters.DatabaseType, DBParameters.ProviderType, DBParameters.ProviderName, DBParameters.OleDbName,
                DBParameters.ConnectionString, DBParameters.DbDirectory, SystemServices);
        }

        public void Close()
        {
            DB = null;
        }

        private String GetDBTableName(TableStructure table)
        {
            if (DB.GenDBType == GenDBType.SQLServer)
                return "[" + table.Name.ToLower() + "]";
            else if (DB.GenDBType == GenDBType.MySql)
                return "`" + table.Name.ToLower() + "`";
            else if (DB.GenDBType == GenDBType.SQLite)
                return "[" + table.Name + "]";
            else if (DB.GenDBType == GenDBType.Jet)
                return table.Name.ToLower();
            else
            {
                throw new PVInverterManagement.PVException(PVExceptionType.InvalidDatabaseType, "GetDBTableName: - Unhandled Database Type: " + DB.GenDBType);
            }
        }

        private String GetDBColumnName(ColumnStructure column)
        {
            if (DB.GenDBType == GenDBType.SQLServer)
                return "[" + column.Name + "]";
            else if (DB.GenDBType == GenDBType.MySql)
                return "`" + column.Name + "`";
            else if (DB.GenDBType == GenDBType.SQLite)
                return "[" + column.Name + "]";
            else if (DB.GenDBType == GenDBType.Jet)
                return "[" + column.Name + "]";
            else
            {
                throw new PVInverterManagement.PVException(PVExceptionType.InvalidDatabaseType, "GetDBColumnName - Unhandled Database Type: " + DB.GenDBType);
            }
        }

        private String GetDBColumnParameter(ColumnStructure column)
        {
            return "@" + column.Name.Replace(' ', '_');
        }

        public String CreateTableSelect(TableStructure table)
        {
            String cmdStr = "select ";
            int colCount = 0;

            foreach (ColumnStructure col in table.Columns)
            {
                if (colCount++ > 0)
                    cmdStr += ", ";

                cmdStr += GetDBColumnName(col);
            }
            cmdStr += " from " + GetDBTableName(table);

            if (table.PrimaryKey != null)
            {
                int orderCount = 0;
                String orderBy = "order by ";
                foreach (OrderItem item in table.PrimaryKey.Columns)
                {
                    if (orderCount++ > 0)
                        orderBy += ", ";

                    orderBy += GetDBColumnName(item.Column);

                    if (!item.IsAscending)
                        orderBy += " DESC";
                }

                if (orderCount > 0)
                    cmdStr += orderBy;
            }

            return cmdStr;            
        }

        public String CreateTableInsert(TableStructure table)
        {
            String columnList = "";
            String valueList = "";
            int colCount = 0;

            foreach (ColumnStructure col in table.Columns)
            {
                if (colCount++ > 0)
                {
                    columnList += ", ";
                    valueList += ", ";
                }

                columnList += GetDBColumnName(col);
                valueList += GetDBColumnParameter(col);
            }
            String cmdStr = "insert into " + GetDBTableName(table) + " ( " + columnList + " ) values ( " + valueList + " )";
            return cmdStr;            
        }

        public String CreateTableDeleteAll(TableStructure table)
        {
            String cmdStr = "delete from " + GetDBTableName(table);
            return cmdStr;
        }
    }
}
