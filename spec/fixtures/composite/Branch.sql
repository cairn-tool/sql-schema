-- References a table that lives in the referenced package, not in this one. Under the default
-- extraction that table is absent from the document; under --include-referenced it is present and
-- attributed with extensions.sqlserver.fromReference.
CREATE TABLE [local].[Branch] (
    [BranchId] INT NOT NULL,
    [RegionId] INT NOT NULL,
    CONSTRAINT [PK_local_Branch] PRIMARY KEY ([BranchId]),
    CONSTRAINT [FK_local_Branch_Region] FOREIGN KEY ([RegionId])
        REFERENCES [shared].[Region] ([RegionId])
);
