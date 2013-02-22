ALTER TABLE [pvhistory].[meterreading]
	ADD CONSTRAINT [MeterReadingMeter_FK] 
	FOREIGN KEY (Meter_Id)
	REFERENCES [pvhistory].[meter] (Id)	

