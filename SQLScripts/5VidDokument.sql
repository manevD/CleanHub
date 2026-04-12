BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2026OriginalMarti].dbo.DocumentTyp ON;

    INSERT INTO [2026OriginalMarti].dbo.DocumentTyp( Id,VidDokument)
    SELECT VidID,VidDokument
    FROM [2026-04Marti].dbo.VidDokumenti ;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;
select * from DocumentTyp