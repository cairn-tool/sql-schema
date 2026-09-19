CREATE FUNCTION [app].[fn_StoresIn] (@StoreNumber INT)
RETURNS TABLE
AS
RETURN (SELECT [StoreId], [Name] FROM [app].[Store] WHERE [StoreNumber] = @StoreNumber);
