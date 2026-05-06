-- Federation outbox: per-cluster durable queue of clinical event envelopes
-- pending replication to upstream sites. Drained by OutboxDrainerService.
--
-- Idempotent — safe to run on every silo startup.

IF OBJECT_ID(N'[FederationOutbox]', 'U') IS NULL
BEGIN
    CREATE TABLE [FederationOutbox] (
        [EventId]            VARCHAR(64)   NOT NULL,
        [PatientId]          VARCHAR(128)  NOT NULL,
        [Domain]             VARCHAR(64)   NOT NULL,
        [EventType]          VARCHAR(128)  NOT NULL,
        [OccurredUtc]        DATETIME2     NOT NULL,
        [SourceClusterId]    VARCHAR(64)   NOT NULL,
        [EventHash]          VARCHAR(128)  NOT NULL,
        [PreviousEventHash]  VARCHAR(128)  NOT NULL,
        [EnvelopeBlob]       VARBINARY(MAX) NOT NULL,
        [EnqueuedUtc]        DATETIME2     NOT NULL CONSTRAINT [DF_FederationOutbox_EnqueuedUtc]    DEFAULT SYSUTCDATETIME(),
        [SentUtc]            DATETIME2     NULL,
        [Attempts]           INT           NOT NULL CONSTRAINT [DF_FederationOutbox_Attempts]       DEFAULT 0,
        [LastError]          NVARCHAR(2000) NULL,
        [NextAttemptUtc]     DATETIME2     NOT NULL CONSTRAINT [DF_FederationOutbox_NextAttemptUtc] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_FederationOutbox] PRIMARY KEY CLUSTERED ([EventId])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FederationOutbox_Pending' AND object_id = OBJECT_ID(N'[FederationOutbox]'))
BEGIN
    CREATE INDEX [IX_FederationOutbox_Pending]
        ON [FederationOutbox] ([NextAttemptUtc])
        WHERE [SentUtc] IS NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FederationOutbox_Patient' AND object_id = OBJECT_ID(N'[FederationOutbox]'))
BEGIN
    CREATE INDEX [IX_FederationOutbox_Patient]
        ON [FederationOutbox] ([PatientId]);
END
GO
