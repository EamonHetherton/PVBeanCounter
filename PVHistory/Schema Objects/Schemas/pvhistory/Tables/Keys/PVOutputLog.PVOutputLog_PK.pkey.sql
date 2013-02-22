ALTER TABLE [pvhistory].[pvoutputlog]
	ADD CONSTRAINT [PVOutputLog_PK]
	PRIMARY KEY (SiteId, OutputDay, OutputTime)