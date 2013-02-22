ALTER TABLE [pvhistory].[invertermanager]
    ADD CONSTRAINT [InverterManager_UK]
    UNIQUE (ManagerType, InstanceNo)