BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Documents ON;

    INSERT INTO [2021MartiHigienaNew].dbo.Documents
        (Id, Number, Date, CustomerId, ToDocument, Description, DateReceived, TotalInput, TotalOutput, CreatedTime, DateTimeChanged)
    SELECT 
        Dokid,
        Broj,
        Datum,
        PartnerID,
        PoDokument,
        OpisDok,
        Valuta,
        VkupnoVl,
        VkupnoIz,
        Vreme,
        VremePromena
    FROM [2021MartiHigienaOriginal].dbo.Dokumenti

    COMMIT TRANSACTION;
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Documents OFF;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    THROW;
END CATCH;
