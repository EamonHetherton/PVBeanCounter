ALTER TABLE [pvhistory].[version]
	ADD CONSTRAINT [Version_PK]
	PRIMARY KEY (Major, Minor, Release, Patch)