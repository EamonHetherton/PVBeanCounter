CREATE VIEW [pvhistory].[fulldayoutputsummary_v]
	AS SELECT OutputDay, SUM(OutputKwh) OutputKwh
	from FullDayOutput_v
	group by OutputDay