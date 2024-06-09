BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Buildings ON;

    INSERT INTO [2021MartiHigienaNew].dbo.Buildings(Id,Name,BankAccount)
    SELECT OddelID,Oddel,OddelBanka
    FROM [2021MartiHigienaOriginal].dbo.Partneri_Oddeli
    COMMIT TRANSACTION;
	    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Buildings Off;

    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;

BEGIN TRANSACTION;