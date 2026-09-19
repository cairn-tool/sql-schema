CREATE FUNCTION [app].[fn_StoreName] (@StoreId INT)
RETURNS NVARCHAR (80)
AS
BEGIN
    RETURN (SELECT [Name] FROM [app].[Store] WHERE [StoreId] = @StoreId);
END
