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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;

namespace Algorithms
{
    public abstract class AlgorithmAction
    {
        public String Name { get; protected set; }
        public String Type { get; protected set; }
        protected DeviceAlgorithm Device;
        protected DeviceBlock DeviceBlock;
        protected ActionSettings ActionSettings;
        protected AlgorithmAction ParentAction;

        public bool ContinueOnFailure { get; protected set; }
        public bool ExitOnSuccess { get; protected set; }
        public bool ExitOnFailure { get; protected set; }
        public bool OnDbWriteOnly { get; protected set; }

        protected List<AlgorithmAction> Actions;
        protected List<ActionParameter> Parameters;
        

        public AlgorithmAction(DeviceAlgorithm device, AlgorithmAction algorithmAction, ActionSettings actionSettings)
        {
            Device = device;
            
            ParentAction = algorithmAction;
            ActionSettings = actionSettings;
            Type = actionSettings.Type;
            Name = actionSettings.Name;
            OnDbWriteOnly = actionSettings.OnDbWriteOnly;

            CommonConstruction();
        }

        protected void LogMessage(String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage("AlgorithmAction", message, logEntryType);
        }

        private void CommonConstruction()
        {
            ExitOnSuccess = ActionSettings.ExitOnSuccess;
            ExitOnFailure = ActionSettings.ExitOnFailure;
            if (ExitOnSuccess || ExitOnFailure)
                ContinueOnFailure = true;
            else
                ContinueOnFailure = ActionSettings.ContinueOnFailure;

            DeviceBlock = FindBlock();
            LoadActions();
            LoadParameters();
        }

        private void LoadActions()
        {
            Actions = new List<AlgorithmAction>();
            foreach (ActionSettings actionSettings in ActionSettings.ActionList)
            {
                AlgorithmAction action = null;

                if (actionSettings.Type == "GetBlock")
                    action = new AlgorithmAction_GetBlock(Device, this, actionSettings);
                else if (actionSettings.Type == "SendBlock")
                    action = new AlgorithmAction_SendBlock(Device, this, actionSettings);
                else if (actionSettings.Type == "LogError")
                    action = new AlgorithmAction_LogError(Device, this, actionSettings);
                else if (actionSettings.Type == "RepeatCountTimes")
                    action = new AlgorithmAction_RepeatCountTimes(Device, this, actionSettings);
                else if (actionSettings.Type == "Repeat")
                    action = new AlgorithmAction_Repeat(Device, this, actionSettings);

                Actions.Add(action);
            }
        }

        private void LoadParameters()
        {
            Parameters = new List<ActionParameter>();
            foreach (ParameterSettings parameterSettings in ActionSettings.ParameterList)
            {
                ActionParameter parameter = new ActionParameter(Device, this, parameterSettings);
                Parameters.Add(parameter);

                // do not bind references to special values
                if (!parameter.IsSpecialValue)
                    parameter.Register = Device.FindRegister("", DeviceBlock.Name, parameter.Name);
            }
        }

        internal abstract bool ExecuteInternal(int depth);

        protected bool ExecuteActions(int depth)
        {
            int newDepth = depth + 1;
            foreach (AlgorithmAction action in Actions)
                if (!action.ExecuteInternal(newDepth))
                {
                    if (action.ExitOnFailure)
                        return true;
                    if (!action.ContinueOnFailure)
                        return false;
                }
                else if (action.ExitOnSuccess)
                    return true;
            return true;
        }

        public DeviceBlock FindBlock()
        {
            // return local block if present
            if (DeviceBlock != null)
                return DeviceBlock;

            string blockName = ActionSettings.BlockName;
            if (blockName != "")
            {
                DeviceBlock = Device.FindBlock(blockName);
                if (DeviceBlock == null)
                    throw new Exception("AlgorithmAction - Cannot find target block: '" + blockName + "'");
            }
            else if (ParentAction != null) // climb the parent tree
                DeviceBlock = ParentAction.FindBlock();
            else
                DeviceBlock = null;

            return DeviceBlock;
        }

        protected void ConfigureParameters(bool isGetBlock = false)
        {
            if (isGetBlock && DeviceBlock != null)
            {
                foreach (Register item in DeviceBlock.BlockAllRegisters)
                {
                    item.ClearSetValueDelegate();
                }
            }

            foreach (ActionParameter param in Parameters)
            {
                param.SetParameterValue(isGetBlock);
            }
        }
    }

    public class Algorithm : AlgorithmAction
    {
        public Algorithm(DeviceAlgorithm device, ActionSettings actionSettings)
            : base(device, null, actionSettings)
        {
        }

        public virtual bool Execute()
        {
            return ExecuteInternal(0);
        }

        internal override bool ExecuteInternal(int depth)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Algorithm Starting - Depth: " + depth + " - Name: " + Name + " - Type: " + Type, LogEntryType.Trace);
            bool res = ExecuteActions(depth);
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Algorithm Complete - Depth: " + depth + " - Name: " + Name + " - Result: " + (res ? "true" : "false"), LogEntryType.Trace);
            return res;
        }
    }

    public class AlgorithmAction_SendBlock : AlgorithmAction
    {
        public AlgorithmAction_SendBlock(DeviceAlgorithm device, Algorithm algorithm, ActionSettings actionSettings)
            : base(device, algorithm, actionSettings)
        {
            CommonConstruction();
        }

        public AlgorithmAction_SendBlock(DeviceAlgorithm device, AlgorithmAction action, ActionSettings actionSettings)
            : base(device, action, actionSettings)
        {
            CommonConstruction();
        }

        private void CommonConstruction()
        {
            if (DeviceBlock == null)
                throw new Exception("AlgorithmAction_SendBlock - Cannot resolve target block");
        }

        internal override bool ExecuteInternal(int depth)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action SendBlock - Starting - Depth: " + depth + " - Name: " + Name + " - Type: " + Type, LogEntryType.Trace);
            bool res = ExecuteActions(depth);
            if (res)
            {
                ConfigureParameters();
                DeviceBlock.SendBlock(ContinueOnFailure);
            }
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action SendBlock - Complete - Depth: " + depth + " - Name: " + Name + " - Result: " + (res ? "true" : "false"), LogEntryType.Trace);
            return res;
        }
    }

    public class AlgorithmAction_GetBlock : AlgorithmAction
    {
        public AlgorithmAction_GetBlock(DeviceAlgorithm device, Algorithm algorithm, ActionSettings actionSettings)
            : base(device, algorithm, actionSettings)
        {
            CommonConstruction();
        }

        public AlgorithmAction_GetBlock(DeviceAlgorithm device, AlgorithmAction action, ActionSettings actionSettings)
            : base(device, action, actionSettings)
        {
            CommonConstruction();
        }

        private void CommonConstruction()
        {
            if (DeviceBlock == null)
                throw new Exception("AlgorithmAction_GetBlock - Cannot resolve target block");
        }

        internal override bool ExecuteInternal(int depth)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action GetBlock - Starting - Depth: " + depth + " - Name: " + Name + " - Type: " + Type, LogEntryType.Trace);
            ConfigureParameters(true);
            bool res = DeviceBlock.GetBlock(ContinueOnFailure, true);
            if (res)
                res = ExecuteActions(depth);
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action GetBlock - Complete - Depth: " + depth + " - Name: " + Name + " - Result: " + (res ? "true" : "false"), LogEntryType.Trace);
            return res;
        }
    }

    public class AlgorithmAction_LogError : AlgorithmAction
    {
        public AlgorithmAction_LogError(DeviceAlgorithm device, Algorithm algorithm, ActionSettings actionSettings)
            : base(device, algorithm, actionSettings)
        {
            CommonConstruction();
        }

        public AlgorithmAction_LogError(DeviceAlgorithm device, AlgorithmAction action, ActionSettings actionSettings)
            : base(device, action, actionSettings)
        {
            CommonConstruction();
        }

        private void CommonConstruction()
        {
            if (DeviceBlock == null)
                throw new Exception("AlgorithmAction_LogError - Cannot resolve target block");
        }

        internal override bool ExecuteInternal(int depth)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action LogError - Starting - Depth: " + depth + " - Name: " + Name + " - Type: " + Type, LogEntryType.Trace);
            ConfigureParameters();
            bool res = ExecuteActions(depth);

            if (res)
            {
                ErrorLogger logger = Device.Params.ErrorLogger;
                DateTime now = DateTime.Now;
                foreach (Register item in DeviceBlock.BlockAllRegisters)
                {
                    if (!item.IsErrorDetail)
                        continue;
                    if (item.GetType() == typeof(RegisterBytes))
                        logger.LogError(((int)Device.Address).ToString(), now, item.Settings.Name, 2, ((RegisterBytes)item).ValueBytes);
                    else if (item.GetType() == typeof(RegisterNumber))
                        logger.LogError(((int)Device.Address).ToString(), now, item.Settings.Name + ":(" + ((RegisterNumber)item).ValueDecimal.ToString() + ")");
                    else if (item.GetType() == typeof(RegisterString))
                        logger.LogError(((int)Device.Address).ToString(), now, item.Settings.Name + ":(" + ((RegisterString)item).ValueString + ")");
                }
            }

            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action LogError - Complete - Depth: " + depth + " - Name: " + Name + " - Result: " + (res ? "true" : "false"), LogEntryType.Trace);

            return res;
        }
    }

    public class AlgorithmAction_RepeatCountTimes : AlgorithmAction
    {
        private RegisterNumber CountMapItem;
        private int Count = 0;

        public AlgorithmAction_RepeatCountTimes(DeviceAlgorithm device, Algorithm algorithm, ActionSettings actionSettings)
            : base(device, algorithm, actionSettings)
        {
            CommonConstruction();
        }

        public AlgorithmAction_RepeatCountTimes(DeviceAlgorithm device, AlgorithmAction action, ActionSettings actionSettings)
            : base(device, action, actionSettings)
        {
            CommonConstruction();
        }

        private void CommonConstruction()
        {
            String countName = ActionSettings.Count;
            if (countName == "")
                throw new Exception("AlgorithmAction_RepeatCountTimes - Count not specified");
            Register countMapItem = Device.FindRegister("", "", countName);
            if (countMapItem == null)
                throw new Exception("AlgorithmAction_RepeatCountTimes - Cannot find DeviceMapItem: " + countName);
            if (countMapItem.GetType() != typeof(RegisterNumber))
                throw new Exception("AlgorithmAction_RepeatCountTimes - Count is not numeric: " + countName);
            CountMapItem = (RegisterNumber)countMapItem;
        }

        internal override bool ExecuteInternal(int depth)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action RepeatCountTimes - Starting - Depth: " + depth + " - Name: " + Name + " - Type: " + Type, LogEntryType.Trace);
            ConfigureParameters();
            Count = (int)CountMapItem.ValueDecimal;
            bool res = true;
            for (int i = 0; i < Count; i++)
                if (!ExecuteActions(depth))
                {
                    if (!ContinueOnFailure)
                    {
                        res = false;
                        break;
                    }
                }
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action RepeatCountTimes - Complete - Depth: " + depth + " - Name: " + Name + " - Result: " + (res ? "true" : "false"), LogEntryType.Trace);
            return res;
        }
    }

    public class AlgorithmAction_Repeat : AlgorithmAction
    {
        public AlgorithmAction_Repeat(DeviceAlgorithm device, Algorithm algorithm, ActionSettings actionSettings)
            : base(device, algorithm, actionSettings)
        {
            CommonConstruction();
        }

        public AlgorithmAction_Repeat(DeviceAlgorithm device, AlgorithmAction action, ActionSettings actionSettings)
            : base(device, action, actionSettings)
        {
            CommonConstruction();
        }

        private void CommonConstruction()
        {
            bool found = false;
            foreach (AlgorithmAction action in Actions)
            {
                if (action.ExitOnSuccess || action.ExitOnFailure)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new Exception("AlgorithmAction_Repeat - Cannot find exit condition");
        }

        internal override bool ExecuteInternal(int depth)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action Repeat - Starting - Depth: " + depth + " - Name: " + Name + " - Type: " + Type, LogEntryType.Trace);
            ConfigureParameters();

            bool contin = true;
            bool retVal = true;
            int count = 0;
            int newDepth = depth + 1;
            while (contin)
            {
                foreach (AlgorithmAction action in Actions)
                    if (!action.ExecuteInternal(newDepth))
                    {
                        if (action.ExitOnFailure)
                        {
                            contin = false;
                            break;
                        }
                        if (!action.ContinueOnFailure)
                        {
                            contin = false;
                            retVal = false;
                            break;
                        }
                    }
                    else if (action.ExitOnSuccess)
                    {
                        contin = false;
                        break;
                    }
                if (contin)
                    count++;
            }

            foreach (ActionParameter param in Parameters)
            {
                if (param.Name == "!RepeatCount" && param.ContentVariable != null)
                {
                    ((VariableEntry_Numeric)param.ContentVariable).SetValueDelegate(count);
                }
            }

            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Action RepeatCountTimes - Complete - Depth: " + depth + " - Name: " + Name + " - Result: " + (retVal ? "true" : "false"), LogEntryType.Trace);

            return retVal;
        }
    }
}
