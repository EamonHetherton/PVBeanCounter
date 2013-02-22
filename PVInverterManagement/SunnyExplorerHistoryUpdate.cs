using System;
using GenericConnector;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MackayFisher.Utilities;
using DeviceDataRecorders;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class SunnyExplorerHistoryUpdate : DeviceDataRecorders.HistoryUpdate
    {

        public SunnyExplorerHistoryUpdate(IDataRecorder dataRecorder, IEvents energyEvents)
            : base(dataRecorder, energyEvents)
        {            
        }

        private void CheckReadingSet(EnergyReadingSet readingSet)
        {
            /*
            * This detects a specific dataset problem found in some SMA sourced datasets where there is a single missing reading
            * followed by a catchup reading with a high power level. pvoutput can reject this catchup as having too high a power level
            * This code will detect the scenario and replace the single missing reading
            */
            int prevDuration = 0;

            for (int i = 0; i < readingSet.Readings.Count; i++)
            {
                EnergyReading reading = readingSet.Readings[i];

                if (i > 0)
                {
                    int duration = reading.Seconds;

                    // only act on jumps from 0 to > 1.5KW 
                    // this should prevent removal of zero values caused by dark cloud - a natural jump from 0 to 1.5KW would be very unusual
                    // only act on single missing values where duration is double expected
                    if (i > 1 && duration == (prevDuration * 2) && reading.TotalPower > 1500)
                    {
                        if (reading.Power.HasValue)
                            reading.Power = reading.Power / 2;
                        
                        reading.Seconds = prevDuration;

                        EnergyReading extraReading = new EnergyReading(reading.DeviceId, reading.OutputTime.AddSeconds(-prevDuration), reading.Feature, prevDuration, false);
                        
                        extraReading.Power = reading.Power;
                        
                        readingSet.Readings.Insert(i, extraReading);
                        i++;
                    }
                }
                prevDuration = reading.Seconds;
            }
        }

        private Boolean UpdateHistory(String fileName, GenConnection connection)
        {
            SunnyExplorerCSVParser csvParser = new SunnyExplorerCSVParser();

            List<EnergyReadingSet> readings;

            Boolean res = csvParser.ExtractRecords(connection, DataRecorder.DeviceManager, fileName, out readings);

            if (res)
            {
                foreach (EnergyReadingSet readingSet in readings)
                {
                    CheckReadingSet(readingSet);
                    UpdateReadingSet(readingSet, connection);
                }
                return true;
            }
            return false;
        }

        public override int UpdateFromDirectory(String inDirectory, String filePattern, String moveToDirectory, GenConnection connection)
        {
            int res = 0;
            DirectoryInfo directoryInInfo = new DirectoryInfo(inDirectory);

            if (!directoryInInfo.Exists)
                throw new PVException(PVExceptionType.DirectoryMissing, "UpdateFromDirectory - Directory: " + inDirectory + " :does not exist");

            DirectoryInfo directoryMoveToInfo = new DirectoryInfo(moveToDirectory);
            if (!directoryMoveToInfo.Exists)
                try
                {
                    directoryMoveToInfo.Create();
                }
                catch (Exception e)
                {
                    throw new PVException(PVExceptionType.CannotCreateDirectory,
                        "UpdateFromDirectory - Error creating directory: " + moveToDirectory + " - Exception: " + e.Message, e);
                }

            foreach (FileInfo fileInfo in directoryInInfo.EnumerateFiles(filePattern))
            {
                bool updated = false;

                updated = UpdateHistory(fileInfo.FullName, connection);

                if (updated)
                {
                    res++;
                    try
                    {
                        String newName = System.IO.Path.Combine(directoryMoveToInfo.FullName, fileInfo.Name);
                        FileInfo moveToFileInfo = new FileInfo(newName);
                        if (moveToFileInfo.Exists)
                            moveToFileInfo.Delete();
                        moveToFileInfo = null;

                        fileInfo.MoveTo(newName);
                    }
                    catch (Exception e)
                    {
                        throw new PVException(PVExceptionType.CannotMoveFile,
                            "UpdateFromDirectory - Error moving file: " + fileInfo.FullName + " :to: " + moveToDirectory + " - Exception: " + e.Message, e);
                    }
                }
            }

            return res;
        }

    }
}
