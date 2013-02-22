CREATE VIEW [pvhistory].[fulldayoutputtimes_v]
	AS SELECT oh2.Inverter_Id, CAST(FLOOR(CAST(oh2.OutputTime AS float)) AS DATETIME) OutputDay, 
	SUM(oh2.OutputKwh) OutputKwh, DATEDIFF( second, CAST(FLOOR(CAST(MIN(oh2.OutputTime) AS float)) AS DATETIME), MIN(oh2.OutputTime)) StartTime, 
	DATEDIFF( second, CAST(FLOOR(CAST(MAX(oh2.OutputTime) AS float)) AS DATETIME), MAX(oh2.OutputTime)) EndTime
	from OutputHistory oh2, FullDayOutput_v fd
	where oh2.OutputKwh <> 0
	and oh2.Inverter_Id = fd.Inverter_id
	and CAST(FLOOR(CAST(oh2.OutputTime AS float)) AS DATETIME) = fd.OutputDay
	group by oh2.Inverter_Id, CAST(FLOOR(CAST(oh2.OutputTime AS float)) AS DATETIME)