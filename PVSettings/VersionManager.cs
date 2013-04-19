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
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GenericConnector;
using System.Data.Common;
using MackayFisher.Utilities;

namespace PVSettings
{
    // DDL is the abstract class representing DDL commands used to bring a database from an earlier version to a later version
    // Must be implemented for each target database type as the DDL is similar but not generic
    internal abstract class DDL
    {
        public abstract String Table_version { get; }
        public virtual String CurrentTable_version { get { return Table_version; } }

        public abstract String Table_pvoutputlog { get; } // original tab;e
        public abstract String Table_pvoutputlog_2000 { get; }  // version  2000
        public virtual String CurrentTable_pvoutputlog { get { return Table_pvoutputlog_2000; } }

        public virtual String View_pvoutput_v
        { get { return ""; } }

        public virtual String View_pvoutput5min_v
        { get { return ""; } }

        public virtual String View_pvoutput_sub_v
        { get { return ""; } }

        public virtual String Alter_pvoutputlog_1400a
        { get { return ""; } }

        public virtual String Alter_pvoutputlog_1400b
        { get { return ""; } }

        public abstract string Alter_pvoutputlog_1421
        { get; }

        public abstract string Alter_inverter_1700
        { get; }

        public virtual String Table_meter
        { get { return ""; } }

        public virtual String Table_meterreading
        { get { return ""; } }

        public virtual String Alter_meterreading_1400
        { get { return ""; } }

        public virtual String Alter_meterreading_1401a
        { get { return ""; } }

        public virtual String Alter_meterreading_1401b
        { get { return ""; } }

        public virtual String Table_meterhistory
        { get { return ""; } }

        public abstract String Table_cmsdata_1500 { get; }
       
        public abstract String Alter_outputhistory_1500 { get; }

        public abstract String View_pvoutput_sub_v_1500 { get; }

        public abstract String View_pvoutput5min_v_1500 { get; }

        public abstract String View_pvoutput_v_1500 { get; }

        public abstract String View_pvoutput_sub_v_1700 { get; }

        public abstract String View_pvoutput5min_v_1700 { get; }

        public abstract String View_pvoutput_v_1700 { get; }

        public virtual String Alter_meterreading_dropPK_1700 
        {
            get
            {
                throw new NotImplementedException("Not available for this DB type");
            }
        }

        public virtual String Alter_meterreading_createPK_1700
        {
            get
            {
                throw new NotImplementedException("Not available for this DB type");
            }
        }

        public abstract String Alter_meter_1710 { get; }

        public abstract String Alter_cmsdata_1830 { get; }

        public abstract String View_pvoutput_sub_v_1836 { get; }

        public abstract String View_pvoutput5min_v_1836 { get; }

        public abstract String View_pvoutput_v_1836 { get; }

        public abstract string Alter_cmsdata_1836 { get; }

        public abstract string Alter_outputhistory_1836 { get; }

        public abstract string Drop_inverter_index_1902 { get; }

        public abstract string Create_inverter_index_1902 { get; }



        public abstract string Table_devicetype_2000 { get; }
        public virtual String CurrentTable_devicetype { get { return Table_devicetype_2000; } }

        public abstract string Table_devicefeature_2000 { get; }
        public virtual String CurrentTable_devicefeature { get { return Table_devicefeature_2000; } }

        public abstract string Table_device_2000 { get; }
        public virtual String CurrentTable_device { get { return Table_device_2000; } }

        public abstract string Table_devicereading_energy_2000 { get; }
        public virtual String CurrentTable_devicereading_energy { get { return Table_devicereading_energy_2000; } }

        public abstract String View_devicedayoutput_v_2000 { get; }
        public virtual String CurrentView_devicedayoutput_v { get { return View_devicedayoutput_v_2000; } }

    }

    internal class MySql_DDL : DDL
    {
        // required for 1.3.0.0 and later
        public override String Table_version
        {
            get
            {
                return
                    "create table `version` " +
                    "( " +
                        "`Major` varchar(4) NOT NULL, " +
                        "`Minor` varchar(4) NOT NULL, " +
                        "`Release` varchar(4) NOT NULL, " +
                        "`Patch` varchar(4) NOT NULL " +
                     ") ENGINE=InnoDB DEFAULT CHARSET=latin1";
            }
        }

        // required for 1.3.0.0 and later
        public override string Table_pvoutputlog
        {
            get
            {
                return
                    "create table `pvoutputlog` " +
                    "( " +
                        "`SiteId` varchar(10) NOT NULL, " +
                        "`OutputDay` date NOT NULL, " +
                        "`OutputTime` mediumint(9) NOT NULL, " +
                        "`Loaded` tinyint(1) NOT NULL DEFAULT '0', " +
                        "`ImportEnergy` double NULL, " +
                        "`ImportPower` double NULL, " +
                        "PRIMARY KEY (`SiteId`,`OutputDay`,`OutputTime`) " +
                     ") ENGINE=InnoDB DEFAULT CHARSET=latin1";
            }
        }

