UPDATE c
SET c.Saldo = ISNULL(k.SumPobaruva, 0) - ISNULL(d.SumVkupnoIz, 0)
FROM [2026MartiNew].dbo.Customers c
OUTER APPLY (
    SELECT SUM(VkupnoIz) AS SumVkupnoIz
    FROM Dokumenti d
    WHERE d.PartnerID = c.Id
) d
OUTER APPLY (
    SELECT SUM(Pobaruva) AS SumPobaruva
    FROM KnigaF k
    WHERE k.PartnerID = c.Id
      AND k.SmetkaID = 1200
) k
WHERE c.BuildingId IS NOT NULL

















--- Update Saldo plus Dolizi
UPDATE c
SET c.Saldo =
    c.Saldo - ISNULL(kf.Dolzi, 0)
FROM [2026MartiNew].dbo.Customers c
INNER JOIN Partneri pr
    ON pr.PartnerID = c.Id

LEFT JOIN
(
    SELECT PartnerID,
           SUM(Dolzi) AS Dolzi
    FROM KnigaF
    WHERE SmetkaID = 1200
      AND Opis LIKE N'%салдо%'
    GROUP BY PartnerID
) kf
    ON kf.PartnerID = pr.PartnerID

WHERE pr.DejnostID = 3
  AND kf.Dolzi IS NOT NULL;