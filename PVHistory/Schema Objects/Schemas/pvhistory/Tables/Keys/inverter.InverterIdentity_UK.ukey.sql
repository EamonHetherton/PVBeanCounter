ALTER TABLE [pvhistory].[inverter]
    ADD CONSTRAINT [InverterIdentity_UK]
    UNIQUE (SerialNumber, InverterType_Id)