-- A one-to-many foreign key with NO ACTION, a composite UNIQUE, and a nullable foreign key
-- column -- which is what makes the relationship optional under the specification's derivation.
CREATE TABLE [items].[SalesItemDetail] (
    [SalesItemDetailId] BIGINT NOT NULL,
    [SalesItemId]       BIGINT NOT NULL,
    [EffectiveDate]     DATE   NOT NULL,
    [SupersedesId]      BIGINT NULL,
    CONSTRAINT [PK_items_SalesItemDetail] PRIMARY KEY ([SalesItemDetailId]),
    CONSTRAINT [UQ_items_SalesItemDetail] UNIQUE ([SalesItemId], [EffectiveDate]),
    CONSTRAINT [FK_items_SalesItemDetail_SalesItem] FOREIGN KEY ([SalesItemId])
        REFERENCES [items].[SalesItem] ([SalesItemId]),
    CONSTRAINT [FK_items_SalesItemDetail_Self] FOREIGN KEY ([SupersedesId])
        REFERENCES [items].[SalesItemDetail] ([SalesItemDetailId])
);
GO

-- Declared in its own batch, which the document must render indistinguishably from an inline one.
CREATE NONCLUSTERED INDEX [IX_items_SalesItemDetail_Effective]
    ON [items].[SalesItemDetail] ([EffectiveDate] DESC)
    INCLUDE ([SalesItemId])
    WHERE [SupersedesId] IS NULL;
