BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT Documents ON;

    INSERT INTO Documents
        (Id, Number, Date, CustomerId, ToDocument, Description, DateReceived, TotalInput, TotalOutput, CreatedTime, DateTimeChanged,PaymentStatus,PaymentType)
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
        VremePromena,1,0
    FROM [2026MartiNew].dbo.Dokumenti

    COMMIT TRANSACTION;
    SET IDENTITY_INSERT Documents OFF;

    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    THROW;
END CATCH;