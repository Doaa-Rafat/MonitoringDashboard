IF DB_ID(N'MonitoringDb') IS NULL
    CREATE DATABASE MonitoringDb;
GO

USE MonitoringDb;
GO

/* ---------- Roles ---------- */
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
CREATE TABLE dbo.Roles (
    RoleId      INT IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(50)  NOT NULL,
    Description NVARCHAR(250) NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_Name UNIQUE (Name)
);
GO

/* ---------- Users ---------- */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users (
    UserId             INT IDENTITY(1,1) NOT NULL,
    UserName           NVARCHAR(50)  NOT NULL,
    Email              NVARCHAR(100) NOT NULL,
    PasswordHash       NVARCHAR(500) NOT NULL,
    RefreshToken       NVARCHAR(500) NULL,        -- store a HASH of the token, not the raw value
    RefreshTokenExpiry DATETIME2(3)  NULL,
    CreatedAt          DATETIME2(3)  NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_UserName UNIQUE (UserName),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
GO

/* ---------- UserRoles (many-to-many Users <-> Roles) ---------- */
IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
CREATE TABLE dbo.UserRoles (
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE NO ACTION,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId) ON DELETE NO ACTION,
    INDEX IX_UserRoles_RoleId NONCLUSTERED (RoleId)
);
GO

/* ---------- Servers ---------- */
IF OBJECT_ID(N'dbo.Servers', N'U') IS NULL
CREATE TABLE dbo.Servers (
    ServerId    INT IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    IPAddress   NVARCHAR(50)  NULL,
    Status      NVARCHAR(20)  NOT NULL CONSTRAINT DF_Servers_Status DEFAULT N'Up',
    Description NVARCHAR(250) NULL,
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Servers_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Servers PRIMARY KEY (ServerId),
    CONSTRAINT UQ_Servers_Name UNIQUE (Name),
    CONSTRAINT CK_Servers_Status CHECK (Status IN (N'Up', N'Down'))
);
GO

/* ---------- Metrics (grows fast: one row per server per collection) ---------- */
IF OBJECT_ID(N'dbo.Metrics', N'U') IS NULL
CREATE TABLE dbo.Metrics (
    MetricId     INT IDENTITY(1,1) NOT NULL,
    ServerId     INT NOT NULL,
    CpuUsage     FLOAT NOT NULL,
    MemoryUsage  FLOAT NOT NULL,
    DiskUsage    FLOAT NOT NULL,
    ResponseTime FLOAT NOT NULL,              -- milliseconds
    NetworkInKbps  FLOAT NULL,                 -- simulated inbound throughput; NULL for pre-upgrade history
    NetworkOutKbps FLOAT NULL,                 -- simulated outbound throughput; NULL for pre-upgrade history
    Status       NVARCHAR(20) NOT NULL,
    Timestamp    DATETIME2(3) NOT NULL CONSTRAINT DF_Metrics_Timestamp DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Metrics PRIMARY KEY (MetricId),
    CONSTRAINT FK_Metrics_Servers FOREIGN KEY (ServerId) REFERENCES dbo.Servers (ServerId) ON DELETE NO ACTION,
    CONSTRAINT CK_Metrics_Cpu    CHECK (CpuUsage    BETWEEN 0 AND 100),
    CONSTRAINT CK_Metrics_Memory CHECK (MemoryUsage BETWEEN 0 AND 100),
    CONSTRAINT CK_Metrics_Disk   CHECK (DiskUsage   BETWEEN 0 AND 100),
    CONSTRAINT CK_Metrics_Resp   CHECK (ResponseTime >= 0),
    CONSTRAINT CK_Metrics_NetworkIn  CHECK (NetworkInKbps IS NULL OR NetworkInKbps >= 0),
    CONSTRAINT CK_Metrics_NetworkOut CHECK (NetworkOutKbps IS NULL OR NetworkOutKbps >= 0),
    CONSTRAINT CK_Metrics_Status CHECK (Status IN (N'Up', N'Down')),
    INDEX IX_Metrics_ServerId_Timestamp NONCLUSTERED (ServerId, Timestamp DESC)
        INCLUDE (CpuUsage, MemoryUsage, DiskUsage, ResponseTime, NetworkInKbps, NetworkOutKbps, Status),
    INDEX IX_Metrics_Timestamp NONCLUSTERED (Timestamp)   -- retention cleanup
);
GO