        // required for 1.3.0.0 and later
        public override string View_pvoutput_v
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput_v` AS " +
                    "select OutputDay, (OutputTime DIV 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower " +
                    "from pvoutput_sub_v " +
                    "group by OutputDay, (OutputTime DIV 600) * 600";
            }
        }

        // required for 1.3.3.0 and later
        public override string View_pvoutput5min_v
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput5min_v` AS " +
                    "select OutputDay, (OutputTime DIV 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower " +
                    "from pvoutput_sub_v " +
                    "group by OutputDay, (OutputTime DIV 300) * 300";
            }
        }

        public override string View_pvoutput_sub_v
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput_sub_v` AS " +
                    "select DATE(oh.OutputTime) as OutputDay, TIME_TO_SEC(TIME(oh.OutputTime)) as OutputTime, " +
                    "sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) as Power " +
                    "from outputhistory as oh " +
                    "where oh.OutputKwh > 0 " +
                    "and oh.OutputTime >= (curdate() - 20) " +
                    "group by DATE(oh.OutputTime), TIME_TO_SEC(TIME(oh.OutputTime))";
            }
        }

        // required for 1.4.0.0 and later
        public override string Alter_pvoutputlog_1400a
        {
            get
            {
                return
                    "alter table `pvoutputlog` " +
                    "add `ImportEnergy` double NULL, " +
                    "add `ImportPower` double NULL ";
            }
        }

        public override string Alter_pvoutputlog_1400b
        {
            get
            {
                return
                    "alter table `pvoutputlog` " +
                    "modify `Energy` double NULL, " +
                    "modify `Power` double NULL ";
            }
        }

        // required for 1.4.2.1 and later
        public override string Alter_pvoutputlog_1421
        {
            get
            {
                return
                    "alter table `pvoutputlog` " +
                    "add `Temperature` double NULL ";
            }
        }

        public override string Table_meter
        {
            get
            {
                return
                    "create table if not exists `meter` " +
                    "( " +
                        "`Id` TINYINT NOT NULL AUTO_INCREMENT , " +
                        "`MeterName` varchar(45) NOT NULL, " +
                        "`MeterType` varchar(45) NOT NULL, " +
                        "`StandardDuration` MEDIUMINT NULL , " +
                        "`CondensedDuration` MEDIUMINT NULL , " +
                        "`CondenseAge` MEDIUMINT NULL , " +
                        "`Enabled` TINYINT(1)  NULL , " +
                        "PRIMARY KEY (`Id`) , " +
                        "UNIQUE INDEX `MeterName_UNIQUE` (`MeterName` ASC) " +
                     ") ENGINE=InnoDB DEFAULT CHARSET=latin1";
            }
        }

        public override string Table_meterreading
        {
            get
            {
                return
                    "create table if not exists `meterreading` " +
                    "( " +
                        "`Meter_Id` TINYINT NOT NULL , " +
                        "`Appliance` TINYINT NOT NULL , " +
                        "`ReadingTime` DATETIME NOT NULL , " +
                        "`Duration` MEDIUMINT NULL , " +
                        "`Energy` DOUBLE NULL , " +
                        "`Temperature` FLOAT NULL , " +
                        "`Calculated` DOUBLE NULL , " +
                        "`MinPower` MEDIUMINT NULL , " +
                        "`MaxPower` MEDIUMINT NULL , " +
                        "PRIMARY KEY (`Meter_Id`, `Appliance`, `ReadingTime`) , " +
                        "INDEX `fk_MeterReading_Meter` (`Meter_Id` ASC) , " +
                        "CONSTRAINT `fk_MeterReading_Meter` " +
                            "FOREIGN KEY (`Meter_Id` ) " +
                            "REFERENCES `pvhistory`.`Meter` (`Id` ) " +
                            "ON DELETE NO ACTION " +
                            "ON UPDATE NO ACTION " +
                     ") ENGINE=InnoDB DEFAULT CHARSET=latin1";
            }
        }

        public override string Alter_meterreading_1400
        {
            get
            {
                return
                    "alter table `meterreading` " +
                    "modify `Energy` DOUBLE NULL ";
            }
        }

        public override string Alter_meterreading_1401a
        {
            get
            {
                return
                    "alter table `meterreading` " +
                    "drop column `ReadingType` ";
            }
        }

        public override string Alter_meterreading_1401b
        {
            get
            {
                return
                    "alter table `meterreading` " +
                    "drop column `ReadingNo` ";
            }
        }

        public override string Table_meterhistory
        {
            get
            {
                return
                    "create table if not exists `meterhistory` " +
                    "( " +
                        "`Meter_Id` TINYINT NOT NULL , " +
                        "`Appliance` TINYINT NOT NULL , " +
                        "`ReadingTime` DATETIME NOT NULL , " +
                        "`HistoryType` CHAR NOT NULL , " +
                        "`Duration` MEDIUMINT NOT NULL , " +
                        "`Energy` DOUBLE NOT NULL , " +
                        "PRIMARY KEY (`Meter_Id`, `Appliance`, `ReadingTime`, `HistoryType`) , " +
                        "INDEX `fk_MeterHistory_Meter` (`Meter_Id` ASC) , " +
                        "CONSTRAINT `fk_MeterHistory_Meter` " +
                            "FOREIGN KEY (`Meter_Id` ) " +
                            "REFERENCES `pvhistory`.`Meter` (`Id` ) " +
                            "ON DELETE NO ACTION " +
                            "ON UPDATE NO ACTION " +
                     ") ENGINE=InnoDB DEFAULT CHARSET=latin1";
            }
        }

        public override String Table_cmsdata_1500
        {
            get
            {
                return
                    "CREATE TABLE cmsdata " +
                    "( " +
                        "Inverter_Id MEDIUMINT NOT NULL, " +
                        "OutputTime DATETIME NOT NULL, " +
                        "EnergyTotal DOUBLE NOT NULL, " +
                        "EnergyToday DOUBLE NULL, " +
                        "Temperature DOUBLE NULL, " +
                        "VoltsPV DOUBLE NULL, " +
                        "CurrentAC DOUBLE NULL, " +
                        "VoltsAC DOUBLE NULL, " +
                        "FrequencyAC DOUBLE NULL, " +
                        "PowerAC DOUBLE NULL, " +
                        "ImpedanceAC DOUBLE NULL, " +
                        "Hours MEDIUMINT NULL, " +
                        "Mode MEDIUMINT NULL, " +
                        "PRIMARY KEY (Inverter_Id, OutputTime) , " +
                        "INDEX fk_cmsdata_inverter (Inverter_Id ASC) , " +
                        "CONSTRAINT fk_cmsdata_inverter " +
                            "FOREIGN KEY (Inverter_Id ) " +
                            "REFERENCES Inverter (Id ) " +
                            "ON DELETE NO ACTION " +
                            "ON UPDATE NO ACTION " +
                    ") ";
            }
        }

        public override String Alter_outputhistory_1500
        {
            get
            {
                return
                    "Alter Table outputhistory " +
                        "add MinPower real null, " +
                        "add MaxPower real null ";
            }
        }

        public override String View_pvoutput_sub_v_1500
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput_sub_v` AS " +
                    "select DATE(oh.OutputTime) as OutputDay, TIME_TO_SEC(TIME(oh.OutputTime)) as OutputTime, " +
                    "sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) as Power, " +
                    "MIN(MinPower) AS Min_Power, " +
                    "MAX(MaxPower) AS Max_Power " +
                    "from outputhistory as oh " +
                    "where oh.OutputKwh > 0 " +
                    "and oh.OutputTime >= (curdate() - 20) " +
                    "group by DATE(oh.OutputTime), TIME_TO_SEC(TIME(oh.OutputTime))";
            }
        }

        public override String View_pvoutput5min_v_1500
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput5min_v` AS " +
                    "select OutputDay, (OutputTime DIV 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power)*1000 AS RealMaxPower, " +
                    "MIN(Min_Power)*1000 AS RealMinPower " +
                    "from pvoutput_sub_v " +
                    "group by OutputDay, (OutputTime DIV 300) * 300";
            }
        }

        public override String View_pvoutput_v_1500
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput_v` AS " +
                    "select OutputDay, (OutputTime DIV 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power)*1000 AS RealMaxPower, " +
                    "MIN(Min_Power)*1000 AS RealMinPower " +
                    "from pvoutput_sub_v " +
                    "group by OutputDay, (OutputTime DIV 600) * 600";
            }
        }

        public override string Alter_inverter_1700
        {
            get
            {
                return
                    "alter table `inverter` " +
                    "add SiteId varchar(10) NULL ";
            }
        }

        public override String View_pvoutput_sub_v_1700
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput_sub_v` AS " +
                    "select i.SiteId as SiteId, DATE(oh.OutputTime) as OutputDay, TIME_TO_SEC(TIME(oh.OutputTime)) as OutputTime, " +
                    "sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) as Power, " +
                    "MIN(MinPower) AS Min_Power, " +
                    "MAX(MaxPower) AS Max_Power " +
                    "from outputhistory as oh, inverter as i " +
                    "where oh.Inverter_Id = i.Id " +
                    "and oh.OutputKwh > 0 " +
                    "and oh.OutputTime >= (curdate() - 20) " +
                    "group by i.SiteId, DATE(oh.OutputTime), TIME_TO_SEC(TIME(oh.OutputTime))";
            }
        }

        public override String View_pvoutput5min_v_1700
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput5min_v` AS " +
                    "select SiteId, OutputDay, (OutputTime DIV 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power)*1000 AS RealMaxPower, " +
                    "MIN(Min_Power)*1000 AS RealMinPower " +
                    "from pvoutput_sub_v " +
                    "group by SiteId, OutputDay, (OutputTime DIV 300) * 300";
            }
        }

        public override String View_pvoutput_v_1700
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput_v` AS " +
                    "select SiteId, OutputDay, (OutputTime DIV 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power)*1000 AS RealMaxPower, " +
                    "MIN(Min_Power)*1000 AS RealMinPower " +
                    "from pvoutput_sub_v " +
                    "group by SiteId, OutputDay, (OutputTime DIV 600) * 600";
            }
        }

        public override String Alter_meterreading_dropPK_1700
        {
            get
            {
                return "ALTER TABLE meterreading DROP PRIMARY KEY ";
            }
        }

        public override String Alter_meterreading_createPK_1700
        {
            get
            {
                return "ALTER TABLE meterreading ADD PRIMARY KEY " +
                        "( Meter_Id, ReadingTime, Appliance ) ";
            }
        }

        public override string Alter_meter_1710
        {
            get
            {
                return
                    "alter table `meter` " +
                    "add `InstanceNo` MEDIUMINT NULL, " +
                    "add `NextDate` DATETIME NULL ";
            }
        }

        public override string Alter_cmsdata_1830
        {
            get
            {
                return
                    "alter table `cmsdata` " +
                    "add `VoltsPV2` DOUBLE NULL, " +
                    "add `PowerPV` DOUBLE NULL, " +
                    "add `EstEnergy` DOUBLE NULL, " +
                    "add `ErrorCode` MEDIUMINT NULL ";
            }
        }

        public override String View_pvoutput_sub_v_1836
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput_sub_v` AS " +
                    "select i.SiteId as SiteId, DATE(oh.OutputTime) as OutputDay, TIME_TO_SEC(TIME(oh.OutputTime)) as OutputTime, " +
                    "sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) as Power, " +
                    "MIN(MinPower) AS Min_Power, " +
                    "MAX(MaxPower) AS Max_Power, " +
                    "MAX(oh.Temperature) AS Max_Temp " +
                    "from outputhistory as oh, inverter as i " +
                    "where oh.Inverter_Id = i.Id " +
                    "and oh.OutputKwh > 0 " +
                    "and oh.OutputTime >= (curdate() - 20) " +
                    "group by i.SiteId, DATE(oh.OutputTime), TIME_TO_SEC(TIME(oh.OutputTime))";
            }
        }

        public override String View_pvoutput5min_v_1836
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput5min_v` AS " +
                    "select SiteId, OutputDay, (OutputTime DIV 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power)*1000 AS RealMaxPower, " +
                    "MIN(Min_Power)*1000 AS RealMinPower, " +
                    "MAX(Max_Temp) AS MaxTemp " +
                    "from pvoutput_sub_v " +
                    "group by SiteId, OutputDay, (OutputTime DIV 300) * 300";
            }
        }

        public override String View_pvoutput_v_1836
        {
            get
            {
                return
                    "CREATE VIEW `pvoutput_v` AS " +
                    "select SiteId, OutputDay, (OutputTime DIV 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power)*1000 AS RealMaxPower, " +
                    "MIN(Min_Power)*1000 AS RealMinPower, " +
                    "MAX(Max_Temp) AS MaxTemp " +
                    "from pvoutput_sub_v " +
                    "group by SiteId, OutputDay, (OutputTime DIV 600) * 600";
            }
        }

        public override string Alter_cmsdata_1836
        {
            get
            {
                return
                    "alter table `cmsdata` " +
                    "add `VoltsPV1` DOUBLE NULL, " +
                    "add `VoltsPV3` DOUBLE NULL, " +
                    "add `CurrentPV1` DOUBLE NULL, " +
                    "add `CurrentPV2` DOUBLE NULL, " +
                    "add `CurrentPV3` DOUBLE NULL ";
            }
        }

        public override string Alter_outputhistory_1836
        {
            get
            {
                return
                    "alter table `outputhistory` " +
                    "add `Temperature` DOUBLE NULL " ;
            }
        }

        public override string Drop_inverter_index_1902
        {
            get
            {
                return "drop index InverterIdentity ON inverter ";
            }
        }

        public override string Create_inverter_index_1902
        {
            get
            {
                return "create unique index InverterIdentity_UK on inverter (SerialNumber, InverterType_Id, InverterManager_Id) ";
            }
        }

        public override string Table_pvoutputlog_2000
        {
            get
            {
                return
                    "create table `pvoutputlog` " +
                    "( " +
                        "`SiteId` varchar(10) NOT NULL, " +
                        "`OutputDay` date NOT NULL, " +
                        "`OutputTime` mediumint(9) NOT NULL, " +
                        "`Loaded` tinyint(1) NOT NULL DEFAULT '0', " +
                        "`ImportEnergy` double NULL, " +
                        "`ImportPower` double NULL, " +
                        "`Temperature` double NULL, " +
                        "PRIMARY KEY (`SiteId`,`OutputDay`,`OutputTime`) " +
                     ") ENGINE=InnoDB DEFAULT CHARSET=latin1";
            }
        }

        public override string Table_devicetype_2000
        {
            get
            {
                return
                    "CREATE TABLE if not exists `devicetype` " +
                    "( " +
                        "`Id` MEDIUMINT UNSIGNED NOT NULL AUTO_INCREMENT, " +
                        "`Manufacturer` VARCHAR(60) NOT NULL, " +
                        "`Model` VARCHAR(50) NOT NULL, " +
                        "`MaxPower` MEDIUMINT NULL, " +
                        "`DeviceType` VARCHAR(20) NOT NULL, " +
                        "PRIMARY KEY (`Id` ), " +
                        "CONSTRAINT `uk1_devicetype` UNIQUE (`Manufacturer` ASC, `Model` ASC) " +
                    ") ENGINE=InnoDB DEFAULT CHARSET=latin1 ";
            }
        }

        public override string Table_device_2000
        {
            get
            {
                return
                    "CREATE TABLE `device` " +
                    "( " +
                        "`Id` MEDIUMINT UNSIGNED NOT NULL AUTO_INCREMENT, " +
                        "`SerialNumber` VARCHAR(45) NOT NULL, " +
                        "`DeviceType_Id` MEDIUMINT UNSIGNED NOT NULL, " +
                        "PRIMARY KEY (`Id` ), " +
                        "CONSTRAINT `uk1_device` UNIQUE (`SerialNumber` ASC, `DeviceType_Id` ASC), " +
                        "CONSTRAINT `fk_device_devicetype` FOREIGN KEY (`DeviceType_Id`) REFERENCES `devicetype` (`Id`) " +
                    ") ENGINE=InnoDB DEFAULT CHARSET=latin1 ";
            }
        }

        public override string Table_devicefeature_2000
        {
            get
            {
                return
                    "CREATE TABLE `devicefeature` " +
                    "( " +
                        "`Id` MEDIUMINT UNSIGNED NOT NULL AUTO_INCREMENT, " +
                        "`Device_Id` MEDIUMINT UNSIGNED NOT NULL, " +
                        "`FeatureType` SMALLINT NOT NULL, " +
                        "`FeatureId` TINYINT NOT NULL, " +
                        "`MeasureType` VARCHAR(20) NOT NULL, " +
                        "`IsConsumption` CHAR(1) NULL, " +
                        "`IsAC` CHAR(1) NULL, " +
                        "`IsThreePhase` CHAR(1) NULL, " +
                        "`StringNumber` MEDIUMINT NULL, " +
                        "`PhaseNumber` MEDIUMINT NULL, " +
                        "PRIMARY KEY ( `Id` ), " +
                        "CONSTRAINT `uk1_devicefeature` UNIQUE (`Device_Id` ASC, `FeatureType` ASC, `FeatureId` ASC), " +
                        "CONSTRAINT `fk_devicefeature_device` FOREIGN KEY (`Device_Id`) REFERENCES `device` (`Id`) " +
                    ") ENGINE=InnoDB DEFAULT CHARSET=latin1 ";
            }
        }

        public override string Table_devicereading_energy_2000
        {
            get
            {
                return
                    "CREATE TABLE `devicereading_energy` " +
                    "( " +
                        "`ReadingEnd` DATETIME NOT NULL, " +
                        "`DeviceFeature_Id` MEDIUMINT UNSIGNED NOT NULL, " +
                        "`ReadingStart` DATETIME NULL, " +
                        "`EnergyTotal` DOUBLE NULL, " +
                        "`EnergyToday` DOUBLE NULL, " +
                        "`EnergyDelta` FLOAT NULL, " +
                        "`CalcEnergyDelta` FLOAT NULL, " +
                        "`HistEnergyDelta` FLOAT NULL, " +
                        "`Mode` MEDIUMINT NULL, " +
                        "`ErrorCode` MEDIUMINT NULL, " +
                        "`Power` MEDIUMINT NULL, " +
                        "`Volts` FLOAT NULL, " +
                        "`Amps` FLOAT NULL, " +
                        "`Frequency` FLOAT NULL, " +
                        "`Temperature` FLOAT NULL, " +
                        "`MinPower` MEDIUMINT NULL, " +
                        "`MaxPower` MEDIUMINT NULL, " +                        
                        "PRIMARY KEY (`ReadingEnd`, `DeviceFeature_Id` ) , " +
                        "CONSTRAINT `uk_devicereading_energy` UNIQUE (`DeviceFeature_Id`, `ReadingEnd`) , " +
                        "CONSTRAINT `fk_devicereadingenergy_devicefeature` FOREIGN KEY (DeviceFeature_Id) REFERENCES `devicefeature` (Id) " +
                    ") ENGINE=InnoDB DEFAULT CHARSET=latin1 ";
            }
        }

        public override String View_devicedayoutput_v_2000
        {
            get
            {
                return
                    "CREATE VIEW devicedayoutput_v " +
                    "AS SELECT f.Device_Id, DATE(r.ReadingEnd) OutputDay, SUM(r.EnergyDelta) OutputKwh " +
                    "from devicereading_energy r, devicefeature f " +
                    "where r.DeviceFeature_Id = f.Id " +
                    "group by f.Device_Id, DATE(r.ReadingEnd) ";
            }
        }
    }

    internal class SQLite_DDL : DDL
    {
        // required for 1.3.0.0 and later
        public override String Table_version
        {
            get
            {
                return
                    "create table `version` " +
                    "( " +
                        "`Major` TEXT NOT NULL, " +
                        "`Minor` TEXT NOT NULL, " +
                        "`Release` TEXT NOT NULL, " +
                        "`Patch` TEXT NOT NULL " +
                     ")";
            }
        }

        // required for 1.3.0.0 and later
        public override string Table_pvoutputlog
        {
            get
            {
                return
                    "create table `pvoutputlog` " +
                    "( " +
                        "`SiteId` TEXT NOT NULL, " +
                        "`OutputDay` date NOT NULL, " +
                        "`OutputTime` INTEGER NOT NULL, " +
                        "`Energy` REAL NULL, " +
                        "`Power` REAL NULL, " +
                        "`Loaded` INTEGER NOT NULL DEFAULT '0', " +
                        "`ImportEnergy` REAL NULL, " +
                        "`ImportPower` REAL NULL, " +
                        "PRIMARY KEY (`SiteId`,`OutputDay`,`OutputTime`) " +
                     ")";
            }
        }

        // required for 1.3.0.0 and later
        public override string View_pvoutput_v
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput_v] AS " +
                    "select OutputDay, (OutputTime / 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower " +
                    "from pvoutput_sub_v " +
                    "group by OutputDay, (OutputTime / 600) * 600";
            }
        }

        public override string View_pvoutput5min_v
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput5min_v] AS " +
                    "select OutputDay, (OutputTime / 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower " +
                    "from pvoutput_sub_v " +                    
                    "group by OutputDay, (OutputTime / 300) * 300";
            }
        }

        public override string View_pvoutput_sub_v
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput_sub_v] AS " +
                    "select DATE(oh.OutputTime) as OutputDay, " +
                    "(strftime('%s', oh.OutputTime) - strftime('%s', date(oh.OutputTime))) as OutputTime, " +
                    "sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) as Power " +
                    "from outputhistory as oh " +
                    "where oh.OutputKwh > 0 " +
                    "and oh.OutputTime >= date('now', '-20 day') " + 
                    "group by DATE(oh.OutputTime), (strftime('%s', oh.OutputTime) - strftime('%s', date(oh.OutputTime)))";
            }
        }

        // required for 1.4.0.0 and later
        public override string Alter_pvoutputlog_1400a
        {
            get
            {
                return
                    "alter table `pvoutputlog` add " +
                    "`ImportEnergy` REAL ";
            }
        }

        public override string Alter_pvoutputlog_1400b
        {
            get
            {
                return
                    "alter table `pvoutputlog` add " +
                    "`ImportPower` REAL ";
            }
        }

        // required for 1.4.2.1 and later
        public override string Alter_pvoutputlog_1421
        {
            get
            {
                return
                    "alter table `pvoutputlog` " +
                    "add `Temperature` double NULL ";
            }
        }

        public override string Table_meter
        {
            get
            {
                return
                    "create table if not exists `meter` " +
                    "( " +
                        "Id INTEGER PRIMARY KEY AUTOINCREMENT , " +
                        "`MeterName` TEXT NOT NULL , " +
                        "`MeterType` TEXT NOT NULL, " +
                        "`StandardDuration` int NULL , " +
                        "`CondensedDuration` int NULL , " +
                        "`CondenseAge` INTEGER NULL , " +
                        "`Enabled` INTEGER  NULL , " +
                        "constraint metername_uk UNIQUE(`MeterName`) " + 
                     ")";
            }
        }

        public override string Table_meterreading
        {
            get
            {
                return
                    "create table if not exists `meterreading` " +
                    "( " +
                        "`Meter_Id` INTEGER NOT NULL , " +
                        "`Appliance` INTEGER NOT NULL , " +
                        "`ReadingTime` DATETIME NOT NULL , " +
                        "`Duration` INTEGER NULL , " +
                        "`Energy` REAL NULL , " +
                        "`Temperature` REAL NULL , " +
                        "`Calculated` REAL NULL , " +
                        "`MinPower` INTEGER NULL , " +
                        "`MaxPower` INTEGER NULL , " +
                        "constraint meterreading_pk PRIMARY KEY ( `Meter_Id`, `Appliance`, `ReadingTime`) " +
                    ")";
            }
        }

        public override string Table_meterhistory
        {
            get
            {
                return
                    "create table if not exists `meterhistory` " +
                    "( " +
                        "`Meter_Id` INTEGER NOT NULL , " +
                        "`Appliance` INTEGER NOT NULL , " +
                        "`ReadingTime` DATETIME NOT NULL , " +
                        "`HistoryType` CHAR NOT NULL , " +
                        "`Duration` INTEGER NOT NULL , " +
                        "`Energy` REAL NOT NULL , " +
                        "constraint meterhistory_pk PRIMARY KEY ( `Meter_Id`, `Appliance`, `ReadingTime`, `HistoryType`) " +
                    ")";
            }
        }

        public override String Table_cmsdata_1500
        {
            get
            {
                return
                    "CREATE TABLE if not exists cmsdata " +
                    "( " +
                        "Inverter_Id INTEGER NOT NULL, " +
                        "OutputTime DATETIME NOT NULL, " +
                        "EnergyTotal REAL NOT NULL, " +
                        "EnergyToday REAL NULL, " +
                        "Temperature REAL NULL, " +
                        "VoltsPV REAL NULL, " +
                        "CurrentAC REAL NULL, " +
                        "VoltsAC REAL NULL, " +
                        "FrequencyAC REAL NULL, " +
                        "PowerAC REAL NULL, " +
                        "ImpedanceAC REAL NULL, " +
                        "Hours INTEGER NULL, " +
                        "Mode INTEGER NULL, " +
                        "CONSTRAINT PK_cmsdata PRIMARY KEY " +
                        "( " +
                            "Inverter_Id, " +
                            "OutputTime " +
                        ") " +
                    ") ";
            }
        }

        public override String Alter_outputhistory_1500
        {
            get
            {
                return
                    "Alter Table outputhistory ADD " +
                        "MinPower REAL null ";
            }
        }

        public String Alter_outputhistory_1500b
        {
            get
            {
                return
                    "Alter Table outputhistory ADD " +
                        "MaxPower REAL null ";
            }
        }

        public override String View_pvoutput_sub_v_1500
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput_sub_v] AS " +
                    "select DATE(oh.OutputTime) as OutputDay, " +
                    "(strftime('%s', oh.OutputTime) - strftime('%s', date(oh.OutputTime))) as OutputTime, " +
                    "sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) as Power, " +
                    "MIN(MinPower) AS Min_Power, " +
                    "MAX(MaxPower) AS Max_Power " +
                    "from outputhistory as oh " +
                    "where oh.OutputKwh > 0 " +
                    "and oh.OutputTime >= date('now', '-20 day') " +
                    "group by DATE(oh.OutputTime), (strftime('%s', oh.OutputTime) - strftime('%s', date(oh.OutputTime)))";
            }
        }

        public override String View_pvoutput5min_v_1500
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput5min_v] AS " +
                    "select OutputDay, (OutputTime / 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "from pvoutput_sub_v " +
                    "group by OutputDay, (OutputTime / 300) * 300";
            }
        }

        public override String View_pvoutput_v_1500
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput_v] AS " +
                    "select OutputDay, (OutputTime / 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "from pvoutput_sub_v " +
                    "group by OutputDay, (OutputTime / 600) * 600";
            }
        }

        public string Drop_meterreading
        {
            get
            {
                return
                    "drop table `meterreading` " ;
            }
        }

        public override string Alter_inverter_1700
        {
            get
            {
                return
                    "alter table inverter " +
                    "add SiteId TEXT NULL ";
            }
        }

        public override String View_pvoutput_sub_v_1700
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput_sub_v] AS " +
                    "select i.SiteId as SiteId, DATE(oh.OutputTime) as OutputDay, " +
                    "(strftime('%s', oh.OutputTime) - strftime('%s', date(oh.OutputTime))) as OutputTime, " +
                    "sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) as Power, " +
                    "MIN(MinPower) AS Min_Power, " +
                    "MAX(MaxPower) AS Max_Power " +
                    "from outputhistory as oh, inverter as i " +
                    "where oh.Inverter_Id = i.Id " +
                    "and oh.OutputKwh > 0 " +
                    "and oh.OutputTime >= date('now', '-20 day') " +
                    "group by i.SiteId, DATE(oh.OutputTime), (strftime('%s', oh.OutputTime) - strftime('%s', date(oh.OutputTime)))";
            }
        }

        public override String View_pvoutput5min_v_1700
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput5min_v] AS " +
                    "select SiteId, OutputDay, (OutputTime / 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "from pvoutput_sub_v " +
                    "group by SiteId, OutputDay, (OutputTime / 300) * 300";
            }
        }

        public override String View_pvoutput_v_1700
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput_v] AS " +
                    "select SiteId, OutputDay, (OutputTime / 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "from pvoutput_sub_v " +
                    "group by SiteId, OutputDay, (OutputTime / 600) * 600";
            }
        }

        public string Table_meterreading_1700
        {
            get
            {
                return
                    "create table if not exists `meterreading` " +
                    "( " +
                        "`Meter_Id` INTEGER NOT NULL , " +
                        "`ReadingTime` DATETIME NOT NULL , " +
                        "`Appliance` INTEGER NOT NULL , " +
                        "`Duration` INTEGER NULL , " +
                        "`Energy` REAL NULL , " +
                        "`Temperature` REAL NULL , " +
                        "`Calculated` REAL NULL , " +
                        "`MinPower` INTEGER NULL , " +
                        "`MaxPower` INTEGER NULL , " +
                        "constraint meterreading_pk PRIMARY KEY ( `Meter_Id`, `ReadingTime`, `Appliance`) " +
                    ")";
            }
        }

        public override string Alter_meter_1710
        {
            get
            {
                return
                    "alter table meter " +
                    "add InstanceNo INTEGER NULL ";
            }
        }

        public string Alter_meter_1710a
        {
            get
            {
                return
                    "alter table meter " +
                    "add NextDate DATETIME NULL ";
            }
        }

        public override string Alter_cmsdata_1830
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add VoltsPV2 REAL NULL ";
            }
        }

        public string Alter_cmsdata_1830a
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add PowerPV REAL NULL ";
            }
        }

        public string Alter_cmsdata_1830b
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add EstEnergy REAL NULL ";
            }
        }

        public string Alter_cmsdata_1830c
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add ErrorCode INTEGER NULL ";
            }
        }

        public override String View_pvoutput_sub_v_1836
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput_sub_v] AS " +
                    "select i.SiteId as SiteId, DATE(oh.OutputTime) as OutputDay, " +
                    "(strftime('%s', oh.OutputTime) - strftime('%s', date(oh.OutputTime))) as OutputTime, " +
                    "sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) as Power, " +
                    "MIN(MinPower) AS Min_Power, " +
                    "MAX(MaxPower) AS Max_Power, " +
                    "MAX(oh.Temperature) AS Max_Temp " +
                    "from outputhistory as oh, inverter as i " +
                    "where oh.Inverter_Id = i.Id " +
                    "and oh.OutputKwh > 0 " +
                    "and oh.OutputTime >= date('now', '-20 day') " +
                    "group by i.SiteId, DATE(oh.OutputTime), (strftime('%s', oh.OutputTime) - strftime('%s', date(oh.OutputTime)))";
            }
        }

        public override String View_pvoutput5min_v_1836
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput5min_v] AS " +
                    "select SiteId, OutputDay, (OutputTime / 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower, " +
                    "MAX(Max_Temp) AS MaxTemp " +
                    "from pvoutput_sub_v " +
                    "group by SiteId, OutputDay, (OutputTime / 300) * 300";
            }
        }

        public override String View_pvoutput_v_1836
        {
            get
            {
                return
                    "CREATE VIEW [pvoutput_v] AS " +
                    "select SiteId, OutputDay, (OutputTime / 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, " +
                    "max(Power)*1000 as Power, min(Power)*1000 as MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower, " +
                    "MAX(Max_Temp) AS MaxTemp " +
                    "from pvoutput_sub_v " +
                    "group by SiteId, OutputDay, (OutputTime / 600) * 600";
            }
        }

        public override string Alter_outputhistory_1836
        {
            get
            {
                return
                    "alter table outputhistory " +
                    "add Temperature REAL NULL ";
            }
        }

        public override string Alter_cmsdata_1836
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add VoltsPV1 REAL NULL ";
            }
        }

        public string Alter_cmsdata_1836a
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add VoltsPV3 REAL NULL ";
            }
        }

        public string Alter_cmsdata_1836b
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add CurrentPV1 REAL NULL ";
            }
        }

        public string Alter_cmsdata_1836c
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add CurrentPV2 REAL NULL ";
            }
        }

        public string Alter_cmsdata_1836d
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add CurrentPV3 REAL NULL ";
            }
        }

        public override string  Drop_inverter_index_1902
        {
	        get 
            {
                return "drop index IDX_INVERTER_SERIAL "; 
            }
        }

        public override string Create_inverter_index_1902
        {
            get
            {
                return "create unique index InverterIdentity_UK on inverter (SerialNumber, InverterType_Id, InverterManager_Id) ";
            }
        }

        public override string Table_pvoutputlog_2000
        {
            get
            {
                return
                    "create table `pvoutputlog` " +
                    "( " +
                        "`SiteId` TEXT NOT NULL, " +
                        "`OutputDay` date NOT NULL, " +
                        "`OutputTime` INTEGER NOT NULL, " +
                        "`Energy` REAL NULL, " +
                        "`Power` REAL NULL, " +
                        "`Loaded` INTEGER NOT NULL DEFAULT '0', " +
                        "`ImportEnergy` REAL NULL, " +
                        "`ImportPower` REAL NULL, " +
                        "`Temperature` REAL NULL, " +
                        "PRIMARY KEY (`SiteId`,`OutputDay`,`OutputTime`) " +
                     ")";
            }
        }


        public override string Table_devicetype_2000
        {
            get
            {
                return
                    "CREATE TABLE devicetype " +
                    "( " +
                        "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "Manufacturer TEXT NOT NULL, " +
                        "Model TEXT NOT NULL, " +
                        "MaxPower INTEGER NULL, " +
                        "DeviceType TEXT NOT NULL, " +
                        "CONSTRAINT DeviceType_UK1 UNIQUE (Manufacturer, Model)  " +
                    ") ";
            }
        }

        public override string Table_device_2000
        {
            get
            {
                return
                    "CREATE TABLE device " +
                    "( " +
                        "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "SerialNumber TEXT NOT NULL, " +
                        "DeviceType_Id INTEGER NOT NULL, " +
                        "FOREIGN KEY (DeviceType_Id) REFERENCES devicetype (Id), " +
                        "CONSTRAINT uk_device UNIQUE (SerialNumber, DeviceType_Id)  " +
                    ") ";
            }
        }

        public override string Table_devicefeature_2000
        {
            get
            {
                return
                    "CREATE TABLE devicefeature " +
                    "( " +
                        "`Id` INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "`Device_Id` INTEGER NOT NULL, " +
                        "`FeatureType` INTEGER NOT NULL, " +
                        "`FeatureId` INTEGER NOT NULL, " +
                        "`MeasureType` TEXT NOT NULL, " +
                        "`IsConsumption` TEXT NULL, " +
                        "`IsAC` TEXT NULL, " +
                        "`IsThreePhase` TEXT NULL, " +
                        "`StringNumber` INTEGER NULL, " +
                        "`PhaseNumber` INTEGER NULL, " +
                        "CONSTRAINT uk_devicefeature UNIQUE (Device_Id, FeatureType, FeatureId)  " +
                        "CONSTRAINT `fk_devicefeature_device` FOREIGN KEY (`Device_Id`) REFERENCES `device` (`Id`) " +
                    ") ";
            }
        }

        public override string Table_devicereading_energy_2000
        {
            get
            {
                return
                    "CREATE TABLE devicereading_energy " +
                    "( " +
                        "ReadingEnd DATETIME NOT NULL, " +
                        "DeviceFeature_Id INTEGER NOT NULL, " +
                        "ReadingStart DATETIME NOT NULL, " +
                        "EnergyTotal REAL NULL, " +
                        "EnergyToday REAL NULL, " +
                        "EnergyDelta REAL NULL, " +
                        "CalcEnergyDelta REAL NULL, " +
                        "HistEnergyDelta REAL NULL, " +
                        "Mode INTEGER NULL, " +
                        "ErrorCode INTEGER NULL, " +
                        "Power INTEGER NULL, " +
                        "Volts REAL NULL, " +
                        "Amps REAL NULL, " +
                        "Frequency REAL NULL, " +
                        "Temperature REAL NULL, " +
                        "MinPower INTEGER NULL, " +
                        "MaxPower INTEGER NULL, " +                       
                        "CONSTRAINT PK_devicereading_ac_1 PRIMARY KEY (ReadingEnd, DeviceFeature_Id ), " +
                        "CONSTRAINT uk_devicereading_ac UNIQUE (DeviceFeature_Id, ReadingEnd),  " +
                        "FOREIGN KEY (DeviceFeature_Id) REFERENCES devicefeature (Id) " +
                    ") ";
            }
        }

        public override String View_devicedayoutput_v_2000
        {
            get
            {
                return
                    "CREATE VIEW devicedayoutput_v " +
                    "AS SELECT f.Device_Id, Date(r.ReadingEnd) OutputDay, SUM(r.EnergyDelta) OutputKwh " +
                    "from devicereading_energy r, devicefeature f " +
                    "where r.DeviceFeature_Id = f.Id " +
                    "group by f.Device_Id, Date(r.ReadingEnd) ";
            }
        }
    }

    internal class Jet_DDL : DDL
    {
        // required for 1.3.0.0 and later
        public override String Table_version
        {
            get
            {
                return
                    "create table version " +
                    "( " +
                        "Major varchar(4) NOT NULL, " +
                        "Minor varchar(4) NOT NULL, " +
                        "Release varchar(4) NOT NULL, " +
                        "Patch varchar(4) NOT NULL " +
                    ")";
            }
        }

        // required for 1.3.0.0 and later
        public override string Table_pvoutputlog
        {
            get
            {
                return
                    "create table pvoutputlog " +
                    "( " +
                        "SiteId varchar(10) NOT NULL, " +
                        "OutputDay date NOT NULL, " +
                        "OutputTime integer NOT NULL, " +
                        "Energy double NULL, " +
                        "Power double NULL, " +
                        "Loaded smallint NOT NULL default 0, " +
                        "ImportEnergy double NULL, " +
                        "ImportPower double NULL, " +
                        "constraint pvoutputlog_pk PRIMARY KEY ( SiteId, OutputDay, OutputTime ) " +
                     ")";
            }
        }

        // required for 1.3.0.0 and later
        public override string View_pvoutput_v
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_v AS " +
                    "SELECT pv.OutputDay, int(int(pv.OutputTime/600)*600) AS OutputTime, " +
                    "Sum(pv.Energy)*1000.0 AS Energy, Max(pv.Power)*1000 AS Power, " +
                    "Min(pv.Power)*1000 AS MinPower " +
                    "FROM pvoutput_sub_v AS pv " +
                    "GROUP BY pv.OutputDay, int(int(pv.OutputTime/600)*600)";
            }
        }

        public override string View_pvoutput5min_v
        {
            get
            {
                return
                    "CREATE VIEW pvoutput5min_v AS " +
                    "SELECT pv.OutputDay, int(int(pv.OutputTime/300)*300) AS OutputTime, " +
                    "Sum(pv.Energy)*1000.0 AS Energy, Max(pv.Power)*1000 AS Power, " +
                    "Min(pv.Power)*1000 AS MinPower " +
                    "FROM pvoutput_sub_v AS pv " +
                    "GROUP BY pv.OutputDay, int(int(pv.OutputTime/300)*300)";
            }
        }

        public override string View_pvoutput_sub_v
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_sub_v AS " +
                    "SELECT DateValue(oh.OutputTime) AS OutputDay, " +
                    "int((Hour(TimeValue(oh.OutputTime))*60+Minute(TimeValue(oh.OutputTime)))*60) AS OutputTime, " +
                    "Sum(oh.OutputKwh) AS Energy, Sum(oh.OutputKwh*3600/oh.Duration) AS Power " +
                    "FROM outputhistory AS oh " +
                    "WHERE oh.OutputKwh > 0 " +
                    "AND (((oh.OutputTime)>=(Date()-20))) " + 
                    "GROUP BY DateValue(oh.OutputTime), int((Hour(TimeValue(oh.OutputTime))*60+Minute(TimeValue(oh.OutputTime)))*60)";
            }
        }

        // required for 1.4.0.0 and later
        public override string Alter_pvoutputlog_1400a
        {
            get
            {
                return
                    "alter table pvoutputlog add column " +
                    "ImportEnergy double NULL ";
            }
        }

        public string Alter_pvoutputlog_1400a2
        {
            get
            {
                return
                    "alter table pvoutputlog add column " +
                    "ImportPower double NULL ";
            }
        }

        public override string Alter_pvoutputlog_1400b
        {
            get
            {
                return
                    "alter table pvoutputlog alter column " +
                    "Energy double "; 
            }
        }

        public string Alter_pvoutputlog_1400b2
        {
            get
            {
                return
                    "alter table pvoutputlog alter column " +
                    "Power double ";
            }
        }

        // required for 1.4.2.1 and later
        public override string Alter_pvoutputlog_1421
        {
            get
            {
                return
                    "alter table pvoutputlog " +
                    "add column Temperature double NULL ";
            }
        }

        public override string Table_meter
        {
            get
            {
                return
                    "create table `meter` " +
                    "( " +
                        "`Id` AUTOINCREMENT  , " +
                        "`MeterName` varchar(45) NOT NULL, " +
                        "`MeterType` varchar(45) NOT NULL, " +
                        "`StandardDuration` INTEGER NULL , " +
                        "`CondensedDuration` INTEGER NULL , " +
                        "`CondenseAge` INTEGER NULL , " +
                        "`Enabled` smallint  NULL , " +
                        "constraint meter_pk PRIMARY KEY ( Id ), " +
                        "constraint metername_uk UNIQUE(`MeterName`) " +
                     ")";
            }
        }

        public override string Table_meterreading
        {
            get
            {
                return
                    "create table `meterreading` " +
                        "( " +
                        "`Meter_Id` INTEGER NOT NULL , " +
                        "`Appliance` smallint NOT NULL,  " +
                        "`ReadingTime` DATETIME NOT NULL , " +
                        "`Duration`INTEGER NULL , " +
                        "`Energy` FLOAT NULL , " +
                        "`Temperature` FLOAT NULL , " +
                        "`Calculated` FLOAT NULL , " +
                        "`MinPower` INTEGER NULL , " +
                        "`MaxPower` INTEGER NULL , " +
                        "constraint meterreading_pk PRIMARY KEY " +
                            "( `Meter_Id`, `Appliance`, `ReadingTime`), " +
                        "constraint `fk_MeterReading_Meter` " +
                            "FOREIGN KEY (`Meter_Id` ) REFERENCES `Meter` (`Id` ) " +
                     ")";
            }
        }

        public override string Alter_meterreading_1400
        {
            get
            {
                return
                    "alter table `meterreading` " +
                    "alter column `Energy` DOUBLE NULL ";
            }
        }

        public override string Alter_meterreading_1401a
        {
            get
            {
                return
                    "alter table `meterreading` " +
                    "drop column `ReadingType` ";
            }
        }

        public override string Alter_meterreading_1401b
        {
            get
            {
                return
                    "alter table `meterreading` " +
                    "drop column `ReadingNo` ";
            }
        }


        public override string Table_meterhistory
        {
            get
            {
                return
                    "create table `meterhistory` " +
                        "( " +
                        "`Meter_Id` INTEGER NOT NULL , " +
                        "`Appliance` smallint NOT NULL,  " +
                        "`ReadingTime` DATETIME NOT NULL , " +
                        "`HistoryType` CHAR(1) NOT NULL , " +
                        "`Duration`INTEGER NOT NULL , " +
                        "`Energy` FLOAT NOT NULL , " +
                        "constraint meterhistory_pk PRIMARY KEY " +
                            "( `Meter_Id`, `Appliance`, `ReadingTime`, `HistoryType`), " +
                        "constraint `fk_MeterHistory_Meter` " +
                            "FOREIGN KEY (`Meter_Id` ) REFERENCES `Meter` (`Id` ) " +
                     ")";
            }
        }

        public override String Table_cmsdata_1500
        {
            get
            {
                return
                    "CREATE TABLE cmsdata " +
                    "( " +
                        "Inverter_Id INTEGER NOT NULL, " +
                        "OutputTime DATETIME NOT NULL, " +
                        "EnergyTotal FLOAT NOT NULL, " +
                        "EnergyToday FLOAT NULL, " +
                        "Temperature FLOAT NULL, " +
                        "VoltsPV FLOAT NULL, " +
                        "CurrentAC FLOAT NULL, " +
                        "VoltsAC FLOAT NULL, " +
                        "FrequencyAC FLOAT NULL, " +
                        "PowerAC FLOAT NULL, " +
                        "ImpedanceAC FLOAT NULL, " +
                        "Hours INTEGER NULL, " +
                        "Mode INTEGER NULL, " +
                        "CONSTRAINT PK_cmsdata PRIMARY KEY " +
                        "( " +
                            "Inverter_Id, " +
                            "OutputTime " +
                        ") " +
                    ") ";
            }
        }

        public override String Alter_outputhistory_1500
        {
            get
            {
                return
                    "Alter Table outputhistory ADD " +
                        "MinPower FLOAT null ";
            }
        }

        public String Alter_outputhistory_1500b
        {
            get
            {
                return
                    "Alter Table outputhistory ADD " +
                        "MaxPower FLOAT null ";
            }
        }

        public override String View_pvoutput_sub_v_1500
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_sub_v AS " +
                    "SELECT DateValue(oh.OutputTime) AS OutputDay, " +
                    "int((Hour(TimeValue(oh.OutputTime))*60+Minute(TimeValue(oh.OutputTime)))*60) AS OutputTime, " +
                    "Sum(oh.OutputKwh) AS Energy, Sum(oh.OutputKwh*3600/oh.Duration) AS Power, " +
                    "MIN(MinPower) AS Min_Power, " +
                    "MAX(MaxPower) AS Max_Power " +
                    "FROM outputhistory AS oh " +
                    "WHERE oh.OutputKwh > 0 " +
                    "AND (((oh.OutputTime)>=(Date()-20))) " +
                    "GROUP BY DateValue(oh.OutputTime), int((Hour(TimeValue(oh.OutputTime))*60+Minute(TimeValue(oh.OutputTime)))*60)";
            }
        }

        public override String View_pvoutput5min_v_1500
        {
            get
            {
                return
                    "CREATE VIEW pvoutput5min_v AS " +
                    "SELECT pv.OutputDay, int(int(pv.OutputTime/300)*300) AS OutputTime, " +
                    "Sum(pv.Energy)*1000.0 AS Energy, Max(pv.Power)*1000 AS Power, " +
                    "Min(pv.Power)*1000 AS MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "FROM pvoutput_sub_v AS pv " +
                    "GROUP BY pv.OutputDay, int(int(pv.OutputTime/300)*300)";
            }
        }

        public override String View_pvoutput_v_1500
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_v AS " +
                    "SELECT pv.OutputDay, int(int(pv.OutputTime/600)*600) AS OutputTime, " +
                    "Sum(pv.Energy)*1000.0 AS Energy, Max(pv.Power)*1000 AS Power, " +
                    "Min(pv.Power)*1000 AS MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "FROM pvoutput_sub_v AS pv " +
                    "GROUP BY pv.OutputDay, int(int(pv.OutputTime/600)*600)";
            }
        }

        public override string Alter_inverter_1700
        {
            get
            {
                return
                    "alter table inverter " +
                    "add SiteId varchar(10) NULL ";
            }
        }

        public override String View_pvoutput_sub_v_1700
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_sub_v AS " +
                    "SELECT i.SiteId AS SiteId, DateValue(oh.OutputTime) AS OutputDay, " +
                    "int((Hour(TimeValue(oh.OutputTime))*60+Minute(TimeValue(oh.OutputTime)))*60) AS OutputTime, " +
                    "Sum(oh.OutputKwh) AS Energy, Sum(oh.OutputKwh*3600/oh.Duration) AS Power, " +
                    "MIN(MinPower) AS Min_Power, " +
                    "MAX(MaxPower) AS Max_Power " +
                    "FROM outputhistory AS oh, inverter i " +
                    "WHERE oh.Inverter_Id = i.Id " +
                    "AND oh.OutputKwh > 0 " +
                    "AND (((oh.OutputTime)>=(Date()-20))) " +
                    "GROUP BY i.SiteId, DateValue(oh.OutputTime), int((Hour(TimeValue(oh.OutputTime))*60+Minute(TimeValue(oh.OutputTime)))*60)";
            }
        }

        public override String View_pvoutput5min_v_1700
        {
            get
            {
                return
                    "CREATE VIEW pvoutput5min_v AS " +
                    "SELECT pv.SiteId AS SiteId, pv.OutputDay, int(int(pv.OutputTime/300)*300) AS OutputTime, " +
                    "Sum(pv.Energy)*1000.0 AS Energy, Max(pv.Power)*1000 AS Power, " +
                    "Min(pv.Power)*1000 AS MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "FROM pvoutput_sub_v AS pv " +
                    "GROUP BY pv.SiteId, pv.OutputDay, int(int(pv.OutputTime/300)*300)";
            }
        }

        public override String View_pvoutput_v_1700
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_v AS " +
                    "SELECT pv.SiteId AS SiteId, pv.OutputDay, int(int(pv.OutputTime/600)*600) AS OutputTime, " +
                    "Sum(pv.Energy)*1000.0 AS Energy, Max(pv.Power)*1000 AS Power, " +
                    "Min(pv.Power)*1000 AS MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "FROM pvoutput_sub_v AS pv " +
                    "GROUP BY pv.SiteId, pv.OutputDay, int(int(pv.OutputTime/600)*600)";
            }
        }

        public override String Alter_meterreading_dropPK_1700
        {
            get
            {
                return "ALTER TABLE `meterreading` DROP CONSTRAINT `meterreading_pk` ";
            }
        }

        public override String Alter_meterreading_createPK_1700
        {
            get
            {
                return "ALTER TABLE `meterreading` ADD  CONSTRAINT meterreading_pk PRIMARY KEY " +
                        "( Meter_Id, ReadingTime, Appliance ) ";
            }
        }

        public override string Alter_meter_1710
        {
            get
            {
                return
                    "alter table meter " +
                    "add InstanceNo INTEGER NULL ";
            }
        }

        public string Alter_meter_1710a
        {
            get
            {
                return
                    "alter table meter " +
                    "add NextDate DATETIME NULL ";
            }
        }

        public override string Alter_cmsdata_1830
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add VoltsPV2 FLOAT NULL ";
            }
        }

        public string Alter_cmsdata_1830a
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add PowerPV FLOAT NULL ";
            }
        }

        public string Alter_cmsdata_1830b
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add EstEnergy FLOAT NULL ";
            }
        }

        public string Alter_cmsdata_1830c
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add ErrorCode INTEGER NULL ";
            }
        }

        public override string Alter_outputhistory_1836
        {
            get
            {
                return
                    "alter table outputhistory " +
                    "add Temperature FLOAT NULL ";
            }
        }

        public override String View_pvoutput_sub_v_1836
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_sub_v AS " +
                    "SELECT i.SiteId AS SiteId, DateValue(oh.OutputTime) AS OutputDay, " +
                    "int((Hour(TimeValue(oh.OutputTime))*60+Minute(TimeValue(oh.OutputTime)))*60) AS OutputTime, " +
                    "Sum(oh.OutputKwh) AS Energy, Sum(oh.OutputKwh*3600/oh.Duration) AS Power, " +
                    "MIN(MinPower) AS Min_Power, " +
                    "MAX(MaxPower) AS Max_Power, " +
                    "MAX(oh.Temperature) AS Max_Temp " +
                    "FROM outputhistory AS oh, inverter i " +
                    "WHERE oh.Inverter_Id = i.Id " +
                    "AND oh.OutputKwh > 0 " +
                    "AND (((oh.OutputTime)>=(Date()-20))) " +
                    "GROUP BY i.SiteId, DateValue(oh.OutputTime), int((Hour(TimeValue(oh.OutputTime))*60+Minute(TimeValue(oh.OutputTime)))*60)";
            }
        }

        public override String View_pvoutput5min_v_1836
        {
            get
            {
                return
                    "CREATE VIEW pvoutput5min_v AS " +
                    "SELECT pv.SiteId AS SiteId, pv.OutputDay, int(int(pv.OutputTime/300)*300) AS OutputTime, " +
                    "Sum(pv.Energy)*1000.0 AS Energy, Max(pv.Power)*1000 AS Power, " +
                    "Min(pv.Power)*1000 AS MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower, " +
                    "MAX(Max_Temp) AS MaxTemp " +
                    "FROM pvoutput_sub_v AS pv " +
                    "GROUP BY pv.SiteId, pv.OutputDay, int(int(pv.OutputTime/300)*300)";
            }
        }

        public override String View_pvoutput_v_1836
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_v AS " +
                    "SELECT pv.SiteId AS SiteId, pv.OutputDay, int(int(pv.OutputTime/600)*600) AS OutputTime, " +
                    "Sum(pv.Energy)*1000.0 AS Energy, Max(pv.Power)*1000 AS Power, " +
                    "Min(pv.Power)*1000 AS MinPower, " +
                    "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                    "MIN(Min_Power) * 1000 AS RealMinPower, " +
                    "MAX(Max_Temp) AS MaxTemp " +
                    "FROM pvoutput_sub_v AS pv " +
                    "GROUP BY pv.SiteId, pv.OutputDay, int(int(pv.OutputTime/600)*600)";
            }
        }

        public override string Alter_cmsdata_1836
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add VoltsPV1 FLOAT NULL ";
            }
        }

        public string Alter_cmsdata_1836a
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add VoltsPV3 FLOAT NULL ";
            }
        }

        public string Alter_cmsdata_1836b
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add CurrentPV1 FLOAT NULL ";
            }
        }

        public string Alter_cmsdata_1836c
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add CurrentPV2 FLOAT NULL ";
            }
        }

        public string Alter_cmsdata_1836d
        {
            get
            {
                return
                    "alter table cmsdata " +
                    "add CurrentPV3 FLOAT NULL ";
            }
        }

        public override string Drop_inverter_index_1902
        {
            get
            {
                return "drop index InverterIdentity on inverter ";
            }
        }

        public override string Create_inverter_index_1902
        {
            get
            {
                return "create unique index InverterIdentity_UK on inverter (SerialNumber, InverterType_Id, InverterManager_Id) ";
            }
        }

        public override string Table_pvoutputlog_2000
        {
            get
            {
                return
                    "create table pvoutputlog " +
                    "( " +
                        "SiteId varchar(10) NOT NULL, " +
                        "OutputDay date NOT NULL, " +
                        "OutputTime integer NOT NULL, " +
                        "Energy double NULL, " +
                        "Power double NULL, " +
                        "Loaded smallint NOT NULL default 0, " +
                        "ImportEnergy double NULL, " +
                        "ImportPower double NULL, " +
                        "Temperature double NULL, " +
                        "constraint pvoutputlog_pk PRIMARY KEY ( SiteId, OutputDay, OutputTime ) " +
                     ")";
            }
        }

        public override string Table_devicetype_2000
        {
            get
            {
                return
                    "CREATE TABLE devicetype " +
                    "( " +
                        "Id AUTOINCREMENT, " +
                        "Manufacturer varchar(60) NOT NULL, " +
                        "Model varchar(50) NOT NULL, " +
                        "MaxPower INTEGER NULL, " +
                        "DeviceType varchar(20) NOT NULL, " +
                        "CONSTRAINT PK_DeviceType PRIMARY KEY (Id), " +
                        "CONSTRAINT DeviceType_UK1 UNIQUE (Manufacturer, Model) " +
                    ") ";
            }
        }

        public override string Table_device_2000
        {
            get
            {
                return
                    "CREATE TABLE device " +
                    "( " +
                        "Id AUTOINCREMENT, " +
                        "SerialNumber varchar(45) NOT NULL, " +
                        "DeviceType_Id INTEGER NOT NULL, " +
                        "CONSTRAINT PK_device PRIMARY KEY " +
                        "( " +
                            "Id " +
                        "), " +
                        "CONSTRAINT fk_device_devicetype FOREIGN KEY (DeviceType_Id) REFERENCES devicetype (Id), " +
                        "CONSTRAINT uk_device UNIQUE (SerialNumber, DeviceType_Id)  " +
                    ") ";
            }
        }

        public override string Table_devicefeature_2000
        {
            get
            {
                return
                    "CREATE TABLE devicefeature " +
                    "( " +
                        "Id AUTOINCREMENT, " +
                        "Device_Id INTEGER NOT NULL, " +
                        "FeatureType INTEGER NOT NULL, " +
                        "FeatureId INTEGER NOT NULL, " +
                        "MeasureType VARCHAR(20) NOT NULL, " +
                        "IsConsumption CHAR(1) NULL, " +
                        "IsAC CHAR(1) NULL, " +
                        "IsThreePhase CHAR(1) NULL, " +
                        "StringNumber INTEGER NULL, " +
                        "PhaseNumber INTEGER NULL, " +
                        "CONSTRAINT PK_devicefeature PRIMARY KEY " +
                        "( " +
                            "Id " +
                        "), " +
                        "CONSTRAINT uk_devicefeature UNIQUE (Device_Id, FeatureType, FeatureId),  " +
                        "CONSTRAINT fk_devicefeature_device FOREIGN KEY (Device_Id) REFERENCES device (Id) " +
                    ") ";
            }
        }

        public override string Table_devicereading_energy_2000
        {
            get
            {
                return
                    "CREATE TABLE devicereading_energy " +
                    "( " +
                        "ReadingEnd DATETIME NOT NULL, " +
                        "DeviceFeature_Id INTEGER NOT NULL, " +
                        "ReadingStart DATETIME NOT NULL, " +
                        "EnergyTotal DOUBLE NULL, " +
                        "EnergyToday DOUBLE NULL, " +
                        "EnergyDelta FLOAT NULL, " +
                        "CalcEnergyDelta FLOAT NULL, " +
                        "HistEnergyDelta FLOAT NULL, " +
                        "Mode INTEGER NULL, " +
                        "ErrorCode INTEGER NULL, " +
                        "Power INTEGER NULL, " +
                        "Volts FLOAT NULL, " +
                        "Amps FLOAT NULL, " +
                        "Frequency FLOAT NULL, " +
                        "Temperature FLOAT NULL, " +
                        "MinPower INTEGER NULL, " +
                        "MaxPower INTEGER NULL, " +                        
                        "CONSTRAINT PK_devicereading_energy_1 PRIMARY KEY (ReadingEnd, DeviceFeature_Id ), " +
                        "CONSTRAINT uk_devicereading_energy UNIQUE (DeviceFeature_Id, ReadingEnd), " +
                        "CONSTRAINT fk_devicereadingenergy_devicefeature FOREIGN KEY (DeviceFeature_Id) REFERENCES devicefeature (Id) " +
                    ") ";
            }
        }

        public override String View_devicedayoutput_v_2000
        {
            get
            {
                return
                    "CREATE VIEW devicedayoutput_v " +
                    "AS SELECT f.Device_Id, DateValue(r.ReadingEnd) as OutputDay, SUM(r.EnergyDelta) as OutputKwh " +
                    "from devicereading_energy r, devicefeature f " +
                    "where r.DeviceFeature_Id = f.Id " +
                    "group by f.Device_Id, DateValue(r.ReadingEnd) ";
            }
        }
    }

    internal class SQLServer_DDL : DDL
    {
        public override String Table_version
        {
            get
            {
                return
                    "create table version " +
                    "( " +
                        "Major [varchar](4) NOT NULL, " +
                        "Minor [varchar](4) NOT NULL, " +
                        "Release [varchar](4) NOT NULL, " +
                        "Patch [varchar](4) NOT NULL " +
                     ") ";
            }
        }

        // required for 1.4.2.1 and later
        public override string Alter_pvoutputlog_1421
        {
            get
            {
                return
                    "alter table pvoutputlog " +
                    "add Temperature float NULL ";
            }
        }

        public override String Table_cmsdata_1500
        { 
            get 
            { 
                return 
                    "CREATE TABLE cmsdata " +
                    "( " +
	                    "Inverter_Id [int] NOT NULL, " +
	                    "OutputTime [datetime] NOT NULL, " +
	                    "EnergyTotal [float] NOT NULL, " +
	                    "EnergyToday [float] NULL, " +
	                    "Temperature [real] NULL, " +
	                    "VoltsPV [real] NULL, " +
	                    "CurrentAC [real] NULL, " +
	                    "VoltsAC [real] NULL, " +
	                    "FrequencyAC [real] NULL, " +
	                    "PowerAC [real] NULL, " +
	                    "ImpedanceAC [real] NULL, " +
	                    "Hours [int] NULL, " +
	                    "Mode [int] NULL, " +
                        "CONSTRAINT PK_cmsdata PRIMARY KEY CLUSTERED " +
                        "( " +
	                        "Inverter_Id ASC, " +
	                        "OutputTime ASC " +
                        ") " +
                        "WITH " +
                        "( " +
                            "PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, " +
                            "ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON " +
                        ") " +
                    ") "; 
            } 
        }

        public override String Alter_outputhistory_1500 
        {
            get
            {
                return 
                    "Alter Table outputhistory ADD " +
                        "MinPower real null, " +
                        "MaxPower real null ";
            }
        }

        public override String View_pvoutput_sub_v_1500
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_sub_v AS " +
                    "SELECT CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME) AS OutputDay, " +
                        "DATEDIFF(second, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), OutputTime) AS OutputTime, " +
                        "SUM(OutputKwh) AS Energy, " +
                        "SUM(OutputKwh * 3600 / Duration) AS Power, " +
                        "MIN(MinPower) AS Min_Power, " +
                        "MAX(MaxPower) AS Max_Power " +
                    "FROM pvhistory.outputhistory AS oh " +
                    "WHERE (OutputTime >= GETDATE() - 20) " +
                    "AND (OutputKwh > 0) " +
                    "GROUP BY CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), " +
                        "DATEDIFF(second, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), OutputTime)";
            }
        }

        public override String View_pvoutput5min_v_1500
        {
            get
            {
                return
                    "CREATE VIEW pvoutput5min_v AS " +
                    "SELECT OutputDay, FLOOR(OutputTime / 300) * 300 AS OutputTime, " +
                        "SUM(Energy) * 1000 AS Energy, " +
                        "MAX(Power) * 1000 AS Power, " +
                        "MIN(Power) * 1000 AS MinPower, " +
                        "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                        "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "FROM pvhistory.pvoutput_sub_v " +
                    "GROUP BY OutputDay, FLOOR(OutputTime / 300) * 300 ";
            }
        }

        public override String View_pvoutput_v_1500
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_v AS " +
                    "SELECT OutputDay, FLOOR(OutputTime / 600) * 600 AS OutputTime, " +
                        "SUM(Energy) * 1000 AS Energy, " +
                        "MAX(Power) * 1000 AS Power, " +
                        "MIN(Power) * 1000 AS MinPower, " +
                        "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                        "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "FROM pvhistory.pvoutput_sub_v " +
                    "GROUP BY OutputDay, FLOOR(OutputTime / 600) * 600 ";
            }
        }

        public String Alter_cmsdata_1502
        {
            get
            {
                return "ALTER TABLE cmsdata " +
                            "ADD CONSTRAINT cmsdata_inverter_fk " +
                            "FOREIGN KEY ( Inverter_Id ) " +
                            "REFERENCES inverter (Id) " +
                            "ON DELETE NO ACTION ";
            }
        }

        public override string Alter_inverter_1700
        {
            get
            {
                return
                    "alter table inverter " +
                    "add SiteId varchar(10) NULL ";
            }
        }

        public override String View_pvoutput_sub_v_1700
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_sub_v AS " +
                    "SELECT i.SiteId AS SiteId, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME) AS OutputDay, " +
                        "DATEDIFF(second, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), OutputTime) AS OutputTime, " +
                        "SUM(OutputKwh) AS Energy, " +
                        "SUM(OutputKwh * 3600 / Duration) AS Power, " +
                        "MIN(MinPower) AS Min_Power, " +
                        "MAX(MaxPower) AS Max_Power " +
                    "FROM outputhistory AS oh INNER JOIN inverter AS i ON oh.Inverter_Id = i.Id " +
                    "WHERE (OutputTime >= GETDATE() - 20) " +
                    "AND (OutputKwh > 0) " +
                    "GROUP BY i.SiteId, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), " +
                        "DATEDIFF(second, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), OutputTime)";
            }
        }

        public override String View_pvoutput5min_v_1700
        {
            get
            {
                return
                    "CREATE VIEW pvoutput5min_v AS " +
                    "SELECT SiteId, OutputDay, FLOOR(OutputTime / 300) * 300 AS OutputTime, " +
                        "SUM(Energy) * 1000 AS Energy, " +
                        "MAX(Power) * 1000 AS Power, " +
                        "MIN(Power) * 1000 AS MinPower, " +
                        "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                        "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "FROM pvoutput_sub_v " +
                    "GROUP BY SiteId, OutputDay, FLOOR(OutputTime / 300) * 300 ";
            }
        }

        public override String View_pvoutput_v_1700
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_v AS " +
                    "SELECT SiteId, OutputDay, FLOOR(OutputTime / 600) * 600 AS OutputTime, " +
                        "SUM(Energy) * 1000 AS Energy, " +
                        "MAX(Power) * 1000 AS Power, " +
                        "MIN(Power) * 1000 AS MinPower, " +
                        "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                        "MIN(Min_Power) * 1000 AS RealMinPower " +
                    "FROM pvoutput_sub_v " +
                    "GROUP BY SiteId, OutputDay, FLOOR(OutputTime / 600) * 600 ";
            }
        }

        public override String Alter_meterreading_dropPK_1700
        {
            get
            {
                return "ALTER TABLE meterreading DROP CONSTRAINT MeterReading_PK ";
            }
        }

        public override String Alter_meterreading_createPK_1700
        {
            get
            {
                return "ALTER TABLE meterreading ADD  CONSTRAINT MeterReading_PK PRIMARY KEY CLUSTERED " +
                        "( Meter_Id ASC, ReadingTime ASC, Appliance ASC ) " +
                        "WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, " +
                        "ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ";
            }
        }

        public override string Alter_meter_1710
        {
            get
            {
                return
                    "alter table meter add " +
                    "InstanceNo [int] NULL, " +
                    "NextDate [datetime] NULL ";
            }
        }

        public override string Alter_cmsdata_1830
        {
            get
            {
                return
                    "alter table cmsdata add " +
                    "VoltsPV2 [real] NULL, " +
                    "PowerPV [float] NULL, " +
                    "EstEnergy [float] NULL, " +
                    "ErrorCode [int] NULL ";
            }
        }

        public override String View_pvoutput_sub_v_1836
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_sub_v AS " +
                    "SELECT i.SiteId AS SiteId, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME) AS OutputDay, " +
                        "DATEDIFF(second, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), OutputTime) AS OutputTime, " +
                        "SUM(OutputKwh) AS Energy, " +
                        "SUM(OutputKwh * 3600 / Duration) AS Power, " +
                        "MIN(MinPower) AS Min_Power, " +
                        "MAX(MaxPower) AS Max_Power, " +
                        "MAX(oh.Temperature) AS Max_Temp " +
                    "FROM outputhistory AS oh INNER JOIN inverter AS i ON oh.Inverter_Id = i.Id " +
                    "WHERE (OutputTime >= GETDATE() - 20) " +
                    "AND (OutputKwh > 0) " +
                    "GROUP BY i.SiteId, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), " +
                        "DATEDIFF(second, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), OutputTime)";
            }
        }

        public override String View_pvoutput5min_v_1836
        {
            get
            {
                return
                    "CREATE VIEW pvoutput5min_v AS " +
                    "SELECT SiteId, OutputDay, FLOOR(OutputTime / 300) * 300 AS OutputTime, " +
                        "SUM(Energy) * 1000 AS Energy, " +
                        "MAX(Power) * 1000 AS Power, " +
                        "MIN(Power) * 1000 AS MinPower, " +
                        "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                        "MIN(Min_Power) * 1000 AS RealMinPower, " +
                        "MAX(Max_Temp) AS MaxTemp " +
                    "FROM pvoutput_sub_v " +
                    "GROUP BY SiteId, OutputDay, FLOOR(OutputTime / 300) * 300 ";
            }
        }

        public override String View_pvoutput_v_1836
        {
            get
            {
                return
                    "CREATE VIEW pvoutput_v AS " +
                    "SELECT SiteId, OutputDay, FLOOR(OutputTime / 600) * 600 AS OutputTime, " +
                        "SUM(Energy) * 1000 AS Energy, " +
                        "MAX(Power) * 1000 AS Power, " +
                        "MIN(Power) * 1000 AS MinPower, " +
                        "MAX(Max_Power) * 1000 AS RealMaxPower, " +
                        "MIN(Min_Power) * 1000 AS RealMinPower, " +
                        "MAX(Max_Temp) AS MaxTemp " +
                    "FROM pvoutput_sub_v " +
                    "GROUP BY SiteId, OutputDay, FLOOR(OutputTime / 600) * 600 ";
            }
        }

        public override string Alter_cmsdata_1836
        {
            get
            {
                return
                    "alter table cmsdata add " +
                    "VoltsPV1 [real] NULL, " +
                    "VoltsPV3 [real] NULL, " +
                    "CurrentPV1 [real] NULL, " +
                    "CurrentPV2 [real] NULL, " +
                    "CurrentPV3 [real] NULL ";
            }
        }

        public override string Alter_outputhistory_1836
        {
            get
            {
                return
                    "alter table outputhistory add " +
                    "Temperature [real] NULL ";
            }
        }

        public override string Drop_inverter_index_1902
        {
            get
            {
                return "alter table inverter drop constraint InverterIdentity_UK ";
            }
        }

        public override string Create_inverter_index_1902
        {
            get
            {
                return "alter table inverter add constraint InverterIdentity_UK UNIQUE (SerialNumber, InverterType_Id, InverterManager_Id) ";
            }
        }

        public override string Table_pvoutputlog_2000
        {
            get
            {
                return
                    "create table pvoutputlog " +
                    "( " +
                        "SiteId [varchar](10) NOT NULL, " +
                        "OutputDay [date] NOT NULL, " +
                        "OutputTime [int] NOT NULL, " +
                        "Loaded int NOT NULL DEFAULT 0, " +
                        "ImportEnergy [float] NULL, " +
                        "ImportPower [float] NULL, " +
                        "Temperature [float] NULL, " +
                        "CONSTRAINT PK_pvoutputlog PRIMARY KEY CLUSTERED ([SiteId] ASC, [OutputDay] ASC,[OutputTime] ASC) " +
                     ") ";
            }
        }

        public override string Table_pvoutputlog
        {
            get
            {
                return Table_pvoutputlog_2000;
            }
        }

        public override string Table_devicetype_2000
        {
            get
            {
                return 
                    "CREATE TABLE devicetype " +
                    "( " +
                        "Id [int] IDENTITY(1,1) NOT NULL, " +
                        "Manufacturer [varchar](60) NOT NULL, " +
                        "Model [varchar](50) NOT NULL, " +
                        "MaxPower [int] NULL, " +
                        "DeviceType [varchar](20) NOT NULL, " +
                        "CONSTRAINT PK_DeviceType PRIMARY KEY CLUSTERED ([Id] ASC ) " +
                        "WITH " +
                        "( " +
                            "PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, " +
                            "ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON " +
                        "), " +
                        "CONSTRAINT DeviceType_UK1 UNIQUE (Manufacturer, Model)  " +
                        "WITH " +
                        "( " +
                            "PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, " +
                            "ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON " +
                        ") " +                        
                    ") ";
            }
        }

        public override string Table_device_2000
        {
            get
            {
                return 
                    "CREATE TABLE device " +
                    "( " +
	                    "Id [int] IDENTITY(1,1) NOT NULL, " +
	                    "SerialNumber [varchar](45) NOT NULL, " +
	                    "DeviceType_Id [int] NOT NULL, " +
                        "CONSTRAINT PK_device PRIMARY KEY CLUSTERED " +
                        "( " +
	                        "Id ASC " +
                        ") " +
                        "WITH " +
                        "( " +
                            "PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, " +
                            "ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON " +
                        "), " +
                        "CONSTRAINT fk_device_devicetype FOREIGN KEY (DeviceType_Id) REFERENCES devicetype (Id), " +
                        "CONSTRAINT uk_device UNIQUE (SerialNumber, DeviceType_Id)  " +
                        "WITH " +
                        "( " +
                            "PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, " +
                            "ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON " +
                        ") " +
                    ") "; 
            }
        }

        public override string Table_devicefeature_2000
        {
            get
            {
                return
                    "CREATE TABLE devicefeature " +
                    "( " +
                        "Id [int] IDENTITY(1,1) NOT NULL, " +
                        "Device_Id [int] NOT NULL, " +
                        "FeatureType [smallint] NOT NULL, " +
                        "FeatureId [tinyint] NOT NULL, " +
                        "MeasureType [varchar](20) NOT NULL, " +
                        "IsConsumption [char](1) NULL, " +
                        "IsAC [char](1) NULL, " +
                        "IsThreePhase [char](1) NULL, " +
                        "StringNumber [int] NULL, " +
                        "PhaseNumber [int] NULL, " +
                        "CONSTRAINT PK_devicefeature PRIMARY KEY CLUSTERED " +
                        "( " +
                            "Id ASC " +
                        ") " +
                        "WITH " +
                        "( " +
                            "PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, " +
                            "ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON " +
                        "), " +
                        "CONSTRAINT uk_devicefeature UNIQUE (Device_Id, FeatureType, FeatureId), " +
                        "CONSTRAINT FK_devicefeature_device FOREIGN KEY(Device_Id) REFERENCES device (Id) " +
                    ") ";
            }
        }

        public override string Table_devicereading_energy_2000
        {
            get
            {
                return
                    "CREATE TABLE devicereading_energy " +
                    "( " +
                        "ReadingEnd DATETIME NOT NULL, " +
                        "DeviceFeature_Id INT NOT NULL, " +
                        "ReadingStart DATETIME NOT NULL, " +
                        "EnergyTotal FLOAT NULL, " +
                        "EnergyToday FLOAT NULL, " +
                        "EnergyDelta REAL NULL, " +
                        "CalcEnergyDelta REAL NULL, " +
                        "HistEnergyDelta REAL NULL, " +
                        "Mode INT NULL, " +
                        "ErrorCode INT NULL, " +
                        "Power INT NULL, " +
                        "Volts REAL NULL, " +
                        "Amps REAL NULL, " +
                        "Frequency REAL NULL, " +
                        "Temperature REAL NULL, " +
                        "MinPower INT NULL, " +
                        "MaxPower INT NULL, " +                        
                        "CONSTRAINT [PK_devicereading_energy_1] PRIMARY KEY CLUSTERED (ReadingEnd, DeviceFeature_Id ) " +
                        "WITH " +
                        "( " +
                            "PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, " +
                            "ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON " +
                        "), " +
                        "CONSTRAINT uk_devicereading_energy UNIQUE (DeviceFeature_Id, ReadingEnd)  " +
                        "WITH " +
                        "( " +
                            "PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, " +
                            "ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON " +
                        "), " +
                        "CONSTRAINT fk_devicereading_energy_devicefeature FOREIGN KEY (DeviceFeature_Id) REFERENCES devicefeature (Id) " +
                    ") ";
            }
        }

        public override String View_devicedayoutput_v_2000
        {
            get
            {
                return
                    "CREATE VIEW devicedayoutput_v " +
                    "AS SELECT f.Device_Id, CAST(FLOOR(CAST(r.ReadingEnd AS float)) AS DATETIME) OutputDay, SUM(r.EnergyDelta) OutputKwh " +
                    "from devicereading_energy r, devicefeature f " +
                    "where r.DeviceFeature_Id = f.Id " +
                    "group by f.Device_Id, CAST(FLOOR(CAST(r.ReadingEnd AS float)) AS DATETIME) ";
            }
        }
    }

    public class VersionManager
    {
        private DBVersion Version;

        private bool CopyToTable(String fromRelation, String toTable, List<String> columns, GenConnection con)
        {
            if (columns == null || columns.Count == 0)
            {
                GlobalSettings.LogMessage("VersionManager", "CopyToTable - Empty column list", LogEntryType.ErrorMessage);
                return false;
            }

            String columnList = "";

            foreach (String column in columns)
            {
                if (columnList == "")
                    columnList = column;
                else
                    columnList += ", " + column;
            }



            String fromSelect = "select " + columnList + " from " + fromRelation;
            String toInsert = "insert into " + toTable + " ( " + columnList + " ) " + fromSelect;

            GenCommand cmd = null;

            bool ret = true;

            try
            {
                cmd = new GenCommand(toInsert, con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                ret = false;
                GlobalSettings.LogMessage("VersionManager", "CopyToTable - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }

            return ret;
        }

        private bool Update_pvoutput_v(DDL ddl, GenConnection con)
        {
            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch(Exception)
            {
                //utilityLog.LogMessage("VersionManager", "Update_pvoutput_v: exception dropping pvoutput_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput5min_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                //utilityLog.LogMessage("VersionManager", "Update_pvoutput_v: exception dropping pvoutput5min_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput_sub_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                //utilityLog.LogMessage("VersionManager", "Update_pvoutput_v: exception dropping pvoutput_sub_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                if (CreateRelation(ddl.View_pvoutput_sub_v, con))
                    if (CreateRelation(ddl.View_pvoutput_v, con))
                        if (CreateRelation(ddl.View_pvoutput5min_v, con))
                            return true;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("VersionManager", "Update_pvoutput_v: exception creating view: " + e.Message, LogEntryType.ErrorMessage);
            }
            
            return false;
        }

        private bool Update_pvoutput_v_1500(DDL ddl, GenConnection con)
        {
            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                //utilityLog.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception dropping pvoutput_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput5min_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                //utilityLog.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception dropping pvoutput5min_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput_sub_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                //utilityLog.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception dropping pvoutput_sub_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                if (CreateRelation(ddl.View_pvoutput_sub_v_1500, con))
                    if (CreateRelation(ddl.View_pvoutput_v_1500, con))
                        if (CreateRelation(ddl.View_pvoutput5min_v_1500, con))
                            return true;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception creating view: " + e.Message, LogEntryType.ErrorMessage);
            }

            return false;
        }

        private bool Update_pvoutput_v_1700(DDL ddl, GenConnection con)
        {
            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                //utilityLog.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception dropping pvoutput_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput5min_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                //utilityLog.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception dropping pvoutput5min_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput_sub_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                //utilityLog.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception dropping pvoutput_sub_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                if (CreateRelation(ddl.View_pvoutput_sub_v_1700, con))
                    if (CreateRelation(ddl.View_pvoutput_v_1700, con))
                        if (CreateRelation(ddl.View_pvoutput5min_v_1700, con))
                            return true;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("VersionManager", "Update_pvoutput_v_1700: exception creating view: " + e.Message, LogEntryType.ErrorMessage);
            }

            return false;
        }

        private bool Update_meterreading_1700(String databaseType, DDL ddl, GenConnection con)
        {
            bool ok = true;
            if (databaseType == "SQLite")
            {
                try
                {
                    GenCommand cmd = new GenCommand("alter table meterreading rename to meterreading_old ", con);
                    int res = cmd.ExecuteNonQuery();
                    ok = res >= 0;
                }
                catch (Exception e)
                {
                    ok = false;
                    GlobalSettings.LogMessage("VersionManager", "Update_meterreading_1700: exception renaming existing: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (!ok)
                    return false;

                if (!CreateRelation(((SQLite_DDL)ddl).Table_meterreading_1700, con))
                    return false;

                List<String> columns = new List<String>();
                columns.Add("Meter_Id");
                columns.Add("ReadingTime");
                columns.Add("Appliance");
                columns.Add("Duration");
                columns.Add("Energy");
                columns.Add("Temperature");
                columns.Add("Calculated");
                columns.Add("MinPower");
                columns.Add("MaxPower");

                if (!CopyToTable("meterreading_old", "meterreading", columns, con))
                    return false;

                try
                {
                    GenCommand cmd = new GenCommand("drop table meterreading_old ", con);
                    int res = cmd.ExecuteNonQuery();
                    return true;
                }
                catch (Exception e)
                {
                    GlobalSettings.LogMessage("VersionManager", "Update_meterreading_1700: exception dropping old: " + e.Message, LogEntryType.ErrorMessage);
                }
            }
            else
            {
                if (!AlterRelation(ddl.Alter_meterreading_dropPK_1700, con))
                    return false;

                if (!AlterRelation(ddl.Alter_meterreading_createPK_1700, con))
                    return false;
                return true;
            }

            return false;
        }

        private bool Update_pvoutput_v_1836(DDL ddl, GenConnection con)
        {
            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception dropping pvoutput_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput5min_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception dropping pvoutput5min_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                GenCommand cmd = new GenCommand("drop view pvoutput_sub_v", con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("VersionManager", "Update_pvoutput_v_1500: exception dropping pvoutput_sub_v: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                if (CreateRelation(ddl.View_pvoutput_sub_v_1836, con))
                    if (CreateRelation(ddl.View_pvoutput_v_1836, con))
                        if (CreateRelation(ddl.View_pvoutput5min_v_1836, con))
                            return true;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("VersionManager", "Update_pvoutput_v_1836: exception creating views: " + e.Message, LogEntryType.ErrorMessage);
            }

            return false;
        }

        private bool CreateRelation(String tableCommand, GenConnection con)
        {
            try
            {
                GenCommand cmd = new GenCommand(tableCommand, con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("CreateRelation", "Error creating relation " + tableCommand + " - error: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
            return true;
        }

        private bool AlterRelation(String tableCommand, GenConnection con)
        {
            try
            {
                GenCommand cmd = new GenCommand(tableCommand, con);
                int res = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("AlterRelation", "Error altering relation " + tableCommand + " - error: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
            return true;
        }

        private bool SetMeterInstanceNo(GenConnection con)
        {
            try
            {
                GenCommand cmd = new GenCommand("update meter set InstanceNo = 1 ", con);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("SetMeterInstanceNo", "Error updating meter table - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
        }

        private void UpdateVersion(String major, String minor, String release, String patch, GenConnection con)
        {
            try
            {
                GenCommand cmd = new GenCommand("delete from version", con);
                cmd.ExecuteNonQuery();
            }
            catch 
            {
            }

            String cmdStr;

            if (con.DBType == GenDBType.SQLServer)
                cmdStr =
                "insert into version (Major, Minor, Release, Patch) values (@Major, @Minor, @Release, @Patch)";
            else
                cmdStr =
                "insert into version (Major, Minor, `Release`, Patch) values (@Major, @Minor, @Release, @Patch)";

            try
            {
                GenCommand cmd = new GenCommand(cmdStr, con);
                cmd.AddParameterWithValue("@Major", major);
                cmd.AddParameterWithValue("@Minor", minor);
                cmd.AddParameterWithValue("@Release", release);
                cmd.AddParameterWithValue("@Patch", patch);
                cmd.ExecuteNonQuery();
                Version.major = major;
                Version.minor = minor;
                Version.release = release;
                Version.patch = patch;
            }
            catch (Exception e)
            {
                throw new Exception("UpdateVersion: error updating version table: " + e.Message, e);
            }
        }

        public bool GetCurrentVersion(GenConnection con, String databaseType, out DBVersion version)
        {
            GenCommand cmd;
            if (databaseType == "SQL Server")
                cmd = new GenCommand("Select Major, Minor, [Release], Patch from [version]", con);
            else
                cmd = new GenCommand("Select Major, Minor, `Release`, Patch from version", con);

            version.major = "";
            version.minor = "";
            version.release = "";
            version.patch = "";

            try
            {
                GenDataReader dr;

                dr = (GenDataReader)cmd.ExecuteReader();
                if (dr.HasRows)
                {
                    dr.Read();
                    version.major = dr.GetString("Major");
                    version.minor = dr.GetString("Minor");
                    version.release = dr.GetString("Release");
                    version.patch = dr.GetString("Patch");

                    dr.Close();
                    return true;
                }
                else
                {
                    dr.Close();
                    return false;
                }
            }
            catch (Exception e)
            {
                if (GlobalSettings.SystemServices != null) // true when service is running
                    GlobalSettings.LogMessage("GetCurrentVersion", "Exception: " + e.Message);
                return false;
            }
        }

        public bool UpdateTo_1_3_5_4(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion( "1", "3", "5", "4");
            if (res >= 0)
                return false;
            
            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_3_5_4", "Updating database structure to suit version 1.3.5.4", LogEntryType.Information);

            if (res < 0)
            {
                CreateRelation(DDL.Table_version, con);
                CreateRelation(DDL.Table_pvoutputlog, con);
            }

            if (Update_pvoutput_v(DDL, con))
            {
                UpdateVersion("1", "3", "5", "4", con);
                return true;
            }
            else
                return false;
        }

        public bool UpdateTo_1_3_6_0(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;
            
            res = Version.CompareVersion("1", "3", "6", "0");
            if (res >= 0)
                return false;            

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_3_6_0", "Updating database structure to suit version 1.3.6.0", LogEntryType.Information);

            bool success = true;

            if (res < 0)
            {
                success &= AlterRelation(DDL.Alter_pvoutputlog_1400a, con);
                if (databaseType == "Jet")
                    success &= AlterRelation(((Jet_DDL)DDL).Alter_pvoutputlog_1400a2, con);

                if (databaseType == "SQLite")
                    success &= AlterRelation(((SQLite_DDL)DDL).Alter_pvoutputlog_1400b, con);

                success &= CreateRelation(DDL.Table_meter, con);
                success &= CreateRelation(DDL.Table_meterreading, con);
            }

            // the tables created here are now 1.4.0.0 compliant
            if (success)
                UpdateVersion("1", "4", "0", "0", con);

            return success;
        }

        public bool UpdateTo_1_4_0_0(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;
            
            res = Version.CompareVersion("1", "4", "0", "0");
            if (res >= 0)
                return false;           

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_4_0_0", "Updating database structure to suit version 1.4.0.1", LogEntryType.Information);

            bool success = true;

            if (res < 0)
            {
                if (databaseType == "SQLite")
                {
                    success = AlterRelation(((SQLite_DDL)DDL).Drop_meterreading, con);
                    success &= CreateRelation(DDL.Table_meterreading, con);
                }
                else
                {
                    success = AlterRelation(DDL.Alter_meterreading_1400, con);
                    success &= AlterRelation(DDL.Alter_meterreading_1401a, con);
                    success &= AlterRelation(DDL.Alter_meterreading_1401b, con);
                }

                success &= CreateRelation(DDL.Table_meterhistory, con);
            }

            if (success)
                UpdateVersion("1", "4", "0", "1", con);

            return success;
        }

        public bool UpdateTo_1_4_0_1(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "4", "0", "1");
                if (res >= 0)
                    return false;
            
            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_4_0_1", "Updating database structure to suit version 1.4.0.1", LogEntryType.Information);

            bool success = true;

            if (res < 0)
            {
                if (databaseType == "SQLite")
                {
                    success = AlterRelation(((SQLite_DDL)DDL).Drop_meterreading, con);
                    success &= CreateRelation(DDL.Table_meterreading, con);
                }
                else
                {
                    AlterRelation(DDL.Alter_meterreading_1401a, con);
                    AlterRelation(DDL.Alter_meterreading_1401b, con);
                    success = true;
                }

                success &= CreateRelation(DDL.Table_meterhistory, con);
            }

            if (success)
                UpdateVersion("1", "4", "0", "1", con);

            return success;
        }

        public bool UpdateTo_1_4_0_3(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "4", "0", "3");
                if (res >= 0)
                    return false;
            
            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_4_0_3", "Updating database structure to suit version 1.4.0.3", LogEntryType.Information);

            bool success = true;

            if (res < 0)
            {
                if (databaseType == "SQLite")
                {
                    success = AlterRelation("drop table `pvoutputlog` ", con);
                    success &= CreateRelation(DDL.Table_pvoutputlog, con);
                }
                else
                {
                    success = AlterRelation(DDL.Alter_pvoutputlog_1400b, con);

                }
            }

            if (success)
                UpdateVersion("1", "4", "0", "3", con);

            return success;
        }

        public bool UpdateTo_1_4_1_9(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "4", "1", "9");
                if (res >= 0)
                    return false;
            
            bool success = true;

            if (databaseType == "Jet")
            {
                DDL = new Jet_DDL();

                GlobalSettings.LogMessage("UpdateTo_1_4_1_9", "Updating database structure to suit version 1.4.1.9", LogEntryType.Information);

                AlterRelation(DDL.Alter_pvoutputlog_1400b, con);
                AlterRelation(((Jet_DDL)DDL).Alter_pvoutputlog_1400b2, con);   
            }

            if (success)
                UpdateVersion("1", "4", "1", "9", con);

            return success;
        }

        public bool UpdateTo_1_4_2_1(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "4", "2", "1");
            if (res >= 0)
                return false;
            
            bool success = true;

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else if (databaseType == "SQL Server")
                DDL = new SQLServer_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_4_2_1", "Updating database structure to suit version 1.4.2.1", LogEntryType.Information);

            success = AlterRelation(DDL.Alter_pvoutputlog_1421, con);
            
            if (success)
                UpdateVersion("1", "4", "2", "1", con);

            return success;
        }

        public bool UpdateTo_1_5_0_0(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "5", "0", "0");
            if (res >= 0)
                return false;
            
            bool success = true;

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else if (databaseType == "SQL Server")
                DDL = new SQLServer_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_5_0_0", "Updating database structure to suit version 1.5.0.0", LogEntryType.Information);

            success = CreateRelation(DDL.Table_cmsdata_1500, con);
            success &= AlterRelation(DDL.Alter_outputhistory_1500, con);
            if (databaseType == "SQLite")
                success &= AlterRelation(((SQLite_DDL)DDL).Alter_outputhistory_1500b, con);
            if (databaseType == "Jet")
                success &= AlterRelation(((Jet_DDL)DDL).Alter_outputhistory_1500b, con);
            success &= Update_pvoutput_v_1500(DDL, con);

            if (success)
                UpdateVersion("1", "5", "0", "0", con);

            return success;
        }

        public bool UpdateTo_1_5_0_2(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "5", "0", "2");
            if (res >= 0)
                return false;
            
            bool success = true;

            if (databaseType == "SQL Server")
            {
                DDL = new SQLServer_DDL();


                GlobalSettings.LogMessage("UpdateTo_1_5_0_2", "Updating database structure to suit version 1.5.0.2", LogEntryType.Information);

                success &= AlterRelation(((SQLServer_DDL)DDL).Alter_cmsdata_1502, con);
            }
            
            if (success)
                UpdateVersion("1", "5", "0", "2", con);

            return success;
        }

        public bool UpdateTo_1_7_0_0(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "7", "0", "0");
            if (res >= 0)
                return false;
            
            bool success = true;

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else if (databaseType == "SQL Server")
                DDL = new SQLServer_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_7_0_0", "Updating database structure to suit version 1.7.0.0", LogEntryType.Information);

            success = AlterRelation(DDL.Alter_inverter_1700, con);

            success &= Update_pvoutput_v_1700(DDL, con);

            success &= Update_meterreading_1700(databaseType, DDL, con);

            if (success)
                UpdateVersion("1", "7", "0", "0", con);

            return success;
        }

        public bool UpdateTo_1_7_1_0(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "7", "1", "0");
                if (res >= 0)
                    return false;
            
            bool success = true;

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else if (databaseType == "SQL Server")
                DDL = new SQLServer_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_7_1_0", "Updating database structure to suit version 1.7.1.0", LogEntryType.Information);

            success = AlterRelation(DDL.Alter_meter_1710, con);
            if (databaseType == "SQLite")
                success &= AlterRelation(((SQLite_DDL)DDL).Alter_meter_1710a, con);
            if (databaseType == "Jet")
                success &= AlterRelation(((Jet_DDL)DDL).Alter_meter_1710a, con);

            success &= SetMeterInstanceNo(con);

            if (success)
                UpdateVersion("1", "7", "1", "0", con);

            return success;
        }

        public bool UpdateTo_1_8_3_0(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "8", "3", "0");
            if (res >= 0)
                return false;
            
            bool success = true;

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else if (databaseType == "SQL Server")
                DDL = new SQLServer_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_8_3_0", "Updating database structure to suit version 1.8.3.0", LogEntryType.Information);


            success = AlterRelation(DDL.Alter_cmsdata_1830, con);
            if (databaseType == "SQLite")
            {
                success &= AlterRelation(((SQLite_DDL)DDL).Alter_cmsdata_1830a, con);
                success &= AlterRelation(((SQLite_DDL)DDL).Alter_cmsdata_1830b, con);
                success &= AlterRelation(((SQLite_DDL)DDL).Alter_cmsdata_1830c, con);
            }
            if (databaseType == "Jet")
            {
                success &= AlterRelation(((Jet_DDL)DDL).Alter_cmsdata_1830a, con);
                success &= AlterRelation(((Jet_DDL)DDL).Alter_cmsdata_1830b, con);
                success &= AlterRelation(((Jet_DDL)DDL).Alter_cmsdata_1830c, con);
            }

            if (success)
                UpdateVersion("1", "8", "3", "0", con);

            return success;
        }

        public bool UpdateTo_1_8_3_6(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "8", "3", "6");
            if (res >= 0)
                return false;
            
            bool success = true;

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else if (databaseType == "SQL Server")
                DDL = new SQLServer_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_8_3_6", "Updating database structure to suit version 1.8.3.6", LogEntryType.Information);

            success = AlterRelation(DDL.Alter_cmsdata_1836, con);
            success &= AlterRelation(DDL.Alter_outputhistory_1836, con);
            if (databaseType == "SQLite")
            {
                success &= AlterRelation(((SQLite_DDL)DDL).Alter_cmsdata_1836a, con);
                success &= AlterRelation(((SQLite_DDL)DDL).Alter_cmsdata_1836b, con);
                success &= AlterRelation(((SQLite_DDL)DDL).Alter_cmsdata_1836c, con);
                success &= AlterRelation(((SQLite_DDL)DDL).Alter_cmsdata_1836d, con);
            }
            if (databaseType == "Jet")
            {
                success &= AlterRelation(((Jet_DDL)DDL).Alter_cmsdata_1836a, con);
                success &= AlterRelation(((Jet_DDL)DDL).Alter_cmsdata_1836b, con);
                success &= AlterRelation(((Jet_DDL)DDL).Alter_cmsdata_1836c, con);
                success &= AlterRelation(((Jet_DDL)DDL).Alter_cmsdata_1836d, con);
            }

            success &= Update_pvoutput_v_1836(DDL, con);

            if (success)
                UpdateVersion("1", "8", "3", "6", con);

            return success;
        }

        public bool UpdateTo_1_9_0_2(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("1", "9", "0", "2");
            if (res >= 0)
                return false;
            
            bool success = true;

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else if (databaseType == "SQL Server")
                DDL = new SQLServer_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_1_9_0_2", "Updating database structure to suit version 1.9.0.2", LogEntryType.Information);

            success = AlterRelation(DDL.Drop_inverter_index_1902, con);
            success &= AlterRelation(DDL.Create_inverter_index_1902, con);
            
            if (success)
                UpdateVersion("1", "9", "0", "2", con);

            return success;
        }

        public bool UpdateTo_2_0_0_0(GenConnection con, String databaseType)
        {
            DDL DDL;
            int res;

            res = Version.CompareVersion("2", "0", "0", "0");
            if (res >= 0)
                return false;
            
            bool success = true;

            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else if (databaseType == "SQL Server")
                DDL = new SQLServer_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            GlobalSettings.LogMessage("UpdateTo_2_0_0_0", "Updating database structure to suit version 2.0.0.0", LogEntryType.Information);

            success = CreateRelation(DDL.Table_devicetype_2000, con);
            success &= CreateRelation(DDL.Table_device_2000, con);
            success &= CreateRelation(DDL.Table_devicefeature_2000, con);
            success &= CreateRelation(DDL.Table_devicereading_energy_2000, con);
            success &= CreateRelation(DDL.View_devicedayoutput_v_2000, con);

            if (success)
                UpdateVersion("2", "0", "0", "0", con);

            return success;
        }

        public struct DBVersion
        {
            public String major;
            public String minor;
            public String release;
            public String patch;

            public int CompareVersion(String majorR, String minorR, String releaseR, String patchR)
            {
                int res;

                if ((res = int.Parse(major).CompareTo(int.Parse(majorR))) > 0)
                    return res;
                else if (res == 0)
                {
                    if ((res = int.Parse(minor).CompareTo(int.Parse(minorR))) > 0)
                        return res;
                    else if (res == 0)
                    {
                        if ((res = int.Parse(release).CompareTo(int.Parse(releaseR))) > 0)
                            return res;
                        else if (res == 0)
                            res = int.Parse(patch).CompareTo(int.Parse(patchR));
                    }
                }
                return res;
            }
        }

        private bool RelationExists(GenConnection con, string relationName)
        {
            GenCommand cmd = null;

            try
            {
                cmd = new GenCommand("Select * from " + relationName + " where 0 = 1 ", con);
                GenDataReader dr;
                dr = (GenDataReader)cmd.ExecuteReader();                
                dr.Close();
                return true;
            }
            catch (Exception e)
            {
                if (GlobalSettings.SystemServices != null) // true when service is running
                    GlobalSettings.LogMessage("RelationExists", "Exception: " + e.Message);
                return false;
            }
        }

        private bool PopulateEmptyDatabase(GenConnection con, String databaseType)
        {
            DDL DDL;
            
            if (databaseType == "MySql")
                DDL = new MySql_DDL();
            else if (databaseType == "Jet")
                DDL = new Jet_DDL();
            else if (databaseType == "SQLite")
                DDL = new SQLite_DDL();
            else if (databaseType == "SQL Server")
                DDL = new SQLServer_DDL();
            else
                throw new Exception("Unexpected database type: " + databaseType);

            bool success = true;
            
            if (success && !RelationExists(con, "devicetype"))
                success &= CreateRelation(DDL.CurrentTable_devicetype, con);
            if (success && !RelationExists(con, "device"))
                success &= CreateRelation(DDL.CurrentTable_device, con);
            if (success && !RelationExists(con, "devicefeature"))
                success &= CreateRelation(DDL.CurrentTable_devicefeature, con);
            if (success && !RelationExists(con, "devicereading_energy"))
                success &= CreateRelation(DDL.CurrentTable_devicereading_energy, con);
            if (success && !RelationExists(con, "devicedayoutput_v"))
                success &= CreateRelation(DDL.CurrentView_devicedayoutput_v, con);
            if (success && !RelationExists(con, "pvoutputlog"))
                success &= CreateRelation(DDL.CurrentTable_pvoutputlog, con);

            if (success && !RelationExists(con, "version"))
                success &= CreateRelation(DDL.CurrentTable_version, con);

            if (success)
                UpdateVersion("2", "0", "0", "0", con);

            return success;
        }

        public void PopulateDatabaseIfEmpty(GenConnection con)
        {
            try
            {
                if (!GetCurrentVersion(con, GlobalSettings.ApplicationSettings.DatabaseType, out Version))
                    PopulateEmptyDatabase(con, GlobalSettings.ApplicationSettings.DatabaseType);
            }
            catch (Exception e)
            {
                throw new Exception("Exception in PopulateDatabaseIfEmpty: " + e.Message, e);
            }
        }

        public void UpdateVersion(GenConnection con, String databaseType)
        {
            if (GetCurrentVersion(con, databaseType, out Version))
            {

                if (databaseType != "SQL Server")
                {
                    UpdateTo_1_3_5_4(con, databaseType);
                    UpdateTo_1_3_6_0(con, databaseType);
                    UpdateTo_1_4_0_0(con, databaseType);
                    UpdateTo_1_4_0_1(con, databaseType);
                    UpdateTo_1_4_0_3(con, databaseType);
                }

                UpdateTo_1_4_1_9(con, databaseType);
                UpdateTo_1_4_2_1(con, databaseType);
                UpdateTo_1_5_0_0(con, databaseType);

                if (databaseType == "SQL Server")
                    UpdateTo_1_5_0_2(con, databaseType);

                UpdateTo_1_7_0_0(con, databaseType);
                UpdateTo_1_7_1_0(con, databaseType);
                UpdateTo_1_8_3_0(con, databaseType);
                UpdateTo_1_8_3_6(con, databaseType);
                UpdateTo_1_9_0_2(con, databaseType);
                UpdateTo_2_0_0_0(con, databaseType);
            }
            else
                PopulateEmptyDatabase(con, databaseType);
        }

    }
}
