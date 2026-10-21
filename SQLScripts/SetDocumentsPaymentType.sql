UPDATE d
SET d.PaymentType =
    CASE
        WHEN EXISTS (
            SELECT 1
            FROM BookFinancials bf
            WHERE bf.CustomerId = d.CustomerId
              AND bf.Description LIKE N'%ф-ра%'
        )
        THEN 2 -- Invoice

		WHEN EXISTS (
    SELECT 1
    FROM BookFinancials bf
    WHERE bf.CustomerId = d.CustomerId
      AND (
          bf.Description LIKE N'%извод%'
          OR bf.Description LIKE N'%к.п%'
      )
		)
		THEN 5 
        WHEN EXISTS (
            SELECT 1
            FROM BookFinancials bf
            WHERE bf.CustomerId = d.CustomerId
              AND bf.Description LIKE N'%претплата%'
        )
        THEN 4 -- Subscription

        WHEN EXISTS (
            SELECT 1
            FROM BookFinancials bf
            WHERE bf.CustomerId = d.CustomerId
              AND bf.Description LIKE N'%маил%'
        )
        THEN 3 -- Web

        ELSE d.PaymentType
    END
FROM Documents d;