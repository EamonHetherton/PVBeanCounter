CREATE VIEW [pvhistory].[outputhistory_v]
	AS SELECT Inverter_Id, OutputTime, OutputKwh, Duration, 
	CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME) OutputDate, 
	DATEDIFF( second, CAST(FLOOR(CAST(OutputTime AS float)) AS DATETIME), OutputTime) OutputTimeOfDay,
	(OutputKwh * 3600 / Duration ) 'OutputKw'
	from OutputHistory