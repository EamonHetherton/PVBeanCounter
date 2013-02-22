ALTER TABLE [pvhistory].[outputhistory]
	ADD CONSTRAINT [OutputHistory_PK]
	PRIMARY KEY (Inverter_Id, OutputTime)