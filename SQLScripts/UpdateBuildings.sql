BEGIN TRANSACTION;

BEGIN TRY

    UPDATE b
    SET
        b.Name          = p.Oddel,
        b.BankAccount   = p.OddelBanka,
        b.CustomerRefId = p.OddelPartnerID
    FROM Buildings b
    INNER JOIN [2026MartiNew].dbo.Partneri_Oddeli p
        ON b.Id = p.OddelID;

    COMMIT TRANSACTION;

    PRINT 'Data updated successfully.';

END TRY
BEGIN CATCH

    ROLLBACK TRANSACTION;

    PRINT 'Error occurred. Transaction rolled back.';
    THROW;

END CATCH;