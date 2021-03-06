USE [master]
GO
/****** Object:  Database [PVHistory]    Script Date: 05/13/2011 22:49:40 ******/
CREATE DATABASE [PVHistory] ON  PRIMARY 
( NAME = N'PVHistory', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL10.SQLEXPRESS\MSSQL\DATA\PVHistory.mdf' , SIZE = 2304KB , MAXSIZE = UNLIMITED, FILEGROWTH = 1024KB )
 LOG ON 
( NAME = N'PVHistory_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL10.SQLEXPRESS\MSSQL\DATA\PVHistory_log.ldf' , SIZE = 1024KB , MAXSIZE = 2048GB , FILEGROWTH = 10%)
GO
ALTER DATABASE [PVHistory] SET COMPATIBILITY_LEVEL = 100
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [PVHistory].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [PVHistory] SET ANSI_NULL_DEFAULT ON
GO
ALTER DATABASE [PVHistory] SET ANSI_NULLS ON
GO
ALTER DATABASE [PVHistory] SET ANSI_PADDING ON
GO
ALTER DATABASE [PVHistory] SET ANSI_WARNINGS ON
GO
ALTER DATABASE [PVHistory] SET ARITHABORT ON
GO
ALTER DATABASE [PVHistory] SET AUTO_CLOSE OFF
GO
ALTER DATABASE [PVHistory] SET AUTO_CREATE_STATISTICS ON
GO
ALTER DATABASE [PVHistory] SET AUTO_SHRINK ON
GO
ALTER DATABASE [PVHistory] SET AUTO_UPDATE_STATISTICS ON
GO
ALTER DATABASE [PVHistory] SET CURSOR_CLOSE_ON_COMMIT OFF
GO
ALTER DATABASE [PVHistory] SET CURSOR_DEFAULT  LOCAL
GO
ALTER DATABASE [PVHistory] SET CONCAT_NULL_YIELDS_NULL ON
GO
ALTER DATABASE [PVHistory] SET NUMERIC_ROUNDABORT OFF
GO
ALTER DATABASE [PVHistory] SET QUOTED_IDENTIFIER ON
GO
ALTER DATABASE [PVHistory] SET RECURSIVE_TRIGGERS OFF
GO
ALTER DATABASE [PVHistory] SET  DISABLE_BROKER
GO
ALTER DATABASE [PVHistory] SET AUTO_UPDATE_STATISTICS_ASYNC ON
GO
ALTER DATABASE [PVHistory] SET DATE_CORRELATION_OPTIMIZATION OFF
GO
ALTER DATABASE [PVHistory] SET TRUSTWORTHY OFF
GO
ALTER DATABASE [PVHistory] SET ALLOW_SNAPSHOT_ISOLATION OFF
GO
ALTER DATABASE [PVHistory] SET PARAMETERIZATION SIMPLE
GO
ALTER DATABASE [PVHistory] SET READ_COMMITTED_SNAPSHOT OFF
GO
ALTER DATABASE [PVHistory] SET HONOR_BROKER_PRIORITY OFF
GO
ALTER DATABASE [PVHistory] SET  READ_WRITE
GO
ALTER DATABASE [PVHistory] SET RECOVERY FULL
GO
ALTER DATABASE [PVHistory] SET  MULTI_USER
GO
ALTER DATABASE [PVHistory] SET PAGE_VERIFY NONE
GO
ALTER DATABASE [PVHistory] SET DB_CHAINING OFF
GO
USE [PVHistory]
GO
/****** Object:  User [NT AUTHORITY\LOCAL SERVICE]    Script Date: 05/13/2011 22:49:40 ******/
CREATE USER [NT AUTHORITY\LOCAL SERVICE] FOR LOGIN [NT AUTHORITY\LOCAL SERVICE] WITH DEFAULT_SCHEMA=[pvhistory]
GO
/****** Object:  Schema [pvhistory]    Script Date: 05/13/2011 22:49:40 ******/
CREATE SCHEMA [pvhistory] AUTHORIZATION [dbo]
GO
/****** Object:  Table [pvhistory].[meter]    Script Date: 05/13/2011 22:49:41 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [pvhistory].[meter](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[MeterName] [varchar](45) NOT NULL,
	[MeterType] [varchar](45) NOT NULL,
	[StandardDuration] [int] NULL,
	[CondensedDuration] [int] NULL,
	[CondenseAge] [int] NULL,
	[Enabled] [bit] NOT NULL,
 CONSTRAINT [Meter_PK] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY],
 CONSTRAINT [MeterName_UK] UNIQUE NONCLUSTERED 
(
	[MeterName] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Table [pvhistory].[invertertype]    Script Date: 05/13/2011 22:49:41 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [pvhistory].[invertertype](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Manufacturer] [varchar](60) NOT NULL,
	[Model] [varchar](50) NOT NULL,
	[MaxOutput] [int] NULL,
 CONSTRAINT [InverterType_PK] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY],
 CONSTRAINT [InverterTypeManModel_UK] UNIQUE NONCLUSTERED 
(
	[Manufacturer] ASC,
	[Model] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Table [pvhistory].[invertermanager]    Script Date: 05/13/2011 22:49:41 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [pvhistory].[invertermanager](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ManagerType] [varchar](50) NOT NULL,
	[InstanceNo] [int] NOT NULL,
	[NextFileDate] [datetime] NULL,
	[ConfigFileName] [varchar](255) NULL,
	[OutputDirectory] [varchar](255) NULL,
	[ArchiveDirectory] [varchar](255) NULL,
	[Password] [varchar](20) NULL,
	[Frequency] [int] NULL,
	[Enabled] [bit] NOT NULL,
	[FileNamePattern] [varchar](255) NULL,
 CONSTRAINT [InverterManager_PK] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY],
 CONSTRAINT [InverterManager_UK] UNIQUE NONCLUSTERED 
(
	[ManagerType] ASC,
	[InstanceNo] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Table [pvhistory].[version]    Script Date: 05/13/2011 22:49:41 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [pvhistory].[version](
	[Major] [varchar](4) NOT NULL,
	[Minor] [varchar](4) NOT NULL,
	[Release] [varchar](4) NOT NULL,
	[Patch] [varchar](4) NOT NULL,
 CONSTRAINT [Version_PK] PRIMARY KEY CLUSTERED 
(
	[Major] ASC,
	[Minor] ASC,
	[Release] ASC,
	[Patch] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Table [pvhistory].[pvoutputlog]    Script Date: 05/13/2011 22:49:41 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [pvhistory].[pvoutputlog](
	[SiteId] [varchar](10) NOT NULL,
	[OutputDay] [date] NOT NULL,
	[OutputTime] [int] NOT NULL,
	[Energy] [float] NULL,
	[Power] [float] NULL,
	[Loaded] [bit] NOT NULL,
	[ImportEnergy] [float] NULL,
	[ImportPower] [float] NULL,
 CONSTRAINT [PVOutputLog_PK] PRIMARY KEY CLUSTERED 
(
	[SiteId] ASC,
	[OutputDay] ASC,
	[OutputTime] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Table [pvhistory].[meterreading]    Script Date: 05/13/2011 22:49:41 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [pvhistory].[meterreading](
	[Meter_Id] [int] NOT NULL,
	[Appliance] [int] NOT NULL,
	[ReadingTime] [datetime] NOT NULL,
	[Duration] [int] NULL,
	[Energy] [float] NULL,
	[Temperature] [real] NULL,
	[Calculated] [float] NULL,
	[MinPower] [int] NULL,
	[MaxPower] [int] NULL,
 CONSTRAINT [MeterReading_PK] PRIMARY KEY CLUSTERED 
(
	[Meter_Id] ASC,
	[Appliance] ASC,
	[ReadingTime] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [pvhistory].[meterhistory]    Script Date: 05/13/2011 22:49:41 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [pvhistory].[meterhistory](
	[Meter_Id] [int] NOT NULL,
	[Appliance] [int] NOT NULL,
	[ReadingTime] [datetime] NOT NULL,
	[HistoryType] [char](1) NOT NULL,
	[Duration] [int] NOT NULL,
	[Energy] [float] NOT NULL,
 CONSTRAINT [MeterHistory_PK] PRIMARY KEY CLUSTERED 
(
	[Meter_Id] ASC,
	[Appliance] ASC,
	[ReadingTime] ASC,
	[HistoryType] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Table [pvhistory].[inverter]    Script Date: 05/13/2011 22:49:41 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [pvhistory].[inverter](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[SerialNumber] [varchar](45) NOT NULL,
	[Location] [varchar](255) NULL,
	[InverterType_Id] [int] NOT NULL,
	[InverterManager_Id] [int] NULL,
 CONSTRAINT [Inverter_PK] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY],
 CONSTRAINT [InverterIdentity_UK] UNIQUE NONCLUSTERED 
(
	[SerialNumber] ASC,
	[InverterType_Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Table [pvhistory].[outputhistory]    Script Date: 05/13/2011 22:49:41 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [pvhistory].[outputhistory](
	[Inverter_Id] [int] NOT NULL,
	[OutputTime] [datetime] NOT NULL,
	[OutputKwh] [float] NOT NULL,
	[Duration] [int] NOT NULL,
 CONSTRAINT [OutputHistory_PK] PRIMARY KEY CLUSTERED 
(
	[Inverter_Id] ASC,
	[OutputTime] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [pvhistory].[fulldayoutput_v]    Script Date: 05/13/2011 22:49:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [pvhistory].[fulldayoutput_v]
	AS SELECT oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME) OutputDay, SUM(oh.OutputKwh) OutputKwh
	from OutputHistory oh
	group by oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME)
	having SUM(oh.Duration) = (60 * 60 * 24)
GO
/****** Object:  View [pvhistory].[dayoutput_v]    Script Date: 05/13/2011 22:49:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [pvhistory].[dayoutput_v]
	AS SELECT oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME) OutputDay, SUM(oh.OutputKwh) OutputKwh
	from OutputHistory oh
	group by oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME)
GO
/****** Object:  View [pvhistory].[pvoutput_sub_v]    Script Date: 05/13/2011 22:49:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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
/****** Object:  View [pvhistory].[outputhistory_v]    Script Date: 05/13/2011 22:49:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [pvhistory].[outputhistory_v]
	AS SELECT Inverter_Id, OutputTime, OutputKwh, Duration, 
	CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME) OutputDate, 
	DATEDIFF( second, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), OutputTime) OutputTimeOfDay,
	(OutputKwh * 3600 / Duration ) 'OutputKw'
	from OutputHistory
GO
/****** Object:  View [pvhistory].[pvoutput5min_v]    Script Date: 05/13/2011 22:49:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [pvhistory].[pvoutput5min_v]
	AS SELECT OutputDay, FLOOR(OutputTime / 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, 
	max(Power)*1000 as Power, min(Power)*1000 as MinPower
	from pvoutput_sub_v
	group by OutputDay, FLOOR(OutputTime / 300) * 300
GO
/****** Object:  View [pvhistory].[pvoutput5min]    Script Date: 05/13/2011 22:49:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [pvhistory].[pvoutput5min]
	AS SELECT OutputDay, FLOOR(OutputTime / 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, 
	max(Power)*1000 as Power, min(Power)*1000 as MinPower
	from pvoutput_sub_v
	group by OutputDay, FLOOR(OutputTime / 300) * 300
GO
/****** Object:  View [pvhistory].[pvoutput_v]    Script Date: 05/13/2011 22:49:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [pvhistory].[pvoutput_v]
	AS SELECT OutputDay, FLOOR(OutputTime / 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, 
	max(Power)*1000 as Power, min(Power)*1000 as MinPower
	from pvoutput_sub_v
	group by OutputDay, FLOOR(OutputTime / 600) * 600
GO
/****** Object:  View [pvhistory].[fulldayoutputtimes_v]    Script Date: 05/13/2011 22:49:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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
/****** Object:  View [pvhistory].[fulldayoutputsummary_v]    Script Date: 05/13/2011 22:49:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [pvhistory].[fulldayoutputsummary_v]
	AS SELECT OutputDay, SUM(OutputKwh) OutputKwh
	from FullDayOutput_v
	group by OutputDay
GO
/****** Object:  Default [DF__meter__Enabled__1367E606]    Script Date: 05/13/2011 22:49:41 ******/
ALTER TABLE [pvhistory].[meter] ADD  DEFAULT ((0)) FOR [Enabled]
GO
/****** Object:  Default [DF__inverterm__Enabl__1273C1CD]    Script Date: 05/13/2011 22:49:41 ******/
ALTER TABLE [pvhistory].[invertermanager] ADD  DEFAULT ((0)) FOR [Enabled]
GO
/****** Object:  ForeignKey [MeterReadingMeter_FK]    Script Date: 05/13/2011 22:49:41 ******/
ALTER TABLE [pvhistory].[meterreading]  WITH CHECK ADD  CONSTRAINT [MeterReadingMeter_FK] FOREIGN KEY([Meter_Id])
REFERENCES [pvhistory].[meter] ([Id])
GO
ALTER TABLE [pvhistory].[meterreading] CHECK CONSTRAINT [MeterReadingMeter_FK]
GO
/****** Object:  ForeignKey [MeterHistoryMeter_FK]    Script Date: 05/13/2011 22:49:41 ******/
ALTER TABLE [pvhistory].[meterhistory]  WITH CHECK ADD  CONSTRAINT [MeterHistoryMeter_FK] FOREIGN KEY([Meter_Id])
REFERENCES [pvhistory].[meter] ([Id])
GO
ALTER TABLE [pvhistory].[meterhistory] CHECK CONSTRAINT [MeterHistoryMeter_FK]
GO
/****** Object:  ForeignKey [InverterInverterManager_FK]    Script Date: 05/13/2011 22:49:41 ******/
ALTER TABLE [pvhistory].[inverter]  WITH CHECK ADD  CONSTRAINT [InverterInverterManager_FK] FOREIGN KEY([InverterManager_Id])
REFERENCES [pvhistory].[invertermanager] ([Id])
GO
ALTER TABLE [pvhistory].[inverter] CHECK CONSTRAINT [InverterInverterManager_FK]
GO
/****** Object:  ForeignKey [InverterType_FK]    Script Date: 05/13/2011 22:49:41 ******/
ALTER TABLE [pvhistory].[inverter]  WITH CHECK ADD  CONSTRAINT [InverterType_FK] FOREIGN KEY([InverterType_Id])
REFERENCES [pvhistory].[invertertype] ([Id])
GO
ALTER TABLE [pvhistory].[inverter] CHECK CONSTRAINT [InverterType_FK]
GO
/****** Object:  ForeignKey [OutputHistoryInverter_FK]    Script Date: 05/13/2011 22:49:41 ******/
ALTER TABLE [pvhistory].[outputhistory]  WITH CHECK ADD  CONSTRAINT [OutputHistoryInverter_FK] FOREIGN KEY([Inverter_Id])
REFERENCES [pvhistory].[inverter] ([Id])
GO
ALTER TABLE [pvhistory].[outputhistory] CHECK CONSTRAINT [OutputHistoryInverter_FK]
GO
