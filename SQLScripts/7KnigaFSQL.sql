

BEGIN TRANSACTION;
BEGIN TRY
    -- Allow explicit values to be inserted into the identity column
    SET IDENTITY_INSERT [2026OriginalMarti].dbo.BookFinancials ON;

    INSERT INTO [2026OriginalMarti].dbo.BookFinancials 
        (Id, OrderN, InvoiceId, CustomerId, DocumentTypId, Description, DatumF, Owes, Demands, Time, DateTimeChanges,DontSum,Status,PaymentType)
    SELECT 
        KnigaFID,
        Nalog,
        SmetkaID,
        PartnerID, 
        VidId, 
        Opis, 
        DatumF, 
        Dolzi, 
        Pobaruva, 
        Vreme, 
        VremePromena,0,0,0
    FROM [2026-04Marti].dbo.KnigaF
    -- Turn off IDENTITY_INSERT after the insertion
    SET IDENTITY_INSERT [2026OriginalMarti].dbo.BookFinancials OFF;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;	

 ---Update BookFinancial Status
UPDATE bf
SET bf.Status = 0
FROM BookFinancials bf
INNER JOIN Customers cs ON cs.Id = bf.CustomerId
INNER JOIN Documents d ON d.CustomerId = cs.Id
WHERE d.PaymentStatus = 0;

UPDATE bf
SET bf.Status = 1
FROM BookFinancials bf
INNER JOIN Customers cs ON cs.Id = bf.CustomerId
INNER JOIN Documents d ON d.CustomerId = cs.Id
WHERE d.PaymentStatus = 1;