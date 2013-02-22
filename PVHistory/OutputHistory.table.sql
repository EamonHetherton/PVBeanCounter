CREATE TABLE [pvhistory].[outputhistory]
(
	Inverter_Id int NOT NULL, 
	OutputTime DATETIME NOT NULL,
	OutputKwh FLOAT NOT NULL,
	Duration int NOT NULL
)
