ALTER TABLE [pvhistory].[meterhistory]
	ADD CONSTRAINT [MeterHistoryMeter_FK] 
	FOREIGN KEY (Meter_Id)
	REFERENCES [pvhistory].[meter] (Id)	

