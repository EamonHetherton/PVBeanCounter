ALTER TABLE [pvhistory].[inverter]
	ADD CONSTRAINT [InverterInverterManager_FK] 
	FOREIGN KEY (InverterManager_Id)
	REFERENCES [pvhistory].[invertermanager] (Id)	

