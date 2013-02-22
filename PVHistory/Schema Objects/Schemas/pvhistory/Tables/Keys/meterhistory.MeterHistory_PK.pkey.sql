ALTER TABLE [pvhistory].[meterhistory]
	ADD CONSTRAINT [MeterHistory_PK]
	PRIMARY KEY (Meter_Id, Appliance, ReadingTime, HistoryType)