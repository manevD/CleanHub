UPDATE c
SET c.Saldo =

    ISNULL
    (
        (
            SELECT SUM(k1.Pobaruva)
            FROM KnigaF k1
            WHERE k1.SmetkaID = 1200
            AND k1.PartnerID IN
            (
                SELECT p.PartnerID
                FROM Partneri p
                WHERE p.OddelID = po.OddelID
            )
        ),
        0
    )

    -

    ISNULL
    (
        (
            SELECT SUM(k2.Dolzi)
            FROM KnigaF k2
            WHERE k2.SmetkaID = 1200
            AND k2.PartnerID IN
            (
                SELECT p2.PartnerID
                FROM Partneri p2
                WHERE p2.OddelID = po.OddelID
            )
        ),
        0
    )

FROM [2026MartiNew].dbo.Customers c

INNER JOIN Partneri_Oddeli po
    ON po.OddelPartnerID = c.Id

INNER JOIN Partneri pr
    ON pr.PartnerID = c.Id

WHERE pr.DejnostID = 2;