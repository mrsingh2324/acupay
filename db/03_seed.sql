/* AcuPay - 03_seed.sql
   Exactly the data in section 4.2 of the brief. Re-runnable: it clears first.
   Passwords are PBKDF2-SHA256, 100000 iterations, stored as iterations.salt.key
   requester1 / Req@12345      approver1 / App@12345      finance1 / Fin@12345 */

DELETE FROM dbo.Invoices;
DELETE FROM dbo.PurchaseOrders;
DELETE FROM dbo.Vendors;
DELETE FROM dbo.Users;
GO

DBCC CHECKIDENT ('dbo.Invoices',       RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.PurchaseOrders', RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.Vendors',        RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.Users',          RESEED, 0) WITH NO_INFOMSGS;
GO

INSERT INTO dbo.Users (Username, PasswordHash, Role) VALUES
    ('requester1', '100000.JG5vdphqNrYu0NK9EO67Dg==.GMOg/nKYW80y8bwOIPlYFaiTp2aLH8iZCDocFmDfluk=', 'Requester'),
    ('approver1',  '100000.nBQevBq/edV12gCOXQvX6A==.LCjpCBZkpVQrJL1d0YIrDXT/reUTAOhqv8hchyZqbcU=', 'Approver'),
    ('finance1',   '100000.ZmxWaYxtUhNZ2XTGKNnyoQ==.6peeZel9zCCdoXLlelsPy873+S1LAT9mdnjWizw1lj0=', 'Finance');
GO

INSERT INTO dbo.Vendors (VendorCode, VendorName) VALUES
    ('V001', 'Alpha Infotech'),
    ('V002', 'Beta Supplies'),
    ('V003', 'Gamma Traders');
GO

INSERT INTO dbo.PurchaseOrders (PoNumber, VendorId, PoAmount)
SELECT 'PO-1001', VendorId, 100000.00 FROM dbo.Vendors WHERE VendorCode = 'V001'
UNION ALL
SELECT 'PO-1002', VendorId,  50000.00 FROM dbo.Vendors WHERE VendorCode = 'V001'
UNION ALL
SELECT 'PO-1003', VendorId,  20000.00 FROM dbo.Vendors WHERE VendorCode = 'V002'
UNION ALL
SELECT 'PO-1004', VendorId,  40000.00 FROM dbo.Vendors WHERE VendorCode = 'V003';
GO

/* Invoice 5 points at PO-1003, which belongs to V002, while the invoice is V001.
   That is the VENDOR_MISMATCH case and it is deliberate. */
INSERT INTO dbo.Invoices (InvoiceNumber, VendorId, PoId, InvoiceAmount, Status, CreatedBy)
SELECT 'INV-5001', v.VendorId, po.PoId, 101500.00, 'Pending', 'requester1'
FROM dbo.Vendors v JOIN dbo.PurchaseOrders po ON po.PoNumber = 'PO-1001' WHERE v.VendorCode = 'V001'
UNION ALL
SELECT 'INV-5002', v.VendorId, po.PoId,  49000.00, 'Pending', 'requester1'
FROM dbo.Vendors v JOIN dbo.PurchaseOrders po ON po.PoNumber = 'PO-1002' WHERE v.VendorCode = 'V001'
UNION ALL
SELECT 'INV-5003', v.VendorId, po.PoId,  23000.00, 'Pending', 'approver1'
FROM dbo.Vendors v JOIN dbo.PurchaseOrders po ON po.PoNumber = 'PO-1003' WHERE v.VendorCode = 'V002'
UNION ALL
SELECT 'INV-5004', v.VendorId, NULL,     12000.00, 'Pending', 'requester1'
FROM dbo.Vendors v WHERE v.VendorCode = 'V002'
UNION ALL
SELECT 'INV-5005', v.VendorId, po.PoId,  19000.00, 'Pending', 'requester1'
FROM dbo.Vendors v JOIN dbo.PurchaseOrders po ON po.PoNumber = 'PO-1003' WHERE v.VendorCode = 'V001'
UNION ALL
SELECT 'INV-5006', v.VendorId, po.PoId,  36000.00, 'Pending', 'requester1'
FROM dbo.Vendors v JOIN dbo.PurchaseOrders po ON po.PoNumber = 'PO-1004' WHERE v.VendorCode = 'V003';
GO

SELECT InvoiceId, InvoiceNumber, InvoiceAmount, Status FROM dbo.Invoices ORDER BY InvoiceId;
GO
