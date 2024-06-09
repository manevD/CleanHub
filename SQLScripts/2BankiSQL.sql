BEGIN TRANSACTION;

BEGIN TRY
    INSERT INTO [2021MartiHigienaNew].dbo.Banks(Name)
    SELECT Banka
    FROM [2021MartiHigienaOriginal].dbo.Banki;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;

BEGIN TRANSACTION;