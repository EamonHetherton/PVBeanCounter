CREATE VIEW [pvhistory].[pvoutput_sub_v]
	AS select CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME) as OutputDay, 
	DATEDIFF( second, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME), oh.OutputTime) as OutputTime,
	sum(oh.OutputKwh) as Energy, sum(oh.OutputKwh*3600/oh.Duration) As Power
	from outputhistory as oh
	where oh.OutputTime >= (GETDATE() - 20)
	and oh.OutputKwh > 0 
	group by CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME), DATEDIFF( second, CAST(FLOOR(CAST(oh.OutputTime AS float)) AS DATETIME), oh.OutputTime)
