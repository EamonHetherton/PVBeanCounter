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
using System.Data.OleDb;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GenericConnector
{
    public enum GenDBType
    {
        Jet,
        SQLServer,
        MySql,
        Oracle,
        SQLite
    }

    public enum GenProviderType
    {
        OleDb,
        ODBC,
        Proprietary
    }

    public class ConnectionStringBuilder
    {
        private DbConnectionStringBuilder DbConnectionStringBuilder;
        private GenDatabase GenDatabase;

        public ConnectionStringBuilder(GenDatabase db)
        {
            GenDatabase = db;
            DbConnectionStringBuilder = GenDatabase.DbProviderFactory.CreateConnectionStringBuilder();
            if (db.GenProviderType == GenProviderType.OleDb)
                ((OleDbConnectionStringBuilder)DbConnectionStringBuilder).Provider = GenDatabase.OleDbName;
        }

        public ConnectionStringBuilder(String connectionString, GenDatabase db)
        {
            GenDatabase = db;
            DbConnectionStringBuilder = GenDatabase.DbProviderFactory.CreateConnectionStringBuilder();
            DbConnectionStringBuilder.ConnectionString = connectionString;
            if (GenDatabase.GenProviderType == GenProviderType.OleDb)
                ((OleDbConnectionStringBuilder)DbConnectionStringBuilder).Provider = GenDatabase.OleDbName;
        }

        public String Server
        {
            get
            {
                if (GenDatabase.GenDBType == GenDBType.MySql)
                    return DbConnectionStringBuilder["Server"].ToString();
                else if (GenDatabase.GenDBType == GenDBType.SQLServer)
                    return DbConnectionStringBuilder["Data Source"].ToString();
                else
                    return "not used";
            }

            set
            {
                if (GenDatabase.GenDBType == GenDBType.MySql)
                    DbConnectionStringBuilder["Server"] = value;
                if (GenDatabase.GenDBType == GenDBType.SQLServer)
                {
                    DbConnectionStringBuilder["Data Source"] = value;
                    DbConnectionStringBuilder["Integrated Security"] = (UserID.Trim() == "");
                }
            }
        }

        public String Database
        {
            get
            {
                if (GenDatabase.GenDBType == GenDBType.MySql)
                    return DbConnectionStringBuilder["Database"].ToString();
                else if (GenDatabase.GenDBType == GenDBType.Jet)
                    return DbConnectionStringBuilder["Data Source"].ToString();
                else if (GenDatabase.GenDBType == GenDBType.SQLite)
                    return DbConnectionStringBuilder["Data Source"].ToString();
                else if (GenDatabase.GenDBType == GenDBType.SQLServer)
                    return DbConnectionStringBuilder["Initial Catalog"].ToString();
                else
                    return DbConnectionStringBuilder["DataSource"].ToString();
            }

            set
            {
                if (GenDatabase.GenDBType == GenDBType.MySql)
                    DbConnectionStringBuilder["Database"] = value;
                else if (GenDatabase.GenDBType == GenDBType.Jet)
                    DbConnectionStringBuilder["Data Source"] = value;
                else if (GenDatabase.GenDBType == GenDBType.SQLite)
                    DbConnectionStringBuilder["Data Source"] = value;
                else if (GenDatabase.GenDBType == GenDBType.SQLServer)
                    DbConnectionStringBuilder["Initial Catalog"] = value;
                else
                    DbConnectionStringBuilder["DataSource"] = value;
            }
        }

        public String UserID
        {
            get
            {
                if (GenDatabase.GenDBType == GenDBType.MySql)
                    return DbConnectionStringBuilder["UserID"].ToString();
                else if (GenDatabase.GenDBType == GenDBType.SQLServer)
                    return DbConnectionStringBuilder["User ID"].ToString();
                else
                    return "not used";
            }

            set
            {
                if (GenDatabase.GenDBType == GenDBType.MySql)
                    DbConnectionStringBuilder["UserID"] = value;
                else if (GenDatabase.GenDBType == GenDBType.SQLServer)
                {
                    DbConnectionStringBuilder["User ID"] = value;
                    DbConnectionStringBuilder["Integrated Security"] = (value.Trim() == "");
                }
            }
        }

        public String Password
        {
            get
            {
                if (GenDatabase.GenDBType == GenDBType.MySql)
                    return DbConnectionStringBuilder["Password"].ToString();
                else if (GenDatabase.GenDBType == GenDBType.SQLServer)
                    return DbConnectionStringBuilder["Password"].ToString();
                else
                    return "not used";
            }

            set
            {
                if (GenDatabase.GenDBType == GenDBType.MySql)
                    DbConnectionStringBuilder["Password"] = value;
                else if (GenDatabase.GenDBType == GenDBType.SQLServer)
                    DbConnectionStringBuilder["Password"] = value;
            }
        }

        public String ExtendedAnsiSQL
        {
            get
            {
                if (GenDatabase.GenDBType == GenDBType.Jet)
                    return DbConnectionStringBuilder["ExtendedAnsiSQL"].ToString();
                else
                    return "not used";
            }

            set
            {
                if (GenDatabase.GenDBType == GenDBType.Jet)
                    DbConnectionStringBuilder["ExtendedAnsiSQL"] = value;
            }
        }

        public bool PersistSecurityInfo
        {
            get
            {
                return (bool)DbConnectionStringBuilder["PersistSecutityInfo"];
            }

            set
            {
                DbConnectionStringBuilder["PersistSecutityInfo"] = value;
            }
        }

        public override String ToString()
        {
            return DbConnectionStringBuilder.ToString();
        }

    }
}
