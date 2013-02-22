CREATE TABLE [pvhistory].[meter]
(
	Id int IDENTITY NOT NULL, 
	MeterName VARCHAR(45) NOT NULL,
	MeterType VARCHAR(45) NOT NULL,
	StandardDuration int,
	CondensedDuration int,
	CondenseAge int,
	Enabled bit NOT NULL default 0
)
