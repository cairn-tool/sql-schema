CREATE TABLE [app].[Store] (
    [StoreId]     INT           NOT NULL,
    [StoreNumber] INT           NOT NULL,
    [Name]        NVARCHAR (80) NOT NULL,
    CONSTRAINT [PK_app_Store] PRIMARY KEY ([StoreId])
);
