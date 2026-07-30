
-- za da se dodadat BuildingProducts treba site books , Customers i Buildings da se dodadeni 

;WITH LatestDocuments AS
(
    SELECT
        c.BuildingId,
        d.Id AS DocumentId,
        ROW_NUMBER() OVER
        (
            PARTITION BY c.BuildingId
            ORDER BY d.DateReceived DESC, d.Id DESC
        ) AS rn
    FROM Documents d
    INNER JOIN Customers c ON c.Id = d.CustomerId
    WHERE c.BuildingId IS NOT NULL
)
INSERT INTO BuildingProducts
(
    BuildingId,
    ArticleNotes,
    Input,
    Output,
    Price,
    PriceWithTax,
    Quantity,
    Tax,
    Total,
    UnitOfMeasurement
)
SELECT
    ld.BuildingId,

    LTRIM(RTRIM(
        CASE
            -- Чистење на влез за месец Мај 2026 -> Чистење на влез за
            WHEN CHARINDEX(N' за месец ', b.ArticleNotes) > 0
                THEN LEFT(
                    b.ArticleNotes,
                    CHARINDEX(N' за месец ', b.ArticleNotes) + 3
                )

            -- Одржување на сметки за 05/2026 -> Одржување на сметки за
            WHEN PATINDEX(N'% за [0-9]%/%', b.ArticleNotes) > 0
                THEN LEFT(
                    b.ArticleNotes,
                    CHARINDEX(
                        N' за ',
                        b.ArticleNotes,
                        PATINDEX(N'% за [0-9]%/%', b.ArticleNotes)
                    ) + 3
                )

            ELSE b.ArticleNotes
        END
    )) AS ArticleNotes,

    b.Input,
    b.Output,

    CASE
        WHEN b.ArticleNotes LIKE N'Потрошена електр%енергија%'
            THEN 0
        ELSE b.Price
    END AS Price,

    CASE
        WHEN b.ArticleNotes LIKE N'Потрошена електр%енергија%'
            THEN 0
        ELSE b.PriceWithTax
    END AS PriceWithTax,

    b.Quantity,
    b.Tax,

    CASE
        WHEN b.ArticleNotes LIKE N'Потрошена електр%енергија%'
            THEN 0
        ELSE b.Total
    END AS Total,

    b.UnitOfMeasurement

FROM LatestDocuments ld
INNER JOIN Books b
    ON b.DocId = ld.DocumentId
WHERE ld.rn = 1;

SELECT *
FROM BuildingProducts
ORDER BY BuildingId, ArticleNotes;