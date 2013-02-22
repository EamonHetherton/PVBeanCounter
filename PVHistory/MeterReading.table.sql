CREATE TABLE [pvhistory].[meterreading]
(
	Meter_Id int NOT NULL, 
	Appliance int NOT NULL,
	ReadingTime DateTime NOT NULL,
	Duration int,
	Energy FLOAT,
	Temperature REAL,
	Calculated FLOAT,
	MinPower int,
	MaxPower int
)
