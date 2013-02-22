using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Algorithms
{
    public abstract class VariableEntry
    {
        public String Name { get; private set; }

        public VariableEntry(String name)
        {
            Name = name;
        }
    }

    public class VariableEntry_Numeric : VariableEntry
    {
        public VariableEntry_Numeric(String name, SetNumberValueDelegate setValueDelegate, GetNumberValueDelegate getValueDelegate = null)
            : base(name)
        {
            SetValueDelegate = setValueDelegate;
            GetValueDelegate = getValueDelegate;
        }

        public SetNumberValueDelegate SetValueDelegate;
        public GetNumberValueDelegate GetValueDelegate;
    }

    public class VariableEntry_String : VariableEntry
    {
        public VariableEntry_String(String name, SetStringValueDelegate setValueDelegate, GetStringValueDelegate getValueDelegate = null)
            : base(name)
        {
            SetValueDelegate = setValueDelegate;
            GetValueDelegate = getValueDelegate;
        }

        public SetStringValueDelegate SetValueDelegate;
        public GetStringValueDelegate GetValueDelegate;
    }

    public class VariableEntry_Bytes : VariableEntry
    {
        public VariableEntry_Bytes(String name, SetBytesValueDelegate setValueDelegate, GetBytesValueDelegate getValueDelegate = null)
            : base(name)
        {
            SetValueDelegate = setValueDelegate;
            GetValueDelegate = getValueDelegate;
        }

        public SetBytesValueDelegate SetValueDelegate;
        public GetBytesValueDelegate GetValueDelegate;
    }
}
