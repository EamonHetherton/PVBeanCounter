ALTER TABLE [pvhistory].[invertertype]
    ADD CONSTRAINT [InverterTypeManModel_UK]
    UNIQUE (Manufacturer, Model)