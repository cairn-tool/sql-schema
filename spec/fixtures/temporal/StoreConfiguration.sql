-- Named DEFAULT constraints are declared at the column level -- T-SQL permits them nowhere else in
-- a CREATE TABLE -- but the document models every one of them as an entry in the table's
-- `constraints`, because they are named and those names appear in deploy scripts and drift reports.
CREATE TABLE [cfg].[StoreConfiguration] (
    [StoreConfigurationId]     BIGINT             IDENTITY (1, 1) NOT NULL,
    [StoreNumber]              INT                NOT NULL,
    [EffectiveDate]            DATE               NOT NULL,
    [ExpirationDate]           DATE               NULL,
    [ConfigurationData]        VARCHAR (MAX)      NOT NULL,
    [StoreStatusFlags]         INT                CONSTRAINT [DF_cfg_StoreConfiguration_StoreStatusFlags] DEFAULT ((0)) NOT NULL,
    [ConfigurationDataVersion] INT                CONSTRAINT [DF_cfg_StoreConfiguration_ConfigurationDataVersion] DEFAULT ((0)) NOT NULL,
    [RowCreatedDateTime]       DATETIMEOFFSET (7) CONSTRAINT [DF_cfg_StoreConfiguration_RowCreatedDateTime] DEFAULT (SYSDATETIMEOFFSET()) NOT NULL,
    [RowCreatedBy]             VARCHAR (125)      CONSTRAINT [DF_cfg_StoreConfiguration_RowCreatedBy] DEFAULT (SUSER_SNAME()) NOT NULL,
    [RowModifiedDateTime]      DATETIMEOFFSET (7) CONSTRAINT [DF_cfg_StoreConfiguration_RowModifiedDateTime] DEFAULT (SYSDATETIMEOFFSET()) NOT NULL,
    [RowModifiedBy]            VARCHAR (125)      CONSTRAINT [DF_cfg_StoreConfiguration_RowModifiedBy] DEFAULT (SUSER_SNAME()) NOT NULL,
    [ValidFrom]                DATETIME2          GENERATED ALWAYS AS ROW START NOT NULL,
    [ValidTo]                  DATETIME2          GENERATED ALWAYS AS ROW END NOT NULL,
    CONSTRAINT [PK_cfg_StoreConfigurationId] PRIMARY KEY ([StoreConfigurationId]),
    PERIOD FOR SYSTEM_TIME ([ValidFrom], [ValidTo])
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [cfg].[StoreConfigurationHistory]));
