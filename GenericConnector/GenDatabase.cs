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
using MackayFisher.Utilities;

namespace GenericConnector
{
    public class GenDatabase
    {
        private String Server;
        private String Database;
        private String Username;
        private String Password;
        private String DefaultDirectory;

        // used to protect database connector from concurrent access
        // created this when Connections list was removed
        // may not be needed but adds some protection around connection creation
        // and can be expanded to other DB operations if required
        internal Object DBLock;

        internal IUtilityLog UtilityLog;

        private GenConnection GlobalConnection;

        // list used to track open connections - diagnostics only
        // private List<GenConnection> Connections;
        int ConnectionCount;


        public String ConnectionString
        {
            get;
            private set;
        }

        private String DBType;
        private String ProviderType;
        private String ProviderName;

        public String OleDbName
        {
            get;
            private set;
        }

        public DbProviderFactory DbProviderFactory
        {
            get;
            private set;
        }

        public GenDBType GenDBType
        {
            get;
            private set;
        }

        public GenProviderType GenProviderType
        {
            get;
            private set;
        }

        public static GenDBType GetDBType(String dbType)
        {
            if (dbType == "MySql")
                return GenDBType.MySql;
            else if (dbType == "Jet")
                return GenDBType.Jet;
            else if (dbType == "SQLite")
                return GenDBType.SQLite;
            else if (dbType == "SQL Server")
                return GenDBType.SQLServer;
            else if (dbType == "Oracle")
                return GenDBType.Oracle;

            throw new GenException(GenExceptionType.InvalidDatabaseType, dbType);
        }

        public static GenProviderType GetProviderType(String providerType)
        {
            if (providerType == "OleDb")
                return GenProviderType.OleDb;
            else if (providerType == "ODBC")
                return GenProviderType.ODBC;
            else if (providerType == "Proprietary")
                return GenProviderType.Proprietary;

            throw new GenException(GenExceptionType.InvalidProviderType, providerType);
        }


        public GenDatabase(String server, String database, String username, String password, String dbType,
            String providerType, String providerName, String oleDbName, String connectionString, String defaultDirectory, IUtilityLog utilityLog)
        {
            DBLock = new Object();

            Server = server;
            Database = database;
            Username = username;
            Password = password;
            OleDbName = oleDbName;
            ProviderType = providerType;
            ProviderName = providerName;
            ConnectionString = connectionString;
            DBType = dbType;
            DefaultDirectory = defaultDirectory;

            UtilityLog = utilityLog;

            GenDBType = GetDBType(dbType);
            GenProviderType = GetProviderType(providerType);
            GlobalConnection = null;
            // Connections = new List<GenConnection>(10);
            ConnectionCount = 0;

            try
            {
                DbProviderFactory = DbProviderFactories.GetFactory(providerName);
            }
            catch (Exception e)
            {
                throw new GenException(GenExceptionType.ProviderNotFound, "GenDatabase - constructor - cannot find provider: " + providerName + " : " + e.Message, e);
            }

            if (ConnectionString == "")
            {
                ConnectionString = BuildConnectionString();
            }
        }

        public GenConnection NewConnection()
        {
            // SQLite works best when all SQL uses the same connection
            // This avoids Database is Locked error messages
            // GenDatabase creates a single Global connection for SQLite use
            lock (DBLock)
            {
                if (GenDBType == GenericConnector.GenDBType.SQLite)
                {
                    if (GlobalConnection == null)
                    {
                        GlobalConnection = new GenConnection(this, true);
                        GlobalConnection.Open();
                        ConnectionCount++;
                        // Connections.Add(GlobalConnection);
                        if (UtilityLog.LogDatabase)
                            UtilityLog.LogMessage("GenDatabase.NewConnection", "Global connection created - id: " + GlobalConnection.Id + 
                                " - thread: " + GlobalConnection.CreationThread + " - count: " + ConnectionCount, LogEntryType.Database);
                    }
                    return GlobalConnection;
                }
                else
                {
                    GenConnection con = new GenConnection(this);
                    con.Open();
                    ConnectionCount++;
                    // Connections.Add(con);
                    if (UtilityLog.LogDatabase)
                        UtilityLog.LogMessage("GenDatabase.NewConnection", "Connection created - id: " + con.Id +
                                " - thread: " +con.CreationThread + " - count: " + ConnectionCount, LogEntryType.Database);
                    return con;
                }
            }
        }

        private String BuildConnectionString()
        {
            try
            {
                ConnectionStringBuilder conBuilder = new ConnectionStringBuilder(this);
                if (Server != "")
                    conBuilder.Server = Server;
                if (Username != "")
                    conBuilder.UserID = Username;
                //if (GenDBType != GenDBType.Jet)
                //    conBuilder.PersistSecurityInfo = true;
                //if (GenDBType == GenericConnector.GenDBType.Jet)
                    //conBuilder.ExtendedAnsiSQL = "1";
                if (GenDBType == GenDBType.Jet || GenDBType == GenDBType.SQLite)
                    conBuilder.Database = System.IO.Path.Combine(DefaultDirectory, Database);
                else
                    conBuilder.Database = Database;
                if (Password != "")
                    conBuilder.Password = Password;
                return conBuilder.ToString();
            }
            catch (Exception e)
            {
                throw new GenException(GenExceptionType.UnexpectedDbError, "BuildConnectionString: " + e.Message, e);
            }
        }

        internal void ConnectionClosed(GenConnection connection)
        {
            lock (DBLock)
            {
                // This list serves no purpose
                // Connections.Remove(connection);
                ConnectionCount--;
                if (UtilityLog.LogDatabase)
                    UtilityLog.LogMessage("GenDatabase.ConnectionClosed", "id: " + connection.Id +
                            " - creation thread: " + connection.CreationThread +
                            " - closing thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId +
                            " - count: " + ConnectionCount, LogEntryType.Database);
            }
        }
    }
}
