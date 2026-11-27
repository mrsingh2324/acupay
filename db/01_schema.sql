/* AcuPay - 01_schema.sql
   Run once against an empty database. Safe to re-run: it drops and recreates. */

IF OBJECT_ID('dbo.Invoices', 'U')        IS NOT NULL DROP TABLE dbo.Invoices;
IF OBJECT_ID('dbo.PurchaseOrders', 'U')  IS NOT NULL DROP TABLE dbo.PurchaseOrders;
IF OBJECT_ID('dbo.Vendors', 'U')         IS NOT NULL DROP TABLE dbo.Vendors;
IF OBJECT_ID('dbo.Users', 'U')           IS NOT NULL DROP TABLE dbo.Users;
GO

CREATE TABLE dbo.Users
(
    UserId        INT IDENTITY(1,1) NOT NULL,
    Username      NVARCHAR(50)      NOT NULL,
    PasswordHash  NVARCHAR(500)     NOT NULL,
    Role          NVARCHAR(20)      NOT NULL,
    CONSTRAINT PK_Users       PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Name  UNIQUE (Username),
    CONSTRAINT CK_Users_Role  CHECK (Role IN ('Requester', 'Approver', 'Finance'))
);
GO

CREATE TABLE dbo.Vendors
(
    VendorId    INT IDENTITY(1,1) NOT NULL,
    VendorCode  NVARCHAR(10)      NOT NULL,
    VendorName  NVARCHAR(100)     NOT NULL,
    CONSTRAINT PK_Vendors      PRIMARY KEY (VendorId),
    CONSTRAINT UQ_Vendors_Code UNIQUE (VendorCode)
);
GO

CREATE TABLE dbo.PurchaseOrders
(
    PoId      INT IDENTITY(1,1) NOT NULL,
    PoNumber  NVARCHAR(20)      NOT NULL,
    VendorId  INT               NOT NULL,
    PoAmount  DECIMAL(18,2)     NOT NULL,
    CONSTRAINT PK_PurchaseOrders        PRIMARY KEY (PoId),
    CONSTRAINT UQ_PurchaseOrders_Number UNIQUE (PoNumber),
    CONSTRAINT FK_PurchaseOrders_Vendor FOREIGN KEY (VendorId) REFERENCES dbo.Vendors (VendorId),
    CONSTRAINT CK_PurchaseOrders_Amount CHECK (PoAmount > 0)
);
GO

CREATE TABLE dbo.Invoices
(
    InvoiceId       INT IDENTITY(1,1) NOT NULL,
    InvoiceNumber   NVARCHAR(20)      NOT NULL,
    VendorId        INT               NOT NULL,
    PoId            INT               NULL,
    InvoiceAmount   DECIMAL(18,2)     NOT NULL,
    Status          NVARCHAR(20)      NOT NULL CONSTRAINT DF_Invoices_Status DEFAULT ('Pending'),
    MatchNote       NVARCHAR(50)      NULL,
    VariancePercent DECIMAL(9,2)      NULL,
    CreatedBy       NVARCHAR(50)      NOT NULL,
    CreatedAtUtc    DATETIME2(0)      NOT NULL CONSTRAINT DF_Invoices_Created DEFAULT (SYSUTCDATETIME()),
    DecidedBy       NVARCHAR(50)      NULL,
    DecidedAtUtc    DATETIME2(0)      NULL,
    DecisionReason  NVARCHAR(200)     NULL,
    CONSTRAINT PK_Invoices           PRIMARY KEY (InvoiceId),
    CONSTRAINT UQ_Invoices_VendorNum UNIQUE (VendorId, InvoiceNumber),
    CONSTRAINT FK_Invoices_Vendor    FOREIGN KEY (VendorId) REFERENCES dbo.Vendors (VendorId),
    CONSTRAINT FK_Invoices_Po        FOREIGN KEY (PoId)     REFERENCES dbo.PurchaseOrders (PoId),
    CONSTRAINT CK_Invoices_Amount    CHECK (InvoiceAmount > 0),
    CONSTRAINT CK_Invoices_Status    CHECK (Status IN ('Pending', 'Matched', 'Exception', 'Approved', 'Rejected'))
);
GO

/* Supports the queue query, which filters on Status. */
CREATE NONCLUSTERED INDEX IX_Invoices_Status
    ON dbo.Invoices (Status)
    INCLUDE (InvoiceNumber, VendorId, PoId, InvoiceAmount, MatchNote, VariancePercent, CreatedBy, DecidedBy);
GO
