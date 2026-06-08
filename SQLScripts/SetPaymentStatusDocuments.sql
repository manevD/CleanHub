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
                AND
                (
                    --------------------------------------------------
                    -- 🟢 Fall 1: поединечни месеци
                    -- "за 01,02,03/2021"
                    --------------------------------------------------
                    (
                        CHARINDEX('/', bf.Description) > 0
                        AND EXISTS (
                            SELECT 1
                            FROM STRING_SPLIT(
                                LEFT(
                                    bf.Description,
                                    CHARINDEX('/', bf.Description) - 1
                                ),
                                ','
                            ) AS SplitMonths

                            WHERE TRY_CAST(
                                LTRIM(RTRIM(
                                    SUBSTRING(
                                        SplitMonths.value,
                                        PATINDEX('%[0-9]%', SplitMonths.value),
                                        2
                                    )
                                ))
                            AS INT) = TRY_CAST(mm.MonthNum AS INT)
                        )
                    )


                    --------------------------------------------------
                    -- 🟢 Fall 2: периоди
                    -- "од 01-05/2021"
                    -- "за 01-05/2021"
                    -- "01-05/2021"
                    --------------------------------------------------
                    OR
                    (
                        bf.Description LIKE N'%[0-9][0-9]-[0-9][0-9]/%'
                        AND CHARINDEX('-', bf.Description) > 0
                        AND CHARINDEX('/', bf.Description) > 0

                        AND TRY_CAST(
                            SUBSTRING(
                                bf.Description,
                                PATINDEX(
                                    '%[0-9][0-9]-[0-9][0-9]/%',
                                    bf.Description
                                ),
                                2
                            )
                        AS INT)
                        <= TRY_CAST(mm.MonthNum AS INT)


                        AND TRY_CAST(
                            SUBSTRING(
                                bf.Description,
                                CHARINDEX('-', bf.Description) + 1,
                                2
                            )
                        AS INT)
                        >= TRY_CAST(mm.MonthNum AS INT)
                    )
                )


                --------------------------------------------------
                -- година после /
                --------------------------------------------------
                AND CHARINDEX('/', REVERSE(bf.Description)) > 0

                AND d.ToDocument =
                    mm.MonthName + ' ' +
                    LEFT(
                        RIGHT(
                            bf.Description,
                            CHARINDEX('/', REVERSE(bf.Description)) - 1
                        ),
                        4
                    )
        )

        THEN 0   -- платено
        ELSE 1   -- неплатено
    END
FROM Documents d;


Update Documents set PaymentStatus = 0 where PaymentStatus = 1 and YEAR(Date) IN (2019, 2020, 2021, 2022, 2023)










Update Documents set PaymentStatus = 0 where Id = 35667
Update Documents set PaymentStatus = 0 where Id = 42439

Update Documents set PaymentStatus = 0 where Id = 30837

Update Documents set PaymentStatus = 0 where Id = 32130

Update Documents set PaymentStatus = 0 where Id = 53006
Update Documents set PaymentStatus = 0 where Id = 54357

Update Documents set PaymentStatus = 0 where Id = 4909

Update Documents set PaymentStatus = 0 where CustomerId = 605

Update Documents set PaymentStatus = 0 where Id = 9195
Update Documents set PaymentStatus = 0 where CustomerId = 847


Update Documents set PaymentStatus = 0 where CustomerId = 1146