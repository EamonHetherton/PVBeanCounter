CREATE VIEW [pvhistory].[pvoutput5min_v]
	AS SELECT OutputDay, FLOOR(OutputTime / 300) * 300 as OutputTime, sum(Energy)*1000 as Energy, 
	max(Power)*1000 as Power, min(Power)*1000 as MinPower
	from pvoutput_sub_v
	group by OutputDay, FLOOR(OutputTime / 300) * 300