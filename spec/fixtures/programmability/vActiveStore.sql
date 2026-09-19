-- Result columns resolve, because every source is inside the model.
CREATE VIEW [app].[vActiveStore]
AS
SELECT [StoreId], [StoreNumber], [Name]
FROM [app].[Store];
