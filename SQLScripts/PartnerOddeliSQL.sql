

BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Buildings ON;

    INSERT INTO [2021MartiHigienaNew].dbo.Buildings(Id,Name,BankAccount)
    SELECT OddelID,Oddel,OddelBanka
    FROM [2021MartiHigiena122024].dbo.Partneri_Oddeli
    COMMIT TRANSACTION;
	    SET IDENTITY_INSERT [2021MartiHigienaNew].dbo.Buildings Off;

    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;

UPDATE e
SET e.CustomerRefId = oe.Id
FROM Buildings e
JOIN Customers oe ON e.Name LIKE oe.CustomerInfo;


BEGIN TRY
    -- Start der Transaktion
    BEGIN TRANSACTION

    -- Update-Anweisungen
    UPDATE Buildings SET CustomerRefId = 35 WHERE Id = 13;
    UPDATE Buildings SET CustomerRefId = 36 WHERE Id = 14;
    UPDATE Buildings SET CustomerRefId = 133 WHERE Id = 15;
    UPDATE Buildings SET CustomerRefId = 334 WHERE Id = 31;
    UPDATE Buildings SET CustomerRefId = 450 WHERE Id = 40;
    UPDATE Buildings SET CustomerRefId = 549 WHERE Id = 47;
    UPDATE Buildings SET CustomerRefId = 551 WHERE Id = 49;
    UPDATE Buildings SET CustomerRefId = 554 WHERE Id = 53;
    UPDATE Buildings SET CustomerRefId = 605 WHERE Id = 59;
    UPDATE Buildings SET CustomerRefId = 611 WHERE Id = 65;
    UPDATE Buildings SET CustomerRefId = 648 WHERE Id = 66;
    UPDATE Buildings SET CustomerRefId = 650 WHERE Id = 67;
    UPDATE Buildings SET CustomerRefId = 649 WHERE Id = 68;
    UPDATE Buildings SET CustomerRefId = 239 WHERE Id = 69;
    UPDATE Buildings SET CustomerRefId = 687 WHERE Id = 72;
    UPDATE Buildings SET CustomerRefId = 690 WHERE Id = 73;
    UPDATE Buildings SET CustomerRefId = 692 WHERE Id = 74;
    UPDATE Buildings SET CustomerRefId = 696 WHERE Id = 75;
    UPDATE Buildings SET CustomerRefId = 1167 WHERE Id = 76;
    UPDATE Buildings SET CustomerRefId = 711 WHERE Id = 77;
    UPDATE Buildings SET CustomerRefId = 733 WHERE Id = 78;
    UPDATE Buildings SET CustomerRefId = 823 WHERE Id = 83;
    UPDATE Buildings SET CustomerRefId = 844 WHERE Id = 85;
    UPDATE Buildings SET CustomerRefId = 845 WHERE Id = 86;
    UPDATE Buildings SET CustomerRefId = 1043 WHERE Id = 94;
    UPDATE Buildings SET CustomerRefId = 1047 WHERE Id = 95;
    UPDATE Buildings SET CustomerRefId = 1050 WHERE Id = 96;
    UPDATE Buildings SET CustomerRefId = 1117 WHERE Id = 99;
    UPDATE Buildings SET CustomerRefId = 1119 WHERE Id = 100;
    UPDATE Buildings SET CustomerRefId = 1189 WHERE Id = 107;
    UPDATE Buildings SET CustomerRefId = 1227 WHERE Id = 110;
    UPDATE Buildings SET CustomerRefId = 1277 WHERE Id = 114;
    UPDATE Buildings SET CustomerRefId = 1278 WHERE Id = 115;
    UPDATE Buildings SET CustomerRefId = 1281 WHERE Id = 116;
    UPDATE Buildings SET CustomerRefId = 1282 WHERE Id = 117;
    UPDATE Buildings SET CustomerRefId = 1285 WHERE Id = 118;
    UPDATE Buildings SET CustomerRefId = 1509 WHERE Id = 135;
    UPDATE Buildings SET CustomerRefId = 1560 WHERE Id = 138;
    UPDATE Buildings SET CustomerRefId = 600 WHERE Id = 57;
    UPDATE Buildings SET CustomerRefId = 1840 WHERE Id = 156;
    UPDATE Buildings SET CustomerRefId = 1701 WHERE Id = 145;
    UPDATE Buildings SET CustomerRefId = 1816 WHERE Id = 153;
    UPDATE Buildings SET CustomerRefId = 1814 WHERE Id = 152;
    UPDATE Buildings SET CustomerRefId = 1813 WHERE Id = 151;
    UPDATE Buildings SET CustomerRefId = 1707 WHERE Id = 147;

    -- Commit der Transaktion
    COMMIT TRANSACTION
END TRY
BEGIN CATCH
    -- Fehlerbehandlung: Rückgängig machen der Transaktion im Fehlerfall
    ROLLBACK TRANSACTION;

    -- Fehlernachricht ausgeben
    PRINT 'Fehler: ' + ERROR_MESSAGE();
END CATCH


BEGIN TRANSACTION;