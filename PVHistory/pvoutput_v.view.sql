CREATE VIEW [pvhistory].[pvoutput_v]
	AS SELECT OutputDay, FLOOR(OutputTime / 600) * 600 as OutputTime, sum(Energy)*1000 as Energy, 
	max(Power)*1000 as Power, min(Power)*1000 as MinPower
	from pvoutput_sub_v
	group by OutputDay, FLOOR(OutputTime / 600) * 600