--- ToDocument for company documents is set to the month and year of the document date in Macedonian language.
WITH MonthMapping AS
(
    SELECT 1 AS MonthNum, N'Јануари' AS MonthName UNION ALL
    SELECT 2, N'Февруари' UNION ALL
    SELECT 3, N'Март' UNION ALL
    SELECT 4, N'Април' UNION ALL
    SELECT 5, N'Мај' UNION ALL
    SELECT 6, N'Јуни' UNION ALL
    SELECT 7, N'Јули' UNION ALL
    SELECT 8, N'Август' UNION ALL
    SELECT 9, N'Септември' UNION ALL
    SELECT 10, N'Октомври' UNION ALL
    SELECT 11, N'Ноември' UNION ALL
    SELECT 12, N'Декември'
)

UPDATE d
SET d.ToDocument =
    mm.MonthName + N' ' + CAST(YEAR(d.[Date]) AS NVARCHAR(4))
FROM Documents d
INNER JOIN Customers c
    ON c.Id = d.CustomerId
INNER JOIN MonthMapping mm
    ON mm.MonthNum = MONTH(d.[Date])
WHERE c.ActivityId = 1
  AND d.ToDocument IS NULL
  AND d.[Date] IS NOT NULL;