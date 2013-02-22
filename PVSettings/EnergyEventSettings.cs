using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml;

namespace PVSettings
{
    public enum HierarchyType
    {
        Yield,
        Consumption,
        Meter,
        Undefined
    }

    public enum EmitEventType
    {
        Yield,
        Consumption
    }

    public struct EnergyEventKey
    {
        public HierarchyType Hierarchy;
        public String ManagerName;
        public String Component;
        public String DeviceName;
    }

    public class EnergyEventSettings : SettingsBase
    {
        ApplicationSettings ApplicationSettings;

        public EnergyEventSettings(ApplicationSettings root, XmlElement element)
            : base(root, element)
        {
            ApplicationSettings = (ApplicationSettings)root;
        }

        public void RemoveOldNodes()
        {
            DeleteElement("basetype");            
        }

        public HierarchyType Hierarchy
        {
            get
            {
                String val = GetValue("hierarchy");
                if (val == HierarchyType.Yield.ToString())
                    return HierarchyType.Yield;
                if (val == HierarchyType.Consumption.ToString())
                    return HierarchyType.Consumption;
                if (val == HierarchyType.Meter.ToString())
                    return HierarchyType.Meter;

                return HierarchyType.Undefined;
            }

            set
            {
                if (value == HierarchyType.Yield)
                    EventType = "Yield";
                else if (value == HierarchyType.Consumption)
                    EventType = "Consumption";
                SetValue("hierarchy", value.ToString(), "Hierarchy", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public String EventType
        {
            get
            {
                return GetValue("eventtype");
            }

            set
            {
                if (Hierarchy == HierarchyType.Yield || FeedInYield)
                    SetValue("eventtype", "Yield", "EventType", ApplicationSettings.LoadingEnergyEvents);
                else if (Hierarchy == HierarchyType.Consumption || FeedInConsumption)
                    SetValue("eventtype", "Consumption", "EventType", ApplicationSettings.LoadingEnergyEvents);
                else
                    SetValue("eventtype", value, "EventType", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public String ManagerName
        {
            get
            {
                return GetValue("managername");
            }

            set
            {
                SetValue("managername", value, "ManagerName", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public String Component
        {
            get
            {
                return GetValue("component");
            }

            set
            {
                SetValue("component", value, "Component", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public String DeviceName
        {
            get
            {
                return GetValue("devicename");
            }

            set
            {
                SetValue("devicename", value, "DeviceName", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public String Inverter
        {
            get
            {
                return GetValue("inverter");
            }

            set
            {
                SetValue("inverter", value, "Inverter", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public String ConsumeSystem
        {
            get
            {
                return GetValue("consumesystem");
            }

            set
            {
                SetValue("consumesystem", value, "ConsumeSystem", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public String DefaultDescription
        {
            get
            { 
                if (DeviceName != "")
                    return Hierarchy.ToString() + ":" + ManagerName + ":" + Component + ":" + DeviceName;
                if (Component != "")
                    return Hierarchy.ToString() + ":" + ManagerName + ":" + Component;
                if (ManagerName != "")
                    return Hierarchy.ToString() + ":" + ManagerName;
                return Hierarchy.ToString();
            }
        }


        public String Description
        {
            get
            {
                if (ApplicationSettings.UseDefaultEvents && FeedInYield)
                    return "Inverter Yield";
                String val = GetValue("description");
                if (val == "")
                    return DefaultDescription;
                else
                    return val;
            }

            set
            {
                if (ApplicationSettings.UseDefaultEvents && FeedInYield)
                {
                    OnPropertyChanged(new PropertyChangedEventArgs("Description"));
                    return;
                }
                if (value == DefaultDescription)
                    SetValue("description", "", "Description", ApplicationSettings.LoadingEnergyEvents);
                else
                    SetValue("description", value, "Description", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public int? Interval
        {
            get
            {
                String val = GetValue("interval");
                if (val == "" || val == null)
                    return null;
                try
                {
                    return Convert.ToInt32(val);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            set
            {
                if (value == null)
                    SetValue("interval", "", "Interval", ApplicationSettings.LoadingEnergyEvents);
                else
                    SetValue("interval", value.ToString(), "Interval", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public bool EmitEvent
        {
            get
            {
                String val = GetValue("emitevent");
                return (val == "true");
            }

            set
            {
                if (value)
                    SetValue("emitevent", "true", "EmitEvent", ApplicationSettings.LoadingEnergyEvents);
                else
                    SetValue("emitevent", "false", "EmitEvent", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public bool FeedInYield
        {
            get
            {
                String val = GetValue("feedinyield");
                return (val == "true");
            }

            set
            {
                if (Hierarchy == HierarchyType.Consumption)
                    SetValue("feedinyield", "false", "FeedInYield");
                else if (value)
                {
                    ApplicationSettings.ClearYieldFeedin();
                    SetValue("feedinyield", "true", "FeedInYield", ApplicationSettings.LoadingEnergyEvents);
                    SetValue("feedinconsumption", "false", "FeedInConsumption", ApplicationSettings.LoadingEnergyEvents);
                    SetValue("eventtype", "Yield", "EventType", ApplicationSettings.LoadingEnergyEvents);
                    if (ApplicationSettings.UseDefaultEvents)
                        OnPropertyChanged(new PropertyChangedEventArgs("Description"));
                }
                else
                    SetValue("feedinyield", "false", "FeedInYield", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public bool FeedInConsumption
        {
            get
            {
                String val = GetValue("feedinconsumption");
                return (val == "true");
            }

            set
            {
                if (Hierarchy == HierarchyType.Yield)
                    SetValue("feedinconsumption", "false", "FeedInConsumption", ApplicationSettings.LoadingEnergyEvents);
                else if (value)
                {
                    ApplicationSettings.ClearConsumptionFeedin();
                    SetValue("feedinconsumption", "true", "FeedInConsumption", ApplicationSettings.LoadingEnergyEvents);
                    SetValue("feedinyield", "false", "FeedInYield", ApplicationSettings.LoadingEnergyEvents);
                    SetValue("eventtype", "Consumption", "EventType", ApplicationSettings.LoadingEnergyEvents);
                }
                else
                    SetValue("feedinconsumption", "false", "FeedInConsumption", ApplicationSettings.LoadingEnergyEvents);
            }
        }

        public bool IsCurrentEvent { get; set; }
    }
}
