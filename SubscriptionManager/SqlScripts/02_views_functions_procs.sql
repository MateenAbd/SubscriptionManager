USE SubscriptionManagerDb;
GO


CREATE FUNCTION dbo.fn_IsSubscriptionActive (@SubscriptionId INT)
RETURNS BIT
AS
BEGIN
    DECLARE @result BIT = 0;
    IF EXISTS (
        SELECT 1
        FROM dbo.Subscriptions s
        WHERE s.SubscriptionId = @SubscriptionId
          AND s.[Status] = 'Active'
          AND s.EndDate > GETDATE()
    )
        SET @result = 1;
    RETURN @result;
END
GO

CREATE FUNCTION dbo.fn_CalculateRevenue (@Start DATETIME, @End DATETIME)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @total DECIMAL(18,2) = 0.00;
    SELECT @total = COALESCE(SUM(p.Amount), 0.00)
    FROM dbo.Payments p
    WHERE p.PaymentDate >= @Start
      AND p.PaymentDate < DATEADD(DAY, 1, @End)
      AND p.[Status] = 'Completed';
    RETURN @total;
END
GO




CREATE VIEW dbo.vw_ActiveSubscriptions
AS
SELECT
    s.SubscriptionId,
    s.UserId,
    u.FirstName,
    u.LastName,
    u.Email,
    s.PlanId,
    p.[Name] AS PlanName,
    p.BillingCycle,
    s.StartDate,
    s.EndDate,
    s.AutoRenew
FROM dbo.Subscriptions s
JOIN dbo.Users u ON s.UserId = u.UserId
JOIN dbo.Plans p ON s.PlanId = p.PlanId
WHERE s.[Status] = 'Active';
GO

CREATE VIEW dbo.vw_UserPaymentHistory
AS
SELECT
    u.UserId,
    u.FirstName,
    u.LastName,
    u.Email,
    s.SubscriptionId,
    pmt.PaymentId,
    pmt.Amount,
    pmt.PaymentDate,
    pmt.PaymentMethod,
    pmt.TransactionId,
    pmt.[Status] AS PaymentStatus,
    s.PlanId,
    pl.[Name] AS PlanName
FROM dbo.Users u
JOIN dbo.Subscriptions s ON u.UserId = s.UserId
JOIN dbo.Payments pmt ON s.SubscriptionId = pmt.SubscriptionId
JOIN dbo.Plans pl ON s.PlanId = pl.PlanId;
GO


-- Upsert Plan by Name,Returns PlanId.
CREATE PROCEDURE dbo.sp_InsertPlan
    @Name         NVARCHAR(100),
    @Description  NVARCHAR(MAX) = NULL,
    @Price        DECIMAL(10,2),
    @BillingCycle NVARCHAR(20),
    @DurationDays INT,
    @Features     NVARCHAR(MAX) = NULL,
    @PlanId       INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @output TABLE (PlanId INT);

    MERGE dbo.Plans AS tgt
    USING (SELECT @Name AS [Name]) AS src
    ON (tgt.[Name] = src.[Name])
    WHEN MATCHED THEN
        UPDATE SET
            [Description] = @Description,
            Price = @Price,
            BillingCycle = @BillingCycle,
            DurationDays = @DurationDays,
            Features = @Features,
            UpdatedAt = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT ([Name], [Description], Price, BillingCycle, DurationDays, Features, CreatedAt)
        VALUES (@Name, @Description, @Price, @BillingCycle, @DurationDays, @Features, GETDATE())
    OUTPUT inserted.PlanId INTO @output;

    SELECT @PlanId = PlanId FROM @output;
END
GO


CREATE PROCEDURE dbo.sp_UpdateSubscriptionStatus
    @SubscriptionId INT,
    @NewStatus NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    IF (@NewStatus NOT IN ('Active','Cancelled','Expired'))
    BEGIN
        RAISERROR('Invalid status', 16, 1);
        RETURN;
    END

    BEGIN TRAN;
    UPDATE dbo.Subscriptions
    SET [Status] = @NewStatus
    WHERE SubscriptionId = @SubscriptionId;
    COMMIT TRAN;
END
GO


CREATE PROCEDURE dbo.sp_GetExpiredSubscriptions
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.PlanId,
        p.[Name] AS PlanName,
        COUNT(*) AS ExpiredCount
    FROM dbo.Subscriptions s
    JOIN dbo.Plans p ON s.PlanId = p.PlanId
    WHERE s.[Status] = 'Expired' OR s.EndDate <= GETDATE()
    GROUP BY s.PlanId, p.[Name]
    ORDER BY ExpiredCount DESC, PlanName;
END
GO

-- Insert Log
CREATE PROCEDURE dbo.sp_InsertLog
    @UserId INT = NULL,
    @Action NVARCHAR(100),
    @Message NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Logs (UserId, [Action], [Message], LogDate)
    VALUES (@UserId, @Action, @Message, GETDATE());
END
GO

-- Bulk insert users (using UDT)
CREATE PROCEDURE dbo.sp_BulkInsertUsers
    @Users dbo.UserTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
