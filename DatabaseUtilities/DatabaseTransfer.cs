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
using MackayFisher.Utilities;

namespace DatabaseUtilities
{
    public class DatabaseTransfer
    {
        private SchemaStructure SchemaStructure;
        private DBParameters FromDBParameters;
        private DBParameters ToDBParameters;
        private SystemServices SystemServices;

        private Database FromDB;
        private Database ToDB;

        public DatabaseTransfer(SchemaStructure structure, String defaultDirectory, DBParameters fromDBParameters, DBParameters toDBParameters, SystemServices services)
        {
            SystemServices = services;
            SchemaStructure = structure;
            FromDBParameters = fromDBParameters;
            ToDBParameters = toDBParameters;
            FromDB = null;
            ToDB = null;
        }

        private bool CopyTableFromTo(TableStructure table)
        {
            String cmdSelect = FromDB.CreateTableSelect(table);
            String cmdDel = ToDB.CreateTableDeleteAll(table);
            String cmdInsert = ToDB.CreateTableInsert(table);

            return true;
        }

        public bool CopyFromTo()
        {
            FromDB = new Database(FromDBParameters, SystemServices);
            ToDB = new Database(ToDBParameters, SystemServices);

            bool result = true;

            foreach (TableStructure table in SchemaStructure.Tables)
                result &= CopyTableFromTo(table);

            return result;
        }
        
    }
}
