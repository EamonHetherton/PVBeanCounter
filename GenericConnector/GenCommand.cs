/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using System.Data.OleDb;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MackayFisher.Utilities;

namespace GenericConnector
{
    public class GenCommand : DbCommand
    {
        private DbCommand DbCommand;
        private GenDatabase GenDatabase;
        private GenConnection GenConnection;

        public override string CommandText
        {
            get
            {
                return DbCommand.CommandText;
            }
            set
            {
                DbCommand.CommandText = value;
            }
        }

        public override int CommandTimeout
        {
            get
            {
                return DbCommand.CommandTimeout;
            }
            set
            {
                DbCommand.CommandTimeout = value;
            }
        }

        public override System.Data.CommandType CommandType
        {
            get
            {
                return DbCommand.CommandType;
            }
            set
            {
                DbCommand.CommandType = value;
            }
        }

        protected override DbTransaction DbTransaction
        {
            get
            {
                return DbCommand.Transaction;
            }
            set
            {
                DbCommand.Transaction = (DbTransaction)value;
            }
        }

        public override bool DesignTimeVisible
        {
            get
            {
                return DbCommand.DesignTimeVisible;

            }
            set
            {
                DbCommand.DesignTimeVisible = value;
            }
        }

        protected override DbParameterCollection DbParameterCollection
        {
            get 
            {
                return DbCommand.Parameters;
            }
        }

        protected override DbConnection DbConnection
        {
            get
            {
                return DbCommand.Connection;
            }
            set
            {
                DbCommand.Connection = (DbConnection)value;
            }
        }

        public override System.Data.UpdateRowSource UpdatedRowSource
        {
            get
            {
                return DbCommand.UpdatedRowSource;
            }
            set
            {
                DbCommand.UpdatedRowSource = value;
            }
        }

        public GenCommand(GenDatabase db)
            : base()
        {
            GenDatabase = db;
            DbCommand = db.DbProviderFactory.CreateCommand();
        }

        public GenCommand(String commandText, GenConnection connection)
            : base()
        {
            GenDatabase = connection.GenDatabase;
            DbCommand = GenDatabase.DbProviderFactory.CreateCommand();
            DbCommand.CommandText = commandText;
            DbCommand.Connection = connection.InnerConnection;
            GenConnection = connection;

            if (GenDatabase.UtilityLog.LogDatabase)
                GenDatabase.UtilityLog.LogMessage("GenCommand.GenCommand", "id: " + connection.Id +
                        " - creation thread: " + connection.CreationThread +
                        " - this thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId +
                        " - command: " + commandText, LogEntryType.Database);
        }

        public DbParameter AddParameterWithValue(String parameter, object value)
        {
            object intValue;

            if (value == null)
                intValue = DBNull.Value;
            else
                intValue = value;

            if (GenDatabase.GenProviderType == GenericConnector.GenProviderType.OleDb)
            {
                OleDbParameter param = ((OleDbCommand)DbCommand).Parameters.AddWithValue(parameter, intValue);
                //there seems to be a bug related to type selection for datetime values
                if (param.OleDbType == OleDbType.DBTimeStamp || param.OleDbType == OleDbType.DBDate)
                    param.OleDbType = OleDbType.Date;

                return param;
            }
            else
            {
                DbParameter param = GenDatabase.DbProviderFactory.CreateParameter();
                param.ParameterName = parameter; 
                param.Value = intValue;
                DbCommand.Parameters.Add(param);

                return param;
            }
        }

        public void AddRoundedParameterWithValue(String paramName, double? value, int digits)
        {
            if (value.HasValue)
                AddParameterWithValue(paramName, Math.Round(value.Value, digits));
            else
                AddParameterWithValue(paramName, null);
        }

        public void AddNullableBooleanParameterWithValue(String paramName, bool? value)
        {
            if (value.HasValue)
                if (GenDatabase.GenDBType == GenDBType.SQLite)
                    AddParameterWithValue(paramName, value.Value ? "Y" : "N"); // char is recorded as a text numeric character value - use string instead
                else
                    AddParameterWithValue(paramName, value.Value ? 'Y' : 'N' );
            else
                AddParameterWithValue(paramName, null);
        }

        public void AddBooleanParameterWithValue(String paramName, bool value)
        {
            AddParameterWithValue(paramName, value ? 'Y' : 'N');
        }

        public override void Cancel()
        {
                DbCommand.Cancel();
        }

        protected override DbParameter CreateDbParameter()
        {
                return DbCommand.CreateParameter();
        }

        private void LogCommandUsage(String function)
        {
            if (GenDatabase.UtilityLog.LogDatabase)
                GenDatabase.UtilityLog.LogMessage("GenCommand." + function, "id: " + GenConnection.Id +
                        " - creation thread: " + GenConnection.CreationThread +
                        " - this thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId +
                        " - command: " + DbCommand.CommandText, LogEntryType.Database);
        }

        protected override DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior)
        {
            LogCommandUsage("ExecuteDbDataReader");
            return new GenDataReader(DbCommand.ExecuteReader(behavior));
        }

        public override int ExecuteNonQuery()
        {
            LogCommandUsage("ExecuteNonQuery");
            try
            {
                return DbCommand.ExecuteNonQuery();
            }
            catch (DbException e)
            {
                if ((e.ErrorCode == -2147467259) || (e.ErrorCode == -2146233088) || e.ErrorCode == -2146232060)
                    throw new GenException(GenExceptionType.UniqueConstraintRowExists, "ExecuteNonQuery: " + e.Message, e);
                else
                    throw new GenException(GenExceptionType.UnexpectedDbError, "ExecuteNonQuery: " + e.ErrorCode + " - " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new GenException(GenExceptionType.UnexpectedError, "ExecuteNonQuery: " + e.Message, e);
            }
        }

        public override object ExecuteScalar()
        {
            LogCommandUsage("ExecuteScalar");
            return DbCommand.ExecuteScalar();
        }

        public new DbDataReader ExecuteReader()
        {
            LogCommandUsage("ExecuteReader");
            return new GenDataReader(DbCommand.ExecuteReader());
        }

        public override void Prepare()
        {
            DbCommand.Prepare();
        }
    }
}
