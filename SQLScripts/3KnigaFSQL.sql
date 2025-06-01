BEGIN TRANSACTION;

BEGIN TRY
    INSERT INTO [2025MartiTest].dbo.BookFinancials(CustomerId,DocumentTypId,Description,DatumF,Owes,Demands,Time,DateTimeChanges)
    SELECT PartnerID,VidId ,Opis,DatumF,Dolzi,Pobaruva,Vreme,VremePromena
    FROM [2025MartiHigiena].dbo.KnigaF;

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
