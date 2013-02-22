CREATE TABLE [pvhistory].[inverter]
(
	Id int IDENTITY NOT NULL, 
	SerialNumber VARCHAR(45) NOT NULL,
	Location VARCHAR(255),
	InverterType_Id int NOT NULL,
	InverterManager_Id int
)
