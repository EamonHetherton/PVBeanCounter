CREATE TABLE [pvhistory].[meterhistory]
(
	Meter_Id int NOT NULL, 
	Appliance int NOT NULL,
	ReadingTime DateTime NOT NULL,
	HistoryType CHAR NOT NULL,
	Duration int NOT NULL,
	Energy FLOAT NOT NULL
)
