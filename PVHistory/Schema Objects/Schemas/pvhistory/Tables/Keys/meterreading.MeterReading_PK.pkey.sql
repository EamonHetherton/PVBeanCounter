ALTER TABLE [pvhistory].[meterreading]
	ADD CONSTRAINT [MeterReading_PK]
	PRIMARY KEY (Meter_Id, Appliance, ReadingTime)