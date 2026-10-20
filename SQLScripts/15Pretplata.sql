--;WITH CustomerSaldo AS
--(
--    SELECT
--        c.Id,
--        Saldo =
--            ISNULL(
--            (
--                SELECT SUM(ISNULL(Demands,0))
--                FROM BookFinancials bf
--                WHERE bf.CustomerId = c.Id
--                  AND (bf.InvoiceId = 1200 OR bf.DocumentTypId = 11)
--                  AND bf.InvoiceId <> 1201
--            ),0)
--            -
--            (
--                ISNULL(
--                (
--                    SELECT SUM(ISNULL(TotalOutput,0))
--                    FROM Documents d
--                    WHERE d.CustomerId = c.Id
--                      AND d.[Date] > '2021-01-01'
--                ),0)
--                +
--                ISNULL(
--                (
--                    SELECT SUM(ISNULL(Owes,0))
--                    FROM BookFinancials bf
--                    WHERE bf.CustomerId = c.Id
--                      AND (bf.InvoiceId = 1200 OR bf.DocumentTypId = 11)
--                      AND bf.InvoiceId <> 1201
--                      AND bf.Owes <> 0
--                      AND bf.DatumF >= '2021-01-01'
--                ),0)
--            )
--    FROM Customers c
--)

--UPDATE c
--SET c.Subscription = cs.Saldo
--FROM Customers c
--INNER JOIN CustomerSaldo cs
--    ON cs.Id = c.Id
--WHERE cs.Saldo > 0;






--Овој SQL ќе ја постави Subscription на разликата Pobaruva - dolzi за клиентите што имаат претплата, а за сите останати ќе постави NULL.
UPDATE c
SET c.Subscription =
    CASE
        WHEN calc.Pobaruva > calc.Dolzi
        THEN calc.Pobaruva - calc.Dolzi
        ELSE NULL
    END
FROM Customers c
CROSS APPLY
(
    SELECT
        (
            SELECT COALESCE(SUM(b.Demands), 0)
            FROM BookFinancials b
            WHERE b.CustomerId = c.Id
              AND b.InvoiceId = 1200
        ) AS Pobaruva,

        (
            SELECT COALESCE(SUM(d.TotalOutput), 0)
            FROM Documents d
            WHERE d.CustomerId = c.Id
              AND d.Date > '2021-01-01'
        )
        +
        (
            SELECT COALESCE(SUM(b.Owes), 0)
            FROM BookFinancials b
            WHERE b.CustomerId = c.Id
              AND b.InvoiceId = 1200
              AND b.Owes <> 0
              AND b.DatumF >= '2021-01-01'
        ) AS Dolzi
) calc;