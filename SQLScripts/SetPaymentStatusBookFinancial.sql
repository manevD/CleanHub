UPDATE bf
SET bf.Status = 
    CASE 
        WHEN bf.Owes > 0 THEN 1
        ELSE 0
    END
FROM BookFinancials bf
INNER JOIN Customers c ON bf.CustomerId = c.Id