/*
Deployment script for PVHistory
*/

GO
SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;

SET NUMERIC_ROUNDABORT OFF;


GO
:setvar DatabaseName "PVHistory"
:setvar DefaultDataPath "C:\Program Files\Microsoft SQL Server\MSSQL10.SQLEXPRESS\MSSQL\DATA\"
:setvar DefaultLogPath "C:\Program Files\Microsoft SQL Server\MSSQL10.SQLEXPRESS\MSSQL\DATA\"

GO
USE [master]

GO
:on error exit
GO
IF (DB_ID(N'$(DatabaseName)') IS NOT NULL
    AND DATABASEPROPERTYEX(N'$(DatabaseName)','Status') <> N'ONLINE')
BEGIN
    RAISERROR(N'The state of the target database, %s, is not set to ONLINE. To deploy to this database, its state must be set to ONLINE.', 16, 127,N'$(DatabaseName)') WITH NOWAIT
    RETURN
END

GO
IF (DB_ID(N'$(DatabaseName)') IS NOT NULL) 
BEGIN
    ALTER DATABASE [$(DatabaseName)]
    SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$(DatabaseName)];
END

GO
PRINT N'Creating $(DatabaseName)...'
GO
CREATE DATABASE [$(DatabaseName)]
    ON 
    PRIMARY(NAME = [PVHistory], FILENAME = N'$(DefaultDataPath)PVHistory.mdf')
    LOG ON (NAME = [PVHistory_log], FILENAME = N'$(DefaultLogPath)PVHistory_log.ldf') COLLATE SQL_Latin1_General_CP1_CI_AS
GO
EXECUTE sp_dbcmptlevel [$(DatabaseName)], 100;


GO
IF EXISTS (SELECT 1
           FROM   [master].[dbo].[sysdatabases]
           WHERE  [name] = N'$(DatabaseName)')
    BEGIN
        ALTER DATABASE [$(DatabaseName)]
            SET ANSI_NULLS ON,
                ANSI_PADDING ON,
                ANSI_WARNINGS ON,
                ARITHABORT ON,
                CONCAT_NULL_YIELDS_NULL ON,
                NUMERIC_ROUNDABORT OFF,
                QUOTED_IDENTIFIER ON,
                ANSI_NULL_DEFAULT ON,
                CURSOR_DEFAULT LOCAL,
                RECOVERY FULL,
                CURSOR_CLOSE_ON_COMMIT OFF,
                AUTO_CREATE_STATISTICS ON,
                AUTO_SHRINK ON,
                AUTO_UPDATE_STATISTICS ON,
                RECURSIVE_TRIGGERS OFF 
            WITH ROLLBACK IMMEDIATE;
        ALTER DATABASE [$(DatabaseName)]
            SET AUTO_CLOSE OFF 
            WITH ROLLBACK IMMEDIATE;
    END


GO
IF EXISTS (SELECT 1
           FROM   [master].[dbo].[sysdatabases]
           WHERE  [name] = N'$(DatabaseName)')
    BEGIN
        ALTER DATABASE [$(DatabaseName)]
            SET ALLOW_SNAPSHOT_ISOLATION OFF;
    END


GO
IF EXISTS (SELECT 1
           FROM   [master].[dbo].[sysdatabases]
           WHERE  [name] = N'$(DatabaseName)')
    BEGIN
        ALTER DATABASE [$(DatabaseName)]
            SET READ_COMMITTED_SNAPSHOT OFF;
    END


GO
IF EXISTS (SELECT 1
           FROM   [master].[dbo].[sysdatabases]
           WHERE  [name] = N'$(DatabaseName)')
    BEGIN
        ALTER DATABASE [$(DatabaseName)]
            SET AUTO_UPDATE_STATISTICS_ASYNC ON,
                PAGE_VERIFY NONE,
                DATE_CORRELATION_OPTIMIZATION OFF,
                DISABLE_BROKER,
                PARAMETERIZATION SIMPLE,
                SUPPLEMENTAL_LOGGING OFF 
            WITH ROLLBACK IMMEDIATE;
    END


GO
IF IS_SRVROLEMEMBER(N'sysadmin') = 1
    BEGIN
        IF EXISTS (SELECT 1
                   FROM   [master].[dbo].[sysdatabases]
                   WHERE  [name] = N'$(DatabaseName)')
            BEGIN
                EXECUTE sp_executesql N'ALTER DATABASE [$(DatabaseName)]
    SET TRUSTWORTHY OFF,
        DB_CHAINING OFF 
    WITH ROLLBACK IMMEDIATE';
            END
    END
