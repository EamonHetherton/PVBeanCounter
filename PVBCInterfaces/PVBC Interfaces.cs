/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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
using System.Threading;
using Conversations;
using GenericConnector;
using MackayFisher.Utilities;
using PVSettings;

namespace PVBCInterfaces
{

    public interface IEvents
    {
        void EmitEventTypes(bool updatedEvents);
        void NewStatusEvent(string statusType, string statusText);
        void ScanForEvents();
        ManualResetEvent PVEventReadyEvent { get; }
    }

    public interface IDeviceManager : GenThreadManagement.IGenThread
    {
        void CloseErrorLogger();
        ErrorLogger ErrorLogger { get; }
        String ManagerTypeName { get; }
        void ResetStartOfDay();
    }

}
