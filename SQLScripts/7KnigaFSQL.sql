BEGIN TRANSACTION;
BEGIN TRY
    -- Allow explicit values to be inserted into the identity column
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.BookFinancials ON;

    INSERT INTO [2021MartiHigienaNew].dbo.BookFinancials 
        (Id, OrderN, SmetkaId, CustomerId, DocumentTypId, Description, DatumF, Owes, Demands, Time, DateTimeChanges)
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
        VremePromena
    FROM [2021MartiHigienaOriginal].dbo.KnigaF
    -- Turn off IDENTITY_INSERT after the insertion
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.BookFinancials OFF;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;

