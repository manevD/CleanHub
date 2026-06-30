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


BEGIN TRANSACTION;
BEGIN TRY

WITH MonthMapping AS (
    SELECT N'01' AS MonthNum, N'Јануари' AS MonthName UNION ALL
    SELECT N'02', N'Февруари' UNION ALL
    SELECT N'03', N'Март' UNION ALL
    SELECT N'04', N'Април' UNION ALL
    SELECT N'05', N'Мај' UNION ALL
    SELECT N'06', N'Јуни' UNION ALL
    SELECT N'07', N'Јули' UNION ALL
    SELECT N'08', N'Август' UNION ALL
    SELECT N'09', N'Септември' UNION ALL
    SELECT N'10', N'Октомври' UNION ALL
    SELECT N'11', N'Ноември' UNION ALL
    SELECT N'12', N'Декември'
)

UPDATE d
SET d.PaymentStatus = 
    CASE 
        WHEN EXISTS (
            SELECT 1 
            FROM BookFinancials bf
            CROSS JOIN MonthMapping mm
            WHERE 
                bf.CustomerId = d.CustomerId
                AND (
                    -- 🟢 Fall 1: Einzelne Monate z. B. "за 01,02,03/2021"
                    (
                        CHARINDEX('/', bf.Description) > 0  
                        AND EXISTS (
                            SELECT 1 
                            FROM STRING_SPLIT(
                                LEFT(bf.Description, CHARINDEX('/', bf.Description) - 1), 
                                ','
                            ) AS SplitMonths
                            WHERE TRY_CAST(LTRIM(RTRIM(
                                SUBSTRING(SplitMonths.value, PATINDEX('%[0-9]%', SplitMonths.value), 2)
                            )) AS INT) = TRY_CAST(mm.MonthNum AS INT)
                        )
                    )
                    
                    -- 🟢 Fall 2: Monatsbereiche z. B. "од 01-05/2021"
                    OR (
                        bf.Description LIKE N'%од %-%/%'  
                        AND CHARINDEX('-', bf.Description) > 0
                        AND CHARINDEX('/', bf.Description) > 0
                        AND TRY_CAST(
                            SUBSTRING(bf.Description, CHARINDEX('од ', bf.Description) + 3, 2)
                        AS INT) <= TRY_CAST(mm.MonthNum AS INT)  
                        AND TRY_CAST(
                            SUBSTRING(bf.Description, CHARINDEX('-', bf.Description) + 1, 2)
                        AS INT) >= TRY_CAST(mm.MonthNum AS INT)
                    )
                )
                -- 🔹 Korrekte Jahr-Extraktion
                AND CHARINDEX('/', REVERSE(bf.Description)) > 0
                AND d.ToDocument = mm.MonthName + ' ' + 
                    LEFT(
                        RIGHT(bf.Description, CHARINDEX('/', REVERSE(bf.Description)) - 1), 
                        4  -- Nur die ersten 4 Zeichen nach "/" extrahieren (das Jahr)
                    )
        ) 
        THEN 0 -- Wenn es eine Übereinstimmung gibt, setze PaymentStatus auf 0
        ELSE 1 -- Andernfalls setze PaymentStatus auf 1
    END
FROM Documents d;
    COMMIT TRANSACTION;
    PRINT 'PaymentStatus inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    THROW;
END CATCH;



BEGIN TRANSACTION;
BEGIN TRY
UPDATE Documents set NewTotal = TotalOutput where PaymentStatus = 0 
UPDATE Documents
SET NewTotal = 
        CASE 
            WHEN DATEDIFF(DAY, DateReceived, GETDATE()) < 30 THEN ROUND(TotalInput * 1.02, 0)
            WHEN DATEDIFF(DAY, DateReceived, GETDATE()) BETWEEN 31 AND 60 THEN ROUND(TotalOutput * 1.04, 0)
            WHEN DATEDIFF(DAY, DateReceived, GETDATE()) BETWEEN 61 AND 90 THEN ROUND(TotalOutput * 1.06, 0)
            WHEN DATEDIFF(DAY, DateReceived, GETDATE()) BETWEEN 91 AND 180 THEN ROUND(TotalOutput * 1.08, 0)
            WHEN DATEDIFF(DAY, DateReceived, GETDATE()) BETWEEN 181 AND 360 THEN ROUND(TotalOutput * 1.10, 0)
            WHEN DATEDIFF(DAY, DateReceived, GETDATE()) BETWEEN 361 AND 730 THEN ROUND(TotalOutput * 1.13, 0)
            ELSE ROUND(TotalOutput * 1.16, 0)
        END
WHERE DateReceived IS NOT NULL AND TotalOutput IS NOT NULL and PaymentStatus != 0 ;

    COMMIT TRANSACTION;
    PRINT 'NewTotal updated successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    THROW;
END CATCH;