/* Add network columns to an existing database without inventing values for prior samples. */
IF COL_LENGTH(N'dbo.Metrics', N'NetworkInKbps') IS NULL
    ALTER TABLE dbo.Metrics ADD NetworkInKbps FLOAT NULL;
IF COL_LENGTH(N'dbo.Metrics', N'NetworkOutKbps') IS NULL
    ALTER TABLE dbo.Metrics ADD NetworkOutKbps FLOAT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Metrics') AND name = N'CK_Metrics_NetworkIn')
    ALTER TABLE dbo.Metrics ADD CONSTRAINT CK_Metrics_NetworkIn
        CHECK (NetworkInKbps IS NULL OR NetworkInKbps >= 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Metrics') AND name = N'CK_Metrics_NetworkOut')
    ALTER TABLE dbo.Metrics ADD CONSTRAINT CK_Metrics_NetworkOut
        CHECK (NetworkOutKbps IS NULL OR NetworkOutKbps >= 0);
GO

/* Keep history reads covered after the new metric columns are introduced. */
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Metrics') AND name = N'IX_Metrics_ServerId_Timestamp'
)
AND (
    NOT EXISTS (
        SELECT 1 FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID(N'dbo.Metrics') AND i.name = N'IX_Metrics_ServerId_Timestamp'
          AND ic.is_included_column = 1 AND c.name = N'NetworkInKbps'
    )
    OR NOT EXISTS (
        SELECT 1 FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID(N'dbo.Metrics') AND i.name = N'IX_Metrics_ServerId_Timestamp'
          AND ic.is_included_column = 1 AND c.name = N'NetworkOutKbps'
    )
)
BEGIN
    DROP INDEX IX_Metrics_ServerId_Timestamp ON dbo.Metrics;
    CREATE NONCLUSTERED INDEX IX_Metrics_ServerId_Timestamp
        ON dbo.Metrics (ServerId, Timestamp DESC)
        INCLUDE (CpuUsage, MemoryUsage, DiskUsage, ResponseTime, NetworkInKbps, NetworkOutKbps, Status);
END;
GO

/* ---------- Disks ---------- */
IF OBJECT_ID(N'dbo.Disks', N'U') IS NULL
CREATE TABLE dbo.Disks (
    DiskId         INT IDENTITY(1,1) NOT NULL,
    ServerId       INT NOT NULL,
    DriveLetter    NVARCHAR(5) NOT NULL,
    FreeSpaceMB    BIGINT NOT NULL,
    TotalSpaceMB   BIGINT NOT NULL,
    UsedPercentage FLOAT  NOT NULL,
    Timestamp      DATETIME2(3) NOT NULL CONSTRAINT DF_Disks_Timestamp DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Disks PRIMARY KEY (DiskId),
    CONSTRAINT FK_Disks_Servers FOREIGN KEY (ServerId) REFERENCES dbo.Servers (ServerId) ON DELETE NO ACTION,
    CONSTRAINT CK_Disks_Total CHECK (TotalSpaceMB > 0),
    CONSTRAINT CK_Disks_Free  CHECK (FreeSpaceMB >= 0 AND FreeSpaceMB <= TotalSpaceMB),
    CONSTRAINT CK_Disks_Used  CHECK (UsedPercentage BETWEEN 0 AND 100),
    INDEX IX_Disks_ServerId_Timestamp NONCLUSTERED (ServerId, Timestamp DESC)
);
GO

