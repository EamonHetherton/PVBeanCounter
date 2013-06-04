using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenericConnector
{
    public struct DBDateTimeGeneric
    {
        private  static GenDBType DefaultDBType = GenDBType.SQLite;
        private static Int64 DefaultTickResolution = TimeSpan.TicksPerMillisecond;

        public static void SetDefaultDBType(GenDBType dbType, int? tickResolution = null)
        {
            DefaultDBType = dbType;
            if (tickResolution.HasValue)
                DefaultTickResolution = tickResolution.Value;
            else
                switch (dbType)
                {
                    case GenDBType.MySql:
                        DefaultTickResolution = TimeSpan.TicksPerMillisecond;
                        break;
                    case GenDBType.SQLite:
                        DefaultTickResolution = TimeSpan.TicksPerMillisecond;
                        break;
                    case GenDBType.SQLServer:
                        DefaultTickResolution = TimeSpan.TicksPerMillisecond;
                        break;
                    default:
                        DefaultTickResolution = TimeSpan.TicksPerMillisecond;
                        break;
                }
        }

        private DateTime ValueInternal;

        public DateTime Value
        {
            get
            {
                return ValueInternal;
            }

            set
            {
                if (DefaultTickResolution == 1)
                {
                    ValueInternal = value;
                    return;
                }
                DateTime date = value.Date;
                long todTicks = value.TimeOfDay.Ticks / DefaultTickResolution;
                todTicks *= DefaultTickResolution;
                ValueInternal = date.AddTicks(todTicks);
            }
        }

        public override string ToString()
        {
            return ValueInternal.ToString();
        }
    }
}
