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
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace PVSettings
{
    public class DynamicDataMap
    {
        public class MapItem
        {
            public UInt16? Position;
            public RegisterSettings templateRegisterSettings;
        }

        public MapItem[] Items { get; private set; }

        public List<MapItem> RawItems { get; private set; }

        public DynamicDataMap(ObservableCollection<RegisterSettings> registers)
        {
            bool positionsFound = false;

            Items = null;
            RawItems = new List<MapItem>();

            foreach (RegisterSettings reg in registers)
            {                
                MapItem item = new MapItem();

                item.templateRegisterSettings = reg;
                item.Position = reg.Position;
                positionsFound |= item.Position.HasValue;
                RawItems.Add(item);                
            }
            if (positionsFound)
                BuildItemArray();
        }

        public void BuildItemArray()
        {
            int size = 0;

            foreach (MapItem item in RawItems)
                if (item.Position.HasValue) size++;

            Items = new MapItem[size];

            int pos = 0;
            foreach (MapItem item in RawItems)
                if (item.Position.HasValue)
                    Items[pos++] = item;
        }

        public bool SetItemPosition(Int16 id, UInt16 position)
        {
            foreach (MapItem item in RawItems)
                if (item.templateRegisterSettings.Id1 == id)
                {
                    item.Position = position;
                    return true;
                }

            foreach (MapItem item in RawItems)
                if (item.templateRegisterSettings.Id3 == id)
                {
                    item.Position = position;
                    return true;
                }

            return false;
        }
    }
}
