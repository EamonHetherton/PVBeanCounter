ALTER TABLE [pvhistory].[outputhistory]
	ADD CONSTRAINT [OutputHistoryInverter_FK] 
	FOREIGN KEY (Inverter_Id)
	REFERENCES [pvhistory].[inverter] (Id)	

