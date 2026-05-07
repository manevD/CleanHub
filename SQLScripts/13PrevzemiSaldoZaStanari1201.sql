update Partneri set DejnostID = 3 where DejnostID is null

UPDATE c
SET c.Saldo1201 = ISNULL(r.Vkupno, 0) - ISNULL(p.Pobaruva, 0)
FROM [2026MartiNew].dbo.Customers c
INNER JOIN Partneri pr ON pr.PartnerID = c.Id

LEFT JOIN
(
    SELECT d.PartnerID,
           SUM(k.Vkupno) AS Vkupno
    FROM Kniga k
    INNER JOIN Dokumenti d ON d.DokId = k.DokId
    WHERE k.ArtikalZabeleska LIKE N'%Резервен%'
    GROUP BY d.PartnerID
) r ON r.PartnerID = pr.PartnerID

LEFT JOIN
(
    SELECT PartnerID,
           SUM(Pobaruva) AS Pobaruva
    FROM KnigaF
    WHERE SmetkaID = 1201
    GROUP BY PartnerID
) p ON p.PartnerID = pr.PartnerID

WHERE pr.DejnostID = 3;