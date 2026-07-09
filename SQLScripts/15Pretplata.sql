;WITH CustomerSaldo AS
(
    SELECT
        c.Id,
        Saldo =
            ISNULL(
            (
                SELECT SUM(ISNULL(Demands,0))
                FROM BookFinancials bf
                WHERE bf.CustomerId = c.Id
                  AND (bf.InvoiceId = 1200 OR bf.DocumentTypId = 11)
                  AND bf.InvoiceId <> 1201
            ),0)
            -
            (
                ISNULL(
                (
                    SELECT SUM(ISNULL(TotalOutput,0))
                    FROM Documents d
                    WHERE d.CustomerId = c.Id
                      AND d.[Date] > '2021-01-01'
                ),0)
                +
                ISNULL(
                (
                    SELECT SUM(ISNULL(Owes,0))
                    FROM BookFinancials bf
                    WHERE bf.CustomerId = c.Id
                      AND (bf.InvoiceId = 1200 OR bf.DocumentTypId = 11)
                      AND bf.InvoiceId <> 1201
                      AND bf.Owes <> 0
                      AND bf.DatumF >= '2021-01-01'
                ),0)
            )
    FROM Customers c
)

UPDATE c
SET c.Subscription = cs.Saldo
FROM Customers c
INNER JOIN CustomerSaldo cs
    ON cs.Id = c.Id
WHERE cs.Saldo > 0;