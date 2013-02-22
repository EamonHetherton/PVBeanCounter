CREATE VIEW [pvhistory].[fulldayoutput_v]
	AS SELECT oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME) OutputDay, SUM(oh.OutputKwh) OutputKwh
	from OutputHistory oh
	group by oh.Inverter_Id, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME)
	having SUM(oh.Duration) = (60 * 60 * 24)