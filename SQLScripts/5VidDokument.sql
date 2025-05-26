BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2025MartiTest].dbo.DocumentTyp ON;

    INSERT INTO [2025MartiTest].dbo.DocumentTyp( Id,VidDokument)
    SELECT VidID,VidDokument
    FROM [2025MartiHigiena].dbo.VidDokumenti ;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;