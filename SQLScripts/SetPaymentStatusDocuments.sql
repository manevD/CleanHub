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
SET d.PaymentStatus = 1
FROM Documents d
INNER JOIN Customers c ON d.CustomerId = c.Id
INNER JOIN BookFinancials bf ON bf.CustomerId = c.Id
CROSS JOIN MonthMapping mm 
WHERE 
    -- 🟢 Fall 1: Einzelne Monate z. B. "за 01 ѕ.Б, 02, 03/2021"
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

    -- 🔹 Jahr aus dem Text extrahieren (steht immer nach dem letzten "/")
    AND CHARINDEX('/', bf.Description) > 0
    AND d.ToDocument = mm.MonthName + ' ' + 
        CAST(
            TRY_CAST(
                SUBSTRING(
                    bf.Description, 
                    LEN(bf.Description) - CHARINDEX('/', REVERSE(bf.Description)) + 2, 4
                ) AS INT
            ) AS NVARCHAR(10)
        );

