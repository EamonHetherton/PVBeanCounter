CREATE TABLE [pvhistory].[invertermanager]
(
	Id int IDENTITY NOT NULL, 
	ManagerType VARCHAR(50) Not NULL,
	InstanceNo int NOT NULL,
	NextFileDate DateTime,
	ConfigFileName VARCHAR(255),
	OutputDirectory VARCHAR(255),
	ArchiveDirectory VARCHAR(255),
	Password VARCHAR(20),
	Frequency int,
	Enabled bit NOT NULL default 0,
	FileNamePattern VARCHAR(255)
)
