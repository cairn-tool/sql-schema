-- The domain meaning below lives only in these comments, which a compiled model does not keep.
-- That is what the annotation sidecar exists for; see annotations.json beside this file.
CREATE TABLE [cfg].[StoreStatus] (
    [StoreStatusId]    INT      NOT NULL,
    [StoreStatusFlags] INT      NOT NULL, -- bit flags { 1: ingesting, 2: forecasting, 4: scheduling }
    [StatusCode]       CHAR (1) NOT NULL, -- A=Active / I=Inactive
    CONSTRAINT [PK_cfg_StoreStatus] PRIMARY KEY ([StoreStatusId])
);
