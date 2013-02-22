/*
* Copyright (c) 2013 Dennis Mackay-Fisher
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MackayFisher.Utilities
{
    public interface INamedItem
    {
        String UniqueName { get; }
    }

    public class UniqueNameResolver<TNamedItem> where TNamedItem : class, INamedItem 
    {
        public static bool IsNameUnique(string candidateName, IEnumerator<TNamedItem> nameList, TNamedItem excludeItem = null)
        {
            nameList.Reset();
            while (nameList.MoveNext())
            {
                TNamedItem current = nameList.Current;
                if (current != excludeItem && current.UniqueName == candidateName)
                    return false;
            }
            return true;
        }

        private static int? GetCandidateSuffix(String candidate, out String trimmedCandidate)
        {
            String suffix = "";
            String trimmed = candidate;
            int pos = candidate.Length - 1;  // underscore cannot be last char
            while (pos-- > 0) // pos is possible position of underscore
            {
                if (candidate[pos] == '_')
                {
                    int suffixLength = candidate.Length - (pos + 1);
                    trimmed = candidate.Substring(0, candidate.Length - suffixLength);
                    suffix = candidate.Substring(pos+1, suffixLength-1); // exclude leading  _ from suffix
                    break;
                }
            }

            int suffixInt;
            if (int.TryParse(suffix, out suffixInt))  // suffix is valid if it is an integer
            {
                trimmedCandidate = trimmed;
                return suffixInt;
            }

            // no valid suffuix found
            trimmedCandidate = candidate;
            return null;
        }

        public static String ResolveUniqueName(string candidateName, IEnumerator<TNamedItem> nameList, TNamedItem excludeItem = null)
        {
            String tryThis = candidateName;
            String trimmedCandidate = candidateName;
            bool suffixSearched = false;
            int? suffix = null;
            while (true)
            {
                if (IsNameUnique(tryThis, nameList, excludeItem)) // eventually a unique candidate will be found (i hope)
                    return tryThis;
                if (!suffixSearched)
                {
                    suffix = GetCandidateSuffix(tryThis, out trimmedCandidate);
                    suffixSearched = true;
                }
                if (!suffix.HasValue)
                    suffix = 1;
                else
                    suffix++;

                tryThis = trimmedCandidate + "_" + suffix.Value.ToString("00") ;
            }           
        }
    }
}
