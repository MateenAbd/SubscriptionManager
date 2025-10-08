
CREATE DATABASE SubscriptionManagerDb;
GO

USE SubscriptionManagerDb;
GO

CREATE TABLE dbo.Plans
(
    PlanId        INT IDENTITY(1,1) PRIMARY KEY,
    [Name]        NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    Price         DECIMAL(10,2) NOT NULL CONSTRAINT CK_Plans_Price CHECK (Price >= 0),
    BillingCycle  NVARCHAR(20) NOT NULL,
    DurationDays  INT NOT NULL CONSTRAINT CK_Plans_Duration CHECK (DurationDays > 0),
    Features      NVARCHAR(MAX) NULL,
    CreatedAt     DATETIME NOT NULL CONSTRAINT DF_Plans_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt     DATETIME NULL,
    CONSTRAINT UQ_Plans_Name UNIQUE ([Name])
);
GO

CREATE TABLE dbo.Users
(
    UserId           INT IDENTITY(1,1) PRIMARY KEY,
    FirstName        NVARCHAR(50) NOT NULL,
    LastName         NVARCHAR(50) NOT NULL,
    Email            NVARCHAR(100) NOT NULL,
    Phone            VARCHAR(15) NULL,
    [Address]        NVARCHAR(200) NULL,
    RegistrationDate DATETIME NOT NULL CONSTRAINT DF_Users_RegistrationDate DEFAULT (GETDATE()),
    [Role]           NVARCHAR(20) NOT NULL CONSTRAINT DF_Users_Role DEFAULT ('Subscriber'),
    PasswordHash     NVARCHAR(256) NOT NULL,
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
GO

CREATE TABLE dbo.Subscriptions
(
    SubscriptionId INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT NOT NULL,
    PlanId         INT NOT NULL,
    StartDate      DATETIME NOT NULL CONSTRAINT DF_Subscriptions_StartDate DEFAULT (GETDATE()),
    EndDate        DATETIME NOT NULL,
    RenewalDate    DATETIME NULL,
    [Status]       NVARCHAR(20) NOT NULL CONSTRAINT DF_Subscriptions_Status DEFAULT ('Active'),
    AutoRenew      BIT NOT NULL CONSTRAINT DF_Subscriptions_AutoRenew DEFAULT (1),
    CONSTRAINT FK_Subscriptions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Subscriptions_Plans FOREIGN KEY (PlanId) REFERENCES dbo.Plans(PlanId),
    CONSTRAINT CK_Subscriptions_Status CHECK ([Status] IN ('Active','Cancelled','Expired'))
);
GO

CREATE TABLE dbo.Payments
(
    PaymentId     INT IDENTITY(1,1) PRIMARY KEY,
    SubscriptionId INT NOT NULL,
    Amount        DECIMAL(10,2) NOT NULL CONSTRAINT CK_Payments_Amount CHECK (Amount >= 0),
    PaymentDate   DATETIME NOT NULL CONSTRAINT DF_Payments_PaymentDate DEFAULT (GETDATE()),
    PaymentMethod NVARCHAR(50) NULL,
    TransactionId NVARCHAR(100) NOT NULL,
    [Status]      NVARCHAR(20) NOT NULL CONSTRAINT DF_Payments_Status DEFAULT ('Completed'),
    CONSTRAINT UQ_Payments_TransactionId UNIQUE (TransactionId),
    CONSTRAINT FK_Payments_Subscriptions FOREIGN KEY (SubscriptionId) REFERENCES dbo.Subscriptions(SubscriptionId),
    CONSTRAINT CK_Payments_Status CHECK ([Status] IN ('Completed','Failed','Refunded'))
);
GO

CREATE TABLE dbo.Logs
(
    LogId    INT IDENTITY(1,1) PRIMARY KEY,
    UserId   INT NULL,
    [Action] NVARCHAR(100) NOT NULL,
    [Message] NVARCHAR(MAX) NULL,
    LogDate  DATETIME NOT NULL CONSTRAINT DF_Logs_LogDate DEFAULT (GETDATE()),
    CONSTRAINT FK_Logs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO


CREATE NONCLUSTERED INDEX IX_Users_Email ON dbo.Users (Email);
CREATE NONCLUSTERED INDEX IX_Subscriptions_StartDate ON dbo.Subscriptions (StartDate);
CREATE NONCLUSTERED INDEX IX_Subscriptions_UserId ON dbo.Subscriptions (UserId);
CREATE NONCLUSTERED INDEX IX_Subscriptions_PlanId ON dbo.Subscriptions (PlanId);
CREATE NONCLUSTERED INDEX IX_Payments_PaymentDate ON dbo.Payments (PaymentDate);
CREATE NONCLUSTERED INDEX IX_Logs_LogDate ON dbo.Logs (LogDate);
GO


CREATE TYPE dbo.UserTableType AS TABLE
(
    FirstName    NVARCHAR(50) NOT NULL,
    LastName     NVARCHAR(50) NOT NULL,
    Email        NVARCHAR(100) NOT NULL,
    Phone        VARCHAR(15) NULL,
    [Address]    NVARCHAR(200) NULL,
    [Role]       NVARCHAR(20) NOT NULL,
    PasswordHash NVARCHAR(256) NOT NULL
);
GO