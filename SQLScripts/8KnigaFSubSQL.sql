BEGIN TRANSACTION;
BEGIN TRY
    -- Allow explicit values to be inserted into the identity column
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.BookFinancialSub ON;

    INSERT INTO [2021MartiHigienaNew].dbo.BookFinancialSub 
        (Id, BookFinancialId, [Date], Demands, Owes)
    SELECT 
        KnigaF_subID,
        KnigaFID,
        DatumSub,
        PobaruvaSub,
        DolziSub
    FROM [2021MartiHigienaOriginal].dbo.KnigaF_sub;

    -- Turn off IDENTITY_INSERT after the insertion
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.BookFinancialSub OFF;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;
