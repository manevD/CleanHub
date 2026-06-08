UPDATE c
SET c.Subscription =
    ISNULL(k.SumPobaruva, 0) - ISNULL(d.SumVkupnoIz, 0)

FROM [db_aae56c_2025martitest].dbo.Customers c

OUTER APPLY
(
    SELECT SUM(VkupnoIz) AS SumVkupnoIz
    FROM Dokumenti d
    WHERE d.PartnerID = c.Id
) d

OUTER APPLY
(
    SELECT SUM(Pobaruva) AS SumPobaruva
    FROM KnigaF k
    WHERE k.PartnerID = c.Id
      AND k.SmetkaID = 1200
) k

WHERE c.BuildingId IS NOT NULL

-- само тие што имаат претплата
AND (
      ISNULL(k.SumPobaruva, 0)
      - ISNULL(d.SumVkupnoIz, 0)
    ) > 0;