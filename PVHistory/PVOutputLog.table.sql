CREATE TABLE [pvhistory].[pvoutputlog]
(
	SiteId VARCHAR(10) NOT NULL, 
	OutputDay DATE NOT NULL,
	OutputTime int NOT NULL,
	Energy FLOAT,
	[Power] FLOAT,
	Loaded bit NOT NULL,
	ImportEnergy FLOAT,
	ImportPower FLOAT
)
