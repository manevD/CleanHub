BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Customers ON;

    INSERT INTO [2021MartiHigienaNew].dbo.Customers (Id,CustomerInfo, Adress, PhoneNumber, Email, Web, Inactive, InactiveDatum, ActivityId, PhysicalPerson, BuidlingId)
    SELECT PartnerID,Partner, parAdresa, parTel, parEmail, parWeb, parNeaktiven, parNeaktivenDatum, DejnostID, FizickoLice, OddelID
    FROM [2021MartiHigienaOriginal].dbo.Partneri
      update Customers set Inactive = 0 where Inactive is null
    COMMIT TRANSACTION;
	SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Customers OFF;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;