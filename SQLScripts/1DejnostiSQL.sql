BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Activity ON;

    INSERT INTO [2021MartiHigienaNew].dbo.Activity (Id,Name)
    SELECT DejnostID,Dejnost
    FROM [2021MartiHigienaOriginal].dbo.Partneri_Dejnosti;

    COMMIT TRANSACTION;
	    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Activity Off;

    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;

BEGIN TRANSACTION;
