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
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GenericConnector
{
    public class GenConnection : DbConnection
    {
        private static int NextId = 0;

        public int Id
        {
            get;
            private set;
        }

        public int CreationThread
        {
            get;
            private set;
        }

        public DateTime CreationTime
        {
            get;
            private set;
        }

        protected DbConnection DbConnection;

        // SQLite works best when all SQL uses the same connection
        // This avoids Database is Locked error messages
        // GenDatabase creates a single Global connection for SQLite use
        private bool IsGlobal;

        public GenDatabase GenDatabase
        {
            get;
            private set;
        }

        internal GenConnection(GenDatabase db, bool isGlobal = false)
        {
            DbConnection = db.DbProviderFactory.CreateConnection();
            DbConnection.ConnectionString = db.ConnectionString;
            GenDatabase = db;
            IsGlobal = isGlobal;
            Id = NextId++;
            CreationThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            CreationTime = DateTime.Now;
        }

        protected override bool CanRaiseEvents
        {
            get
            {
                // the following is a guess as I cannot find any info on the MySql 
                // implementation of this protected member
                return true;
            }
        }

        public GenDBType DBType
        {
            get
            {
                return GenDatabase.GenDBType;
            }
        }

        public override int ConnectionTimeout
        {
            get
            {
                return DbConnection.ConnectionTimeout;
            }
        }

        public override string ConnectionString
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override string Database
        {
            get { throw new NotImplementedException(); }
        }

        public override string DataSource
        {
            get { throw new NotImplementedException(); }
        }

        protected override DbProviderFactory DbProviderFactory
        {
            get
            {
                return base.DbProviderFactory;
            }
        }

        public DbConnection InnerConnection
        {
            get
            {
                return DbConnection;
            }
        }

        public override string ServerVersion
        {
            get { throw new NotImplementedException(); }
        }

        public override System.Data.ConnectionState State
        {
            get { throw new NotImplementedException(); }
        }

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
        {
            return DbConnection.BeginTransaction(isolationLevel);
        }

        public new DbTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel)
        {
            return DbConnection.BeginTransaction(isolationLevel);
        }

        public new DbTransaction BeginTransaction()
        {
            return DbConnection.BeginTransaction();
        }

        public override void ChangeDatabase(string databaseName)
        {
            DbConnection.ChangeDatabase(databaseName);
        }

        public override void Close()
        {
            if (!IsGlobal)
            {
                DbConnection.Close();
                GenDatabase.ConnectionClosed(this);
            }
        }

        public new void Dispose()
        {
            if (!IsGlobal)
            {
                DbConnection.Dispose();
                base.Dispose();
            }
        }

        protected override DbCommand CreateDbCommand()
        {
            return DbConnection.CreateCommand();
        }

        public override void Open()
        {
            try
            {
                DbConnection.Open();
            }
            catch (Exception e)
            {
                throw new GenException(GenExceptionType.CannotConnectToDatabase, "GenConnection.Open: Error connecting to the database: " + e.Message, e);
            }
        }

        public override DataTable GetSchema()
        {
            return DbConnection.GetSchema();
        }

        public override DataTable GetSchema(String collectionName)
        {
            return DbConnection.GetSchema(collectionName);
        }

        public override DataTable GetSchema(String collectionName, String[] restrictionValues)
        {
            return DbConnection.GetSchema(collectionName, restrictionValues);
        }

        public DataRow GetSchemaTable(String tableName)
        {
            DataTable tableList;
            tableList = GetSchema();
            tableList = GetSchema("Tables");

            foreach (DataRow table in tableList.Rows)
            {
                if (table.ToString() == tableName)
                    return table;
            }
            return null;
        }

        /*
        private DataRow GetSchemaColumn(String tableName)
        {
            DataRow table = GetSchemaTable(tableName);
            if (table == null)
                return null;

            foreach (DataRow column in table.Columns)
            {
                if (table.ToString() == tableName)
                    return table;
            }
            return null;
        }
        */
        public bool SchemaHasTable(String tableName)
        {
            DataRow table = GetSchemaTable(tableName);
            return table != null;
        }
    }
}