/* ---------- AlertRules (ServerId NULL = global rule for all servers) ---------- */
IF OBJECT_ID(N'dbo.AlertRules', N'U') IS NULL
CREATE TABLE dbo.AlertRules (
    AlertRuleId INT IDENTITY(1,1) NOT NULL,
    ServerId    INT NULL,
    MetricType  NVARCHAR(50) NOT NULL,
    Operator    NVARCHAR(20) NOT NULL CONSTRAINT DF_AlertRules_Operator DEFAULT N'GreaterThan',
    Threshold   FLOAT NOT NULL,
    IsEnabled   BIT NOT NULL CONSTRAINT DF_AlertRules_IsEnabled DEFAULT 1,
    CreatedAt   DATETIME2(3) NOT NULL CONSTRAINT DF_AlertRules_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AlertRules PRIMARY KEY (AlertRuleId),
    CONSTRAINT FK_AlertRules_Servers FOREIGN KEY (ServerId) REFERENCES dbo.Servers (ServerId) ON DELETE NO ACTION,
    CONSTRAINT CK_AlertRules_MetricType CHECK (MetricType IN (N'Cpu', N'Memory', N'Disk', N'ResponseTime')),
    CONSTRAINT CK_AlertRules_Operator   CHECK (Operator IN (N'GreaterThan', N'LessThan')),
    INDEX IX_AlertRules_ServerId NONCLUSTERED (ServerId)
);
GO

/* ---------- Alerts ---------- */
IF OBJECT_ID(N'dbo.Alerts', N'U') IS NULL
CREATE TABLE dbo.Alerts (
    AlertId     INT IDENTITY(1,1) NOT NULL,
    ServerId    INT NOT NULL,
    AlertRuleId INT NULL,
    MetricType  NVARCHAR(50) NOT NULL,
    MetricValue FLOAT NOT NULL,
    Threshold   FLOAT NOT NULL,
    Status      NVARCHAR(20) NOT NULL CONSTRAINT DF_Alerts_Status DEFAULT N'Triggered',
    CreatedAt   DATETIME2(3) NOT NULL CONSTRAINT DF_Alerts_CreatedAt DEFAULT SYSUTCDATETIME(),
    ResolvedAt  DATETIME2(3) NULL,
    CONSTRAINT PK_Alerts PRIMARY KEY (AlertId),
    CONSTRAINT FK_Alerts_Servers    FOREIGN KEY (ServerId)    REFERENCES dbo.Servers (ServerId)       ON DELETE NO ACTION,
    CONSTRAINT FK_Alerts_AlertRules FOREIGN KEY (AlertRuleId) REFERENCES dbo.AlertRules (AlertRuleId) ON DELETE NO ACTION,
    CONSTRAINT CK_Alerts_Status CHECK (Status IN (N'Triggered', N'Acknowledged', N'Resolved')),
    CONSTRAINT CK_Alerts_Resolved CHECK (ResolvedAt IS NULL OR ResolvedAt >= CreatedAt),
    INDEX IX_Alerts_ServerId_CreatedAt NONCLUSTERED (ServerId, CreatedAt DESC),
    INDEX IX_Alerts_Status_CreatedAt NONCLUSTERED (Status, CreatedAt DESC),
    INDEX IX_Alerts_AlertRuleId NONCLUSTERED (AlertRuleId)
);
GO

/* ---------- Reports ---------- */
IF OBJECT_ID(N'dbo.Reports', N'U') IS NULL
CREATE TABLE dbo.Reports (
    ReportId    INT IDENTITY(1,1) NOT NULL,
    ServerId    INT NOT NULL,
    StartTime   DATETIME2(3) NOT NULL,
    EndTime     DATETIME2(3) NOT NULL,
    Status      NVARCHAR(20) NOT NULL CONSTRAINT DF_Reports_Status DEFAULT N'Pending',
    FilePath    NVARCHAR(500) NULL,
    CreatedAt   DATETIME2(3) NOT NULL CONSTRAINT DF_Reports_CreatedAt DEFAULT SYSUTCDATETIME(),
    CompletedAt DATETIME2(3) NULL,
    CONSTRAINT PK_Reports PRIMARY KEY (ReportId),
    CONSTRAINT FK_Reports_Servers FOREIGN KEY (ServerId) REFERENCES dbo.Servers (ServerId) ON DELETE NO ACTION,
    CONSTRAINT CK_Reports_Range  CHECK (EndTime > StartTime),
    CONSTRAINT CK_Reports_Status CHECK (Status IN (N'Pending', N'Processing', N'Completed', N'Failed')),
    INDEX IX_Reports_ServerId_CreatedAt NONCLUSTERED (ServerId, CreatedAt DESC),
    INDEX IX_Reports_Status NONCLUSTERED (Status)
);
GO

