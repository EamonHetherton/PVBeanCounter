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
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GenericConnector
{
   public enum GenExceptionType
    {   
        CannotConnectToDatabase,
        InvalidUsernamePassword,
        InvalidDatabaseType,
        InvalidProviderType,
        UniqueConstraintRowExists,
        UnexpectedDbError,
        UnexpectedError,
        NoRowsReturned,
        ProviderNotFound
    }

   public class GenException : Exception
   {
       private GenExceptionType GenExceptionType;

       public GenException(GenExceptionType type, String description)
           : base(description)
       {
           GenExceptionType = type;
       }

       public GenException(GenExceptionType type, String description, Exception innerException)
           : base(description, innerException)
       {
           GenExceptionType = type;
       }

       public GenExceptionType Type
       {
           get
           {
               return GenExceptionType;
           }
       }
   }
}
