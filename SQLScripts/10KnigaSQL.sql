BEGIN TRANSACTION;
BEGIN TRY
    -- Allow explicit values to be inserted into the identity column
    SET IDENTITY_INSERT Books ON;

    INSERT INTO Books 
        (Id,DocId,ArticleId,Input,Output,PriceWithTax,Price,Tax,Quantity,Total,ArticleNotes,UnitOfMeasurement,Hide)
    SELECT 
       KnigaID,Dokid,ArtikalID,Vlez,Izlez,Cenai,Cena,Danok,Kutii,Vkupno,ArtikalZabeleska,EdmK,0
    FROM [2026MartiNew].dbo.Kniga 

    -- Turn off IDENTITY_INSERT after the insertion
    SET IDENTITY_INSERT Books OFF;

    COMMIT TRANSACTION;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;

    UPDATE Books
SET Tax = CASE
    WHEN Tax = 0.18 THEN 18
    WHEN Tax = 0.19 THEN 19
END
WHERE Tax IN (0.18, 0.19);

UPDATE Books
SET
    PriceWithTax = CASE
        WHEN PriceWithTax <> ROUND(PriceWithTax, 2)
        THEN ROUND(PriceWithTax, 2)
        ELSE PriceWithTax
    END,
    Price = CASE
        WHEN Price <> ROUND(Price, 2)
        THEN ROUND(Price, 2)
        ELSE Price
    END,
    Total = CASE
        WHEN Total <> ROUND(Total, 2)
        THEN ROUND(Total, 2)
        ELSE Total
    END
WHERE
    PriceWithTax <> ROUND(PriceWithTax, 2)
    OR Price <> ROUND(Price, 2)
    OR Total <> ROUND(Total, 2);