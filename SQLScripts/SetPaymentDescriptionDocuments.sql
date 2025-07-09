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
SET d.PaymentDescription = bf.Description
FROM Documents d
JOIN Customers c ON c.Id = d.CustomerId
JOIN BookFinancials bf ON bf.CustomerId = c.Id
CROSS JOIN MonthMapping mm
WHERE
(
    -- Fall 1: "01,02,03/2019"
    (
        CHARINDEX('/', bf.Description) > 0
        AND EXISTS (
            SELECT 1 
            FROM STRING_SPLIT(
                LEFT(bf.Description, CHARINDEX('/', bf.Description) - 1),
                ','
            ) AS SplitMonths
            WHERE 
                TRY_CAST(LTRIM(RTRIM(
                    SUBSTRING(SplitMonths.value, PATINDEX('%[0-9]%', SplitMonths.value), 2)
                )) AS INT) = TRY_CAST(mm.MonthNum AS INT)
        )
    )

    -- Fall 2: "од 01-04/2019"
    OR (
        bf.Description LIKE N'%од %-%/%'
        AND CHARINDEX('-', bf.Description) > 0
        AND CHARINDEX('/', bf.Description) > 0
        AND TRY_CAST(SUBSTRING(bf.Description, CHARINDEX('од ', bf.Description) + 3, 2) AS INT) <= TRY_CAST(mm.MonthNum AS INT)
        AND TRY_CAST(SUBSTRING(bf.Description, CHARINDEX('-', bf.Description) + 1, 2) AS INT) >= TRY_CAST(mm.MonthNum AS INT)
    )

    -- Fall 3: "01/2025"
    OR (
        bf.Description LIKE '[0-9][0-9]/[0-9][0-9][0-9][0-9]'
        AND LEFT(bf.Description, 2) = mm.MonthNum
    )
)
AND d.ToDocument = mm.MonthName + ' ' +
    LEFT(
        RIGHT(bf.Description, CHARINDEX('/', REVERSE(bf.Description)) - 1), 
        4
    )
	