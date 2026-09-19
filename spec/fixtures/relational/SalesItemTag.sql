-- ON DELETE CASCADE, so both branches of the referential-action mapping are exercised. The
-- foreign key's columns are exactly this table's primary key, which is what the specification
-- calls one-to-one.
CREATE TABLE [items].[SalesItemTag] (
    [SalesItemId] BIGINT        NOT NULL,
    [Tag]         VARCHAR (40)  NOT NULL,
    CONSTRAINT [PK_items_SalesItemTag] PRIMARY KEY ([SalesItemId]),
    CONSTRAINT [FK_items_SalesItemTag_SalesItem] FOREIGN KEY ([SalesItemId])
        REFERENCES [items].[SalesItem] ([SalesItemId]) ON DELETE CASCADE ON UPDATE NO ACTION,
    CONSTRAINT [CK_items_SalesItemTag_Tag] CHECK (LEN([Tag]) > 0)
);
