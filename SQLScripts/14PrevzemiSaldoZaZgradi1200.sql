UPDATE c
SET c.Saldo = x.TotalSaldo
FROM [2026MartiNew].dbo.Customers c

INNER JOIN
(
    SELECT 
        po.OddelPartnerID,
        SUM(
            ISNULL(kf.Pobaruva, 0) - ISNULL(d.VkupnoIz, 0)
        ) AS TotalSaldo
    FROM Partneri_Oddeli po

    INNER JOIN Partneri pr
        ON pr.OddelID = po.OddelID

    LEFT JOIN
    (
        SELECT PartnerID,
               SUM(VkupnoIz) AS VkupnoIz
        FROM Dokumenti
        GROUP BY PartnerID
    ) d
        ON d.PartnerID = pr.PartnerID

    LEFT JOIN
    (
        SELECT PartnerID,
               SUM(Pobaruva) AS Pobaruva
        FROM KnigaF
        WHERE SmetkaID = 1200
        GROUP BY PartnerID
    ) kf
        ON kf.PartnerID = pr.PartnerID

    WHERE po.OddelPartnerID IN
    (
        SELECT PartnerID
        FROM Partneri
        WHERE DejnostID = 2
    )

    GROUP BY po.OddelPartnerID
) x
    ON x.OddelPartnerID = c.Id;