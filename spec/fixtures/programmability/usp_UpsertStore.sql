-- An OUTPUT parameter (mode inOut), a table-valued parameter (readOnly), and a parameter with a
-- default. A procedure's result set is not in the model at any fidelity, so resultColumns is [].
CREATE PROCEDURE [app].[usp_UpsertStore]
    @StoreId     INT,
    @Name        NVARCHAR (80) = N'unnamed',
    @Stores      [app].[StoreIdList] READONLY,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @RowsAffected = 0;
END
