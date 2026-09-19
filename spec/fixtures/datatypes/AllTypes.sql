-- One column per row of the specification's parameterization table, so that `rendered` is
-- exercised end to end and the "effective, not authored" rule is visible: DATETIME2 with no
-- scale must come back as datetime2(7), and DECIMAL(15) as decimal(15,0).
CREATE TABLE [dt].[AllTypes] (
    [Id]               INT                NOT NULL,

    -- length
    [Char1]            CHAR (1)           NULL,
    [Varchar50]        VARCHAR (50)       NULL,
    [VarcharMax]       VARCHAR (MAX)      NULL,
    [NChar4]           NCHAR (4)          NULL,
    [NVarchar255]      NVARCHAR (255)     NULL,
    [NVarcharMax]      NVARCHAR (MAX)     NULL,
    [Binary8]          BINARY (8)         NULL,
    [VarbinaryMax]     VARBINARY (MAX)    NULL,

    -- precision + scale
    [Decimal84]        DECIMAL (8, 4)     NULL,
    [Decimal15]        DECIMAL (15)       NULL,
    [Numeric102]       NUMERIC (10, 2)    NULL,

    -- precision only
    [Float53]          FLOAT (53)         NULL,

    -- scale only
    [Datetime2Default] DATETIME2          NULL,
    [Datetime2Scale3]  DATETIME2 (3)      NULL,
    [DatetimeOffset7]  DATETIMEOFFSET (7) NULL,
    [Time3]            TIME (3)           NULL,

    -- no parameters
    [BigintCol]        BIGINT             NULL,
    [SmallintCol]      SMALLINT           NULL,
    [TinyintCol]       TINYINT            NULL,
    [BitCol]           BIT                NULL,
    [DateCol]          DATE               NULL,
    [DatetimeCol]      DATETIME           NULL,
    [SmallDatetimeCol] SMALLDATETIME      NULL,
    [MoneyCol]         MONEY              NULL,
    [SmallMoneyCol]    SMALLMONEY         NULL,
    [RealCol]          REAL               NULL,
    [GuidCol]          UNIQUEIDENTIFIER   NULL,
    [XmlCol]           XML                NULL,
    [VariantCol]       SQL_VARIANT        NULL,

    -- a column-level collation override, which must not appear in `rendered`
    [Collated]         VARCHAR (20) COLLATE SQL_Latin1_General_CP1_CS_AS NULL,

    -- a user-defined alias type, where `name` carries the type's id instead of a base type name
    [Account]          [dt].[AccountNumber] NOT NULL,

    -- computed, persisted and not
    [Persisted]        AS ([Id] * 2) PERSISTED,
    [NotPersisted]     AS ([Id] + 1),

    CONSTRAINT [PK_dt_AllTypes] PRIMARY KEY ([Id])
);
