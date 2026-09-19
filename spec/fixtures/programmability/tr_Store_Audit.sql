CREATE TRIGGER [app].[tr_Store_Audit]
    ON [app].[Store]
    AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
END
