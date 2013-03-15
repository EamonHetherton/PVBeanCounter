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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeviceControl
{
    public enum PVExceptionType
    {
        UnexpectedDBError,
        UnexpectedError,
        DirectoryMissing,
        CannotCreateDirectory,
        CannotMoveFile,
        CannotConnectToDatabase,
        InvalidUsernamePassword,
        InvalidDatabaseType,
        ProcessFailed
    };

    public class PVException : Exception
    {
        private PVExceptionType PVExceptionType; 

        public PVException(PVExceptionType type, String desc) :base(desc)
        {
            PVExceptionType = type;
        }

        public PVException(PVExceptionType type, String desc, Exception innerException) : base(desc, innerException)
        {
            PVExceptionType = type;
        }

        public PVExceptionType Type 
        {   
            get
            {
                return PVExceptionType;
            }
        }
    }
}