/* =====================================================================
   SEED DATA
   ===================================================================== */

IF NOT EXISTS (SELECT 1 FROM dbo.Roles)
    INSERT INTO dbo.Roles (Name, Description) VALUES
        (N'Admin', N'Full access, including user management and background jobs'),
        (N'User',  N'Read access to servers, metrics, alerts and reports');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Servers)
    INSERT INTO dbo.Servers (Name, IPAddress, Status, Description) VALUES
        (N'web-01',   N'10.0.1.11', N'Up',   N'Public web front end'),
        (N'web-02',   N'10.0.1.12', N'Up',   N'Public web front end (replica)'),
        (N'api-01',   N'10.0.2.21', N'Up',   N'Application API server'),
        (N'db-01',    N'10.0.3.31', N'Up',   N'Primary SQL Server database'),
        (N'cache-01', N'10.0.4.41', N'Down', N'Redis cache node');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AlertRules)
    INSERT INTO dbo.AlertRules (ServerId, MetricType, Operator, Threshold) VALUES
        (NULL, N'Cpu',          N'GreaterThan', 80),
        (NULL, N'Memory',       N'GreaterThan', 90),
        (NULL, N'Disk',         N'GreaterThan', 90),
        (NULL, N'ResponseTime', N'GreaterThan', 1000);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Disks)
    INSERT INTO dbo.Disks (ServerId, DriveLetter, FreeSpaceMB, TotalSpaceMB, UsedPercentage)
    SELECT s.ServerId,
           v.DriveLetter,
           v.TotalMB - v.UsedMB,
           v.TotalMB,
           ROUND(100.0 * v.UsedMB / v.TotalMB, 1)
    FROM dbo.Servers AS s
    CROSS JOIN (VALUES
        (N'C:', CAST(256000 AS BIGINT), CAST(120000 AS BIGINT)),
        (N'D:', CAST(512000 AS BIGINT), CAST(200000 AS BIGINT))
    ) AS v (DriveLetter, TotalMB, UsedMB);
GO

/* Two hours of history (every 5 minutes) per server, random but plausible */
IF NOT EXISTS (SELECT 1 FROM dbo.Metrics)
BEGIN
    ;WITH n AS (
        SELECT 0 AS i
        UNION ALL
        SELECT i + 1 FROM n WHERE i < 23
    )
    INSERT INTO dbo.Metrics (ServerId, CpuUsage, MemoryUsage, DiskUsage, ResponseTime,
        NetworkInKbps, NetworkOutKbps, Status, Timestamp)
    SELECT s.ServerId,
           10 + ABS(CHECKSUM(NEWID())) % 70,
           30 + ABS(CHECKSUM(NEWID())) % 60,
           40 + ABS(CHECKSUM(NEWID())) % 40,
           50 + ABS(CHECKSUM(NEWID())) % 400,
           CASE WHEN s.Status = N'Down' AND n.i < 3 THEN 0
                ELSE 100 + ABS(CHECKSUM(NEWID())) % 5000 END,
           CASE WHEN s.Status = N'Down' AND n.i < 3 THEN 0
                ELSE 50 + ABS(CHECKSUM(NEWID())) % 3000 END,
           CASE WHEN s.Status = N'Down' AND n.i < 3 THEN N'Down' ELSE N'Up' END,
           DATEADD(MINUTE, -5 * n.i, SYSUTCDATETIME())
    FROM dbo.Servers AS s
    CROSS JOIN n;
END
GO
