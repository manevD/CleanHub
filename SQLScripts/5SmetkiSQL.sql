BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Invoice ON;

    INSERT INTO [2021MartiHigienaNew].dbo.Invoice( Id,Description,KarticaPar)
    SELECT SmetkaId,Smetka,KarticaPar
    FROM [2021MartiHigienaOriginal].dbo.Smetki ;

    COMMIT TRANSACTION;
	    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Invoice Off;

    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;