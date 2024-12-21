BEGIN TRANSACTION;
BEGIN TRY
    -- Allow explicit values to be inserted into the identity column
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Articles ON;

    INSERT INTO [2021MartiHigienaNew].dbo.Articles 
        (Id,Description,PurschaceCalculation,ShortDescription)
    SELECT 
       VidArtID,VidArtikal,PresmNab,Skr
    FROM [2021MartiHigiena122024].dbo.Artikli_Vidovi
    -- Turn off IDENTITY_INSERT after the insertion
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Articles OFF;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;

