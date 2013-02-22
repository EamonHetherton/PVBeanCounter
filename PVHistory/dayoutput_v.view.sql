CREATE VIEW [pvhistory].[dayoutput_v]
	AS SELECT oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME) OutputDay, SUM(oh.OutputKwh) OutputKwh
	from OutputHistory oh
	group by oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME)