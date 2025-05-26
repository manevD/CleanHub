BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2025MartiTest].dbo.Activity ON;

    INSERT INTO [2025MartiTest].dbo.Activity (Id,Name)
    SELECT DejnostID,Dejnost
    FROM [2025MartiHigiena].dbo.Partneri_Dejnosti;

    COMMIT TRANSACTION;
	    SET IDENTITY_INSERT [2025MartiTest].dbo.Activity Off;

    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;

BEGIN TRANSACTION;