ELSE
    BEGIN
        PRINT N'The database settings cannot be modified. You must be a SysAdmin to apply these settings.';
    END


GO
IF IS_SRVROLEMEMBER(N'sysadmin') = 1
    BEGIN
        IF EXISTS (SELECT 1
                   FROM   [master].[dbo].[sysdatabases]
                   WHERE  [name] = N'$(DatabaseName)')
            BEGIN
                EXECUTE sp_executesql N'ALTER DATABASE [$(DatabaseName)]
    SET HONOR_BROKER_PRIORITY OFF 
    WITH ROLLBACK IMMEDIATE';
            END
    END
ELSE
    BEGIN
        PRINT N'The database settings cannot be modified. You must be a SysAdmin to apply these settings.';
    END


GO
USE [$(DatabaseName)]

GO
IF fulltextserviceproperty(N'IsFulltextInstalled') = 1
    EXECUTE sp_fulltext_database 'enable';


GO
/*
 Pre-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be executed before the build script.	
 Use SQLCMD syntax to include a file in the pre-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the pre-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/

GO
PRINT N'Creating [pvhistory]...';


GO
CREATE SCHEMA [pvhistory]
    AUTHORIZATION [dbo];


GO
PRINT N'Creating [pvhistory].[inverter]...';


GO
CREATE TABLE [pvhistory].[inverter] (
    [Id]                 INT           IDENTITY (1, 1) NOT NULL,
    [SerialNumber]       VARCHAR (45)  NOT NULL,
    [Location]           VARCHAR (255) NULL,
    [InverterType_Id]    INT           NOT NULL,
    [InverterManager_Id] INT           NULL
);


GO
PRINT N'Creating Inverter_PK...';


GO
ALTER TABLE [pvhistory].[inverter]
    ADD CONSTRAINT [Inverter_PK] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating InverterIdentity_UK...';


GO
ALTER TABLE [pvhistory].[inverter]
    ADD CONSTRAINT [InverterIdentity_UK] UNIQUE NONCLUSTERED ([SerialNumber] ASC, [InverterType_Id] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating [pvhistory].[invertermanager]...';


GO
CREATE TABLE [pvhistory].[invertermanager] (
    [Id]               INT           IDENTITY (1, 1) NOT NULL,
    [ManagerType]      VARCHAR (50)  NOT NULL,
    [InstanceNo]       INT           NOT NULL,
    [NextFileDate]     DATETIME      NULL,
    [ConfigFileName]   VARCHAR (255) NULL,
    [OutputDirectory]  VARCHAR (255) NULL,
    [ArchiveDirectory] VARCHAR (255) NULL,
    [Password]         VARCHAR (20)  NULL,
    [Frequency]        INT           NULL,
    [Enabled]          INT           NOT NULL,
    [FileNamePattern]  VARCHAR (255) NULL
);


GO
PRINT N'Creating InverterManager_PK...';


GO
ALTER TABLE [pvhistory].[invertermanager]
    ADD CONSTRAINT [InverterManager_PK] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating InverterManager_UK...';


GO
ALTER TABLE [pvhistory].[invertermanager]
    ADD CONSTRAINT [InverterManager_UK] UNIQUE NONCLUSTERED ([ManagerType] ASC, [InstanceNo] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating [pvhistory].[invertertype]...';


GO
CREATE TABLE [pvhistory].[invertertype] (
    [Id]           INT          IDENTITY (1, 1) NOT NULL,
    [Manufacturer] VARCHAR (60) NOT NULL,
    [Model]        VARCHAR (50) NOT NULL,
    [MaxOutput]    INT          NULL
);


GO
PRINT N'Creating InverterType_PK...';


GO
ALTER TABLE [pvhistory].[invertertype]
    ADD CONSTRAINT [InverterType_PK] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating InverterTypeManModel_UK...';


GO
ALTER TABLE [pvhistory].[invertertype]
    ADD CONSTRAINT [InverterTypeManModel_UK] UNIQUE NONCLUSTERED ([Manufacturer] ASC, [Model] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating [pvhistory].[meter]...';


GO
CREATE TABLE [pvhistory].[meter] (
    [Id]                INT          IDENTITY (1, 1) NOT NULL,
    [MeterName]         VARCHAR (45) NOT NULL,
    [MeterType]         VARCHAR (45) NOT NULL,
    [StandardDuration]  INT          NULL,
    [CondensedDuration] INT          NULL,
    [CondenseAge]       INT          NULL,
    [Enabled]           INT          NOT NULL
);


GO
PRINT N'Creating Meter_PK...';


GO
ALTER TABLE [pvhistory].[meter]
    ADD CONSTRAINT [Meter_PK] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating MeterName_UK...';


GO
ALTER TABLE [pvhistory].[meter]
    ADD CONSTRAINT [MeterName_UK] UNIQUE NONCLUSTERED ([MeterName] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating [pvhistory].[meterhistory]...';


GO
CREATE TABLE [pvhistory].[meterhistory] (
    [Meter_Id]    INT      NOT NULL,
    [Appliance]   INT      NOT NULL,
    [ReadingTime] DATETIME NOT NULL,
    [HistoryType] CHAR (1) NOT NULL,
    [Duration]    INT      NOT NULL,
    [Energy]      FLOAT    NOT NULL
);


GO
PRINT N'Creating MeterHistory_PK...';


GO
ALTER TABLE [pvhistory].[meterhistory]
    ADD CONSTRAINT [MeterHistory_PK] PRIMARY KEY CLUSTERED ([Meter_Id] ASC, [Appliance] ASC, [ReadingTime] ASC, [HistoryType] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating [pvhistory].[meterreading]...';


GO
CREATE TABLE [pvhistory].[meterreading] (
    [Meter_Id]    INT      NOT NULL,
    [Appliance]   INT      NOT NULL,
    [ReadingTime] DATETIME NOT NULL,
    [Duration]    INT      NULL,
    [Energy]      FLOAT    NULL,
    [Temperature] REAL     NULL,
    [Calculated]  FLOAT    NULL,
    [MinPower]    INT      NULL,
    [MaxPower]    INT      NULL
);


GO
PRINT N'Creating MeterReading_PK...';


GO
ALTER TABLE [pvhistory].[meterreading]
    ADD CONSTRAINT [MeterReading_PK] PRIMARY KEY CLUSTERED ([Meter_Id] ASC, [Appliance] ASC, [ReadingTime] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating [pvhistory].[outputhistory]...';


GO
CREATE TABLE [pvhistory].[outputhistory] (
    [Inverter_Id] INT      NOT NULL,
    [OutputTime]  DATETIME NOT NULL,
    [OutputKwh]   FLOAT    NOT NULL,
    [Duration]    INT      NOT NULL
);


GO
PRINT N'Creating OutputHistory_PK...';


GO
ALTER TABLE [pvhistory].[outputhistory]
    ADD CONSTRAINT [OutputHistory_PK] PRIMARY KEY CLUSTERED ([Inverter_Id] ASC, [OutputTime] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating [pvhistory].[pvoutputlog]...';


GO
CREATE TABLE [pvhistory].[pvoutputlog] (
    [SiteId]       VARCHAR (10) NOT NULL,
    [OutputDay]    DATE         NOT NULL,
    [OutputTime]   INT          NOT NULL,
    [Energy]       FLOAT        NULL,
    [Power]        FLOAT        NULL,
    [Loaded]       INT          NOT NULL,
    [ImportEnergy] FLOAT        NULL,
    [ImportPower]  FLOAT        NULL
);


GO
PRINT N'Creating PVOutputLog_PK...';


GO
ALTER TABLE [pvhistory].[pvoutputlog]
    ADD CONSTRAINT [PVOutputLog_PK] PRIMARY KEY CLUSTERED ([SiteId] ASC, [OutputDay] ASC, [OutputTime] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating [pvhistory].[version]...';


GO
CREATE TABLE [pvhistory].[version] (
    [Major]   VARCHAR (4) NOT NULL,
    [Minor]   VARCHAR (4) NOT NULL,
    [Release] VARCHAR (4) NOT NULL,
    [Patch]   VARCHAR (4) NOT NULL
);


GO
PRINT N'Creating Version_PK...';


GO
ALTER TABLE [pvhistory].[version]
    ADD CONSTRAINT [Version_PK] PRIMARY KEY CLUSTERED ([Major] ASC, [Minor] ASC, [Release] ASC, [Patch] ASC) WITH (ALLOW_PAGE_LOCKS = ON, ALLOW_ROW_LOCKS = ON, PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = OFF);


GO
PRINT N'Creating On column: Enabled...';


GO
ALTER TABLE [pvhistory].[invertermanager]
    ADD DEFAULT 0 FOR [Enabled];


GO
PRINT N'Creating On column: Enabled...';


GO
ALTER TABLE [pvhistory].[meter]
    ADD DEFAULT 0 FOR [Enabled];


GO
PRINT N'Creating InverterInverterManager_FK...';


GO
ALTER TABLE [pvhistory].[inverter] WITH NOCHECK
    ADD CONSTRAINT [InverterInverterManager_FK] FOREIGN KEY ([InverterManager_Id]) REFERENCES [pvhistory].[invertermanager] ([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION;


GO
PRINT N'Creating InverterType_FK...';


GO
ALTER TABLE [pvhistory].[inverter] WITH NOCHECK
    ADD CONSTRAINT [InverterType_FK] FOREIGN KEY ([InverterType_Id]) REFERENCES [pvhistory].[invertertype] ([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION;


GO
PRINT N'Creating MeterHistoryMeter_FK...';


GO
ALTER TABLE [pvhistory].[meterhistory] WITH NOCHECK
    ADD CONSTRAINT [MeterHistoryMeter_FK] FOREIGN KEY ([Meter_Id]) REFERENCES [pvhistory].[meter] ([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION;


GO
PRINT N'Creating MeterReadingMeter_FK...';


GO
ALTER TABLE [pvhistory].[meterreading] WITH NOCHECK
    ADD CONSTRAINT [MeterReadingMeter_FK] FOREIGN KEY ([Meter_Id]) REFERENCES [pvhistory].[meter] ([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION;


GO
PRINT N'Creating OutputHistoryInverter_FK...';


GO
ALTER TABLE [pvhistory].[outputhistory] WITH NOCHECK
    ADD CONSTRAINT [OutputHistoryInverter_FK] FOREIGN KEY ([Inverter_Id]) REFERENCES [pvhistory].[inverter] ([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION;


GO
PRINT N'Creating [pvhistory].[dayoutput_v]...';


GO
CREATE VIEW [pvhistory].[dayoutput_v]
	AS SELECT oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME) OutputDay, SUM(oh.OutputKwh) OutputKwh
	from OutputHistory oh
	group by oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME)
GO
PRINT N'Creating [pvhistory].[fulldayoutput_v]...';


GO
CREATE VIEW [pvhistory].[fulldayoutput_v]
	AS SELECT oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME) OutputDay, SUM(oh.OutputKwh) OutputKwh
	from OutputHistory oh
	group by oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME)
	having SUM(oh.Duration) = (60 * 60 * 24)
GO
PRINT N'Creating [pvhistory].[fulldayoutputsummary_v]...';


GO
CREATE VIEW [pvhistory].[fulldayoutputsummary_v]
	AS SELECT OutputDay, SUM(OutputKwh) OutputKwh
	from FullDayOutput_v
	group by OutputDay
GO
PRINT N'Creating [pvhistory].[fulldayoutputtimes_v]...';


GO
CREATE VIEW [pvhistory].[fulldayoutputtimes_v]
	AS SELECT oh2.Inverter_Id, CAST(FLOOR(CAST(oh2.OutputTime AS float)) AS DATETIME) OutputDay, 
	SUM(oh2.OutputKwh) OutputKwh, DATEDIFF( second, CAST(FLOOR(CAST(MIN(oh2.OutputTime) AS float)) AS DATETIME), MIN(oh2.OutputTime)) StartTime, 
	DATEDIFF( second, CAST(FLOOR(CAST(MAX(oh2.OutputTime) AS float)) AS DATETIME), MAX(oh2.OutputTime)) EndTime
	from OutputHistory oh2, FullDayOutput_v fd
	where oh2.OutputKwh <> 0
	and oh2.Inverter_Id = fd.Inverter_id
	and CAST(FLOOR(CAST(oh2.OutputTime AS float)) AS DATETIME) = fd.OutputDay
	group by oh2.Inverter_Id, CAST(FLOOR(CAST(oh2.OutputTime AS float)) AS DATETIME)
GO
PRINT N'Creating [pvhistory].[outputhistory_v]...';


GO
CREATE VIEW [pvhistory].[outputhistory_v]
	AS SELECT Inverter_Id, OutputTime, OutputKwh, Duration, 
	CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME) OutputDate, 
	DATEDIFF( second, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), OutputTime) OutputTimeOfDay,
	(OutputKwh * 3600 / Duration ) 'OutputKw'
	from OutputHistory
GO
PRINT N'Creating [pvhistory].[pvoutput_sub_v]...';


GO
CREATE VIEW [pvhistory].[pvoutput_sub_v]
	AS select CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME) as OutputDay, 
	DATEDIFF( second, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME), oh.OutputTime) as OutputTime,
	sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) As Power
	from outputhistory as oh
	where oh.OutputTime >= (GETDATE() - 20)
	and oh.OutputKwh > 0 
	group by CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME), DATEDIFF( second, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME), oh.OutputTime)
GO
PRINT N'Creating [pvhistory].[pvoutput_v]...';


GO
CREATE VIEW [pvhistory].[pvoutput_v]
	AS SELECT OutputDay, FLOOR(OutputTime / 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, 
	max(Power)*1000 as Power, min(Power)*1000 as MinPower
	from pvoutput_sub_v
	group by OutputDay, FLOOR(OutputTime / 600) * 600
GO
PRINT N'Creating [pvhistory].[pvoutput5min]...';


GO
CREATE VIEW [pvhistory].[pvoutput5min]
	AS SELECT OutputDay, FLOOR(OutputTime / 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, 
	max(Power)*1000 as Power, min(Power)*1000 as MinPower
	from pvoutput_sub_v
	group by OutputDay, FLOOR(OutputTime / 300) * 300
GO
-- Refactoring step to update target server with deployed transaction logs
CREATE TABLE  [dbo].[__RefactorLog] (OperationKey UNIQUEIDENTIFIER NOT NULL PRIMARY KEY)
GO
sp_addextendedproperty N'microsoft_database_tools_support', N'refactoring log', N'schema', N'dbo', N'table', N'__RefactorLog'
GO

GO
/*
Post-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be appended to the build script.		
 Use SQLCMD syntax to include a file in the post-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the post-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/

use pvhistory;

delete from pvhistory.version;
insert into pvhistory.version (Major, Minor, Release, Patch)
values ('1', '4', '1', '0');

/* drop user [NT AUTHORITY\LOCAL SERVICE]; */
create user [NT AUTHORITY\LOCAL SERVICE] without login with default_schema = pvhistory;
grant CONTROL to [NT AUTHORITY\LOCAL SERVICE];




GO
PRINT N'Checking existing data against newly created constraints';


GO
USE [$(DatabaseName)];


GO
ALTER TABLE [pvhistory].[inverter] WITH CHECK CHECK CONSTRAINT [InverterInverterManager_FK];

ALTER TABLE [pvhistory].[inverter] WITH CHECK CHECK CONSTRAINT [InverterType_FK];

ALTER TABLE [pvhistory].[meterhistory] WITH CHECK CHECK CONSTRAINT [MeterHistoryMeter_FK];

ALTER TABLE [pvhistory].[meterreading] WITH CHECK CHECK CONSTRAINT [MeterReadingMeter_FK];

ALTER TABLE [pvhistory].[outputhistory] WITH CHECK CHECK CONSTRAINT [OutputHistoryInverter_FK];


GO
IF EXISTS (SELECT 1
           FROM   [master].[dbo].[sysdatabases]
           WHERE  [name] = N'$(DatabaseName)')
    BEGIN
        DECLARE @VarDecimalSupported AS BIT;
        SELECT @VarDecimalSupported = 0;
        IF ((ServerProperty(N'EngineEdition') = 3)
            AND (((@@microsoftversion / power(2, 24) = 9)
                  AND (@@microsoftversion & 0xffff >= 3024))
                 OR ((@@microsoftversion / power(2, 24) = 10)
                     AND (@@microsoftversion & 0xffff >= 1600))))
            SELECT @VarDecimalSupported = 1;
        IF (@VarDecimalSupported > 0)
            BEGIN
                EXECUTE sp_db_vardecimal_storage_format N'$(DatabaseName)', 'ON';
            END
    END


GO
