BEGIN TRANSACTION;
BEGIN TRY
    -- Allow explicit values to be inserted into the identity column
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Books ON;

    INSERT INTO [2021MartiHigienaNew].dbo.Books 
        (Id,DocId,ArticleId,Input,Output,PriceWithTax,Tax,Quantity,Total,ArticleNotes,UnitOfMeasurement)
    SELECT 
       KnigaID,Dokid,ArtikalID,Vlez,Izlez,Cenai,Danok,Kutii,Vkupno,ArtikalZabeleska,EdmK
    FROM [2021MartiHigienaOriginal].dbo.Kniga
    -- Turn off IDENTITY_INSERT after the insertion
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Books OFF;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;
