BEGIN TRANSACTION;
BEGIN TRY
    -- Allow explicit values to be inserted into the identity column
    SET IDENTITY_INSERT [2026OriginalMarti].dbo.Books ON;

    INSERT INTO [2026OriginalMarti].dbo.Books 
        (Id,DocId,ArticleId,Input,Output,PriceWithTax,Price,Tax,Quantity,Total,ArticleNotes,UnitOfMeasurement,Hide)
    SELECT 
       KnigaID,Dokid,ArtikalID,Vlez,Izlez,Cenai,Cena,Danok,Kutii,Vkupno,ArtikalZabeleska,EdmK,0
    FROM [2026-04Marti].dbo.Kniga 
    -- Turn off IDENTITY_INSERT after the insertion
    SET IDENTITY_INSERT [2026OriginalMarti].dbo.Books OFF;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;