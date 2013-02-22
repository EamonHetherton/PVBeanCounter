CREATE TABLE [pvhistory].[invertertype]
(
	Id int IDENTITY NOT NULL, 
	Manufacturer VARCHAR(60) NOT NULL,
	Model VARCHAR(50) NOT NULL,
	MaxOutput int
)
