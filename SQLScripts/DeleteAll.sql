EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

-- Truncate alle Tabellen
EXEC sp_MSforeachtable 'TRUNCATE TABLE ?';

-- Aktiviere Constraints wieder
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';