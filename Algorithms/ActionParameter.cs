/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;

namespace Algorithms
{
    public class ActionParameter
    {
        private DeviceAlgorithm Device;
        private AlgorithmAction AlgorithmAction;
        private ParameterSettings ParameterSettings;

        public String Name { get; private set; }
        public String ParameterValue { get; private set; }
        public String Content { get; private set; }
        public bool IsSpecialValue { get; private set; }

        private Register _Register = null;
        public VariableEntry ContentVariable { get; private set; }

        public Register Register
        {
            get { return _Register; }
            set
            {
                _Register = value;
                if (value == null)
                    return;
            }
        }

        public ActionParameter(DeviceAlgorithm device, AlgorithmAction algorithmAction, ParameterSettings parameterSettings)
        {
            Device = device;
            AlgorithmAction = algorithmAction;
            ParameterSettings = parameterSettings;
            Name = ParameterSettings.Name;
            IsSpecialValue = Name.StartsWith("!");
            Content = ParameterSettings.Content;
            if (Content != "")
                ContentVariable = Device.FindVariable(Content);
            else
                ContentVariable = null;
            ParameterValue = ParameterSettings.ParameterValue;
        }

        public void SetParameterValue(bool isGetBlock)
        {
            if (Register == null)
                return;

            if (ParameterValue != null)
                Register.ValueString = ParameterValue;

            if (isGetBlock && _Register != null && ContentVariable != null)
            {
                if (_Register.GetType() == typeof(RegisterNumber))
                {
                    ((RegisterNumber)_Register).SetSetValueDelegate(((VariableEntry_Numeric)ContentVariable).SetValueDelegate);
                }
            }

        }
    }
}
