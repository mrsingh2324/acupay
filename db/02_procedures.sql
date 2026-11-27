/* AcuPay - 02_procedures.sql
   Every read and write the application performs goes through one of these.
   The application never sends ad hoc SQL. */

IF OBJECT_ID('dbo.usp_GetUserByUsername', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_GetUserByUsername;
IF OBJECT_ID('dbo.usp_GetInvoices', 'P')       IS NOT NULL DROP PROCEDURE dbo.usp_GetInvoices;
IF OBJECT_ID('dbo.usp_GetInvoiceById', 'P')    IS NOT NULL DROP PROCEDURE dbo.usp_GetInvoiceById;
IF OBJECT_ID('dbo.usp_InsertInvoice', 'P')     IS NOT NULL DROP PROCEDURE dbo.usp_InsertInvoice;
IF OBJECT_ID('dbo.usp_UpdateInvoiceMatch', 'P')IS NOT NULL DROP PROCEDURE dbo.usp_UpdateInvoiceMatch;
IF OBJECT_ID('dbo.usp_DecideInvoice', 'P')     IS NOT NULL DROP PROCEDURE dbo.usp_DecideInvoice;
GO

CREATE PROCEDURE dbo.usp_GetUserByUsername
    @Username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  u.UserId,
            u.Username,
            u.PasswordHash,
            u.Role
    FROM    dbo.Users AS u
    WHERE   u.Username = @Username;
END
GO

/* @Status NULL returns every invoice. */
CREATE PROCEDURE dbo.usp_GetInvoices
    @Status NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  i.InvoiceId,
            i.InvoiceNumber,
            i.VendorId,
            v.VendorCode,
            v.VendorName,
            i.PoId,
            po.PoNumber,
            po.PoAmount,
            po.VendorId AS PoVendorId,
            i.InvoiceAmount,
            i.Status,
            i.MatchNote,
            i.VariancePercent,
            i.CreatedBy,
            i.DecidedBy,
            i.DecisionReason
    FROM        dbo.Invoices        AS i
    INNER JOIN  dbo.Vendors         AS v  ON v.VendorId = i.VendorId
    LEFT  JOIN  dbo.PurchaseOrders  AS po ON po.PoId    = i.PoId
    WHERE   (@Status IS NULL OR i.Status = @Status)
    ORDER BY i.InvoiceId;
END
GO

CREATE PROCEDURE dbo.usp_GetInvoiceById
    @InvoiceId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  i.InvoiceId,
            i.InvoiceNumber,
            i.VendorId,
            v.VendorCode,
            v.VendorName,
            i.PoId,
            po.PoNumber,
            po.PoAmount,
            po.VendorId AS PoVendorId,
            i.InvoiceAmount,
            i.Status,
            i.MatchNote,
            i.VariancePercent,
            i.CreatedBy,
            i.DecidedBy,
            i.DecisionReason
    FROM        dbo.Invoices        AS i
    INNER JOIN  dbo.Vendors         AS v  ON v.VendorId = i.VendorId
    LEFT  JOIN  dbo.PurchaseOrders  AS po ON po.PoId    = i.PoId
    WHERE   i.InvoiceId = @InvoiceId;
END
GO

/* Takes business keys, not surrogate ids, so the API never has to look them up first.
   Raises 50001 if the vendor or PO does not exist.
   The unique constraint raises 2627 on a duplicate, which the repository maps to a 409. */
CREATE PROCEDURE dbo.usp_InsertInvoice
    @InvoiceNumber NVARCHAR(20),
    @VendorCode    NVARCHAR(10),
    @PoNumber      NVARCHAR(20)  = NULL,
    @InvoiceAmount DECIMAL(18,2),
    @CreatedBy     NVARCHAR(50),
    @InvoiceId     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @VendorId INT, @PoId INT;

    SELECT @VendorId = VendorId FROM dbo.Vendors WHERE VendorCode = @VendorCode;
    IF @VendorId IS NULL
    BEGIN
        RAISERROR ('UNKNOWN_VENDOR', 16, 1);
        RETURN;
    END

    IF @PoNumber IS NOT NULL
    BEGIN
        SELECT @PoId = PoId FROM dbo.PurchaseOrders WHERE PoNumber = @PoNumber;
        IF @PoId IS NULL
        BEGIN
            RAISERROR ('UNKNOWN_PO', 16, 1);
            RETURN;
        END
    END

    BEGIN TRANSACTION;

        INSERT INTO dbo.Invoices (InvoiceNumber, VendorId, PoId, InvoiceAmount, Status, CreatedBy)
        VALUES (@InvoiceNumber, @VendorId, @PoId, @InvoiceAmount, 'Pending', @CreatedBy);

        SET @InvoiceId = SCOPE_IDENTITY();

    COMMIT TRANSACTION;
END
GO

CREATE PROCEDURE dbo.usp_UpdateInvoiceMatch
    @InvoiceId       INT,
    @Status          NVARCHAR(20),
    @MatchNote       NVARCHAR(50),
    @VariancePercent DECIMAL(9,2) = NULL,
    @DecidedBy       NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE  dbo.Invoices
    SET     Status          = @Status,
            MatchNote       = @MatchNote,
            VariancePercent = @VariancePercent,
            DecidedBy       = @DecidedBy,
            DecidedAtUtc    = CASE WHEN @DecidedBy IS NULL THEN NULL ELSE SYSUTCDATETIME() END
    WHERE   InvoiceId = @InvoiceId;
END
GO

CREATE PROCEDURE dbo.usp_DecideInvoice
    @InvoiceId INT,
    @Status    NVARCHAR(20),
    @DecidedBy NVARCHAR(50),
    @Reason    NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

        UPDATE  dbo.Invoices
        SET     Status         = @Status,
                DecidedBy      = @DecidedBy,
                DecidedAtUtc   = SYSUTCDATETIME(),
                DecisionReason = @Reason
        WHERE   InvoiceId = @InvoiceId;

    COMMIT TRANSACTION;
END
GO
