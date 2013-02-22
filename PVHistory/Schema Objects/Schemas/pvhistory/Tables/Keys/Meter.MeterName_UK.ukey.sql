ALTER TABLE [pvhistory].[meter]
    ADD CONSTRAINT [MeterName_UK]
    UNIQUE (MeterName)