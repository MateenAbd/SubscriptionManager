USE SubscriptionManagerDb;
GO


GO

-- Plans (10)
INSERT INTO dbo.Plans ([Name], [Description], Price, BillingCycle, DurationDays, Features, CreatedAt)
VALUES
('Basic Monthly', 'Great for starters', 9.99, 'Monthly', 30, N'{"users":1,"support":"email"}', GETDATE()),
('Standard Monthly', 'Balanced features', 19.99, 'Monthly', 30, N'{"users":3,"support":"chat"}', GETDATE()),
('Pro Monthly', 'For power users', 29.99, 'Monthly', 30, N'{"users":5,"support":"priority"}', GETDATE()),
('Basic Yearly', 'Monthly basic billed yearly', 99.00, 'Yearly', 365, N'{"users":1,"support":"email"}', GETDATE()),
('Standard Yearly', 'Monthly standard billed yearly', 199.00, 'Yearly', 365, N'{"users":3,"support":"chat"}', GETDATE()),
('Pro Yearly', 'Monthly pro billed yearly', 299.00, 'Yearly', 365, N'{"users":5,"support":"priority"}', GETDATE()),
('Team Monthly', 'For small teams', 49.99, 'Monthly', 30, N'{"users":10,"support":"priority"}', GETDATE()),
('Team Yearly', 'Team plan yearly', 499.00, 'Yearly', 365, N'{"users":10,"support":"priority"}', GETDATE()),
('Enterprise Monthly', 'Enterprise features monthly', 99.99, 'Monthly', 30, N'{"users":50,"support":"dedicated"}', GETDATE()),
('Enterprise Yearly', 'Enterprise features yearly', 999.00, 'Yearly', 365, N'{"users":50,"support":"dedicated"}', GETDATE());
GO

-- Users (20 total, 2 admins). PasswordHash seeded with sentinel.
INSERT INTO dbo.Users (FirstName, LastName, Email, Phone, [Address], [Role], PasswordHash, RegistrationDate)
VALUES
('Admin', 'One', 'admin@example.com', '5550000001', '100 Admin St', 'Admin', '$2a$12$gZq5xY/9ltq2QWInXpWigeB7U7Edfuem7QbQtgXpybG9XQzX7Kyby', GETDATE()), --Password for this admin is admin
('subscriber', 'Two', 'sub@example.com', '5550000002', '200 Sub St', 'Subscriber', '$2a$12$JMoltA5ix2rqz5G.XOBme.s4C1G1hJgYpRtrY71s5q0peilR/93gS', GETDATE()), --Password for this user is subscriber

('Alice', 'Johnson', 'alice.johnson@example.com', '5550001001', '10 Main St', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Bob', 'Smith', 'bob.smith@example.com', '5550001002', '20 Oak Ave', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Carol', 'Lee', 'carol.lee@example.com', '5550001003', '30 Pine Rd', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('David', 'Kim', 'david.kim@example.com', '5550001004', '40 Cedar Ln', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Eva', 'Brown', 'eva.brown@example.com', '5550001005', '50 Maple Dr', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Frank', 'Davis', 'frank.davis@example.com', '5550001006', '60 Birch Pl', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Grace', 'Wilson', 'grace.wilson@example.com', '5550001007', '70 Spruce Ct', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Henry', 'Moore', 'henry.moore@example.com', '5550001008', '80 Elm St', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Ivy', 'Taylor', 'ivy.taylor@example.com', '5550001009', '90 Lakeview', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Jake', 'Anderson', 'jake.anderson@example.com', '5550001010', '101 Hill Rd', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Kara', 'Thomas', 'kara.thomas@example.com', '5550001011', '102 River Rd', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Liam', 'Martin', 'liam.martin@example.com', '5550001012', '103 Valley Rd', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Mia', 'Jackson', 'mia.jackson@example.com', '5550001013', '104 Park Ave', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Noah', 'White', 'noah.white@example.com', '5550001014', '105 Lake St', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Olivia', 'Harris', 'olivia.harris@example.com', '5550001015', '106 Forest Dr', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Paul', 'Clark', 'paul.clark@example.com', '5550001016', '107 Meadow Ln', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Quinn', 'Lewis', 'quinn.lewis@example.com', '5550001017', '108 Sunset Blvd', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE()),
('Ruth', 'Young', 'ruth.young@example.com', '5550001018', '109 Sunrise Ave', 'Subscriber', 'BCRYPT:P@ssw0rd!', GETDATE());
GO

-- Subscriptions (30)
-- Create varied subscriptions across users and plans with different statuses
;WITH UserIds AS (
    SELECT TOP 20 UserId FROM dbo.Users ORDER BY UserId
),
PlanIds AS (
    SELECT TOP 10 PlanId FROM dbo.Plans ORDER BY PlanId
),
Base AS (
    SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn,
           u.UserId,
           p.PlanId
    FROM UserIds u
    CROSS JOIN PlanIds p
)
INSERT INTO dbo.Subscriptions (UserId, PlanId, StartDate, EndDate, RenewalDate, [Status], AutoRenew)
SELECT
    b.UserId,
    b.PlanId,
    DATEADD(DAY, -((b.rn * 3) % 90), GETDATE()) AS StartDate,
    DATEADD(DAY, p.DurationDays, DATEADD(DAY, -((b.rn * 3) % 90), GETDATE())) AS EndDate,
    NULL AS RenewalDate,
    CASE 
        WHEN b.rn % 10 = 0 THEN 'Cancelled'
        WHEN b.rn % 7 = 0 THEN 'Expired'
        ELSE 'Active'
    END AS [Status],
    CASE WHEN b.rn % 2 = 0 THEN 1 ELSE 0 END AS AutoRenew
FROM Base b
JOIN dbo.Plans p ON p.PlanId = b.PlanId
WHERE b.rn <= 30;
GO

-- Payments (25) for first 25 subscriptions
INSERT INTO dbo.Payments (SubscriptionId, Amount, PaymentDate, PaymentMethod, TransactionId, [Status])
SELECT TOP 25
    s.SubscriptionId,
    p.Price,
    DATEADD(DAY, ((s.SubscriptionId % 15) - 7), s.StartDate) AS PaymentDate,
    CASE WHEN s.SubscriptionId % 3 = 0 THEN 'PayPal' ELSE 'Credit Card' END AS PaymentMethod,
    CONVERT(NVARCHAR(100), NEWID()) AS TransactionId,
    CASE WHEN s.SubscriptionId % 13 = 0 THEN 'Refunded' ELSE 'Completed' END AS [Status]
FROM dbo.Subscriptions s
JOIN dbo.Plans p ON s.PlanId = p.PlanId
ORDER BY s.SubscriptionId;
GO

-- Logs (50)
;WITH nums AS (
    SELECT TOP 50 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects
)
INSERT INTO dbo.Logs (UserId, [Action], [Message], LogDate)
SELECT
    CASE WHEN n % 5 = 0 THEN NULL ELSE ((n % 20) + 1) END AS UserId,
    CASE 
        WHEN n % 6 = 0 THEN 'PaymentProcessed'
        WHEN n % 5 = 0 THEN 'SubscriptionCancelled'
        WHEN n % 4 = 0 THEN 'SubscriptionRenewed'
        WHEN n % 3 = 0 THEN 'SubscriptionCreated'
        WHEN n % 2 = 0 THEN 'UserLogin'
        ELSE 'UserRegistered'
    END AS [Action],
    CONCAT('Seed log entry #', n, ' at ', CONVERT(VARCHAR(19), GETDATE(), 120)) AS [Message],
    DATEADD(DAY, -((n % 30)), GETDATE()) AS LogDate
FROM nums;
GO