ALTER TABLE [pvhistory].[inverter]
	ADD CONSTRAINT [InverterType_FK] 
	FOREIGN KEY (InverterType_Id)
	REFERENCES [pvhistory].[invertertype] (Id)	

