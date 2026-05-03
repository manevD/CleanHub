
--SELECT 
--    SUM(bf.Dolzi) AS TotalDolzi
--FROM KnigaF bf
--INNER JOIN Partneri_Oddeli po 
--    ON bf.PartnerID = ISNULL(po.OddelPartnerID, po.OddelID)
--WHERE 
--    po.OddelID = 64
--    AND bf.SmetkaID = 1201;


--SELECT SUM(Pobaruva) 
--FROM KnigaF 
--WHERE PartnerID IN (
--    SELECT PartnerID 
--    FROM Partneri 
--    WHERE OddelID = 64
--)
--AND SmetkaID = 1201;






UPDATE b
SET b.Saldo = ISNULL(x.Saldo, 0)
FROM [2026MartiNew].dbo.Buildings b
LEFT JOIN (
    SELECT 
        p.OddelID,
        SUM(bf.Pobaruva) - SUM(bf.Dolzi) AS Saldo
    FROM KnigaF bf
    INNER JOIN Partneri p 
        ON bf.PartnerID = p.PartnerID
    WHERE bf.SmetkaID = 1201
    GROUP BY p.OddelID
) x 
    ON x.OddelID = b.Id;