USE SubscriptionManagerDb;
GO

CREATE TRIGGER dbo.trg_Plans_SetUpdatedAt
ON dbo.Plans
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE p
    SET UpdatedAt = GETDATE()
    FROM dbo.Plans p
    JOIN inserted i ON p.PlanId = i.PlanId;
END
GO


CREATE TRIGGER dbo.trg_Subscriptions_SetEndDate
ON dbo.Subscriptions
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE s
    SET EndDate = DATEADD(DAY, p.DurationDays, s.StartDate)
    FROM dbo.Subscriptions s
    JOIN inserted i ON s.SubscriptionId = i.SubscriptionId
    JOIN dbo.Plans p ON s.PlanId = p.PlanId
    WHERE i.EndDate IS NULL;
END
GO
