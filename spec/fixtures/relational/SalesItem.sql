CREATE TABLE [items].[SalesItem] (
    [SalesItemId] BIGINT        IDENTITY (1, 1) NOT NULL,
    [Sku]         VARCHAR (50)  NOT NULL,
    [Description] VARCHAR (255) NULL,
    CONSTRAINT [PK_items_SalesItem] PRIMARY KEY ([SalesItemId]),
    CONSTRAINT [UQ_items_SalesItem_Sku] UNIQUE ([Sku])
);
