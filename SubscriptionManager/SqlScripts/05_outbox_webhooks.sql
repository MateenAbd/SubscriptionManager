USE SubscriptionManagerDb;
GO

CREATE TABLE dbo.OutboxMessages
(
    OutboxId     BIGINT IDENTITY(1,1) PRIMARY KEY,
    [Type]       NVARCHAR(100) NOT NULL,
    Payload      NVARCHAR(MAX) NOT NULL,
    CreatedAt    DATETIME NOT NULL DEFAULT (GETDATE()),
    ProcessedAt  DATETIME NULL,
    Attempts     INT NOT NULL DEFAULT (0),
    [Error]      NVARCHAR(1000) NULL
);
GO

CREATE NONCLUSTERED INDEX IX_Outbox_Processed 
    ON dbo.OutboxMessages (ProcessedAt, CreatedAt);
GO


CREATE TABLE dbo.WebhookEvents
(
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    EventId      NVARCHAR(100) NOT NULL UNIQUE,
    Signature    NVARCHAR(200) NULL,
    RawPayload   NVARCHAR(MAX) NOT NULL,
    ReceivedAt   DATETIME NOT NULL DEFAULT (GETDATE()),
    Processed    BIT NOT NULL DEFAULT (0)
);
GO
