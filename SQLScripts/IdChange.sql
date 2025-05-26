-- Neue Tabelle mit KnigaFID ab 15687 erzeugen
SELECT 
    ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) + 15686 AS KnigaFID,  -- IDs ab 15687
    Nalog, SmetkaID, PartnerID, VidID, Broj, Opis, DatumF, Dolzi, Pobaruva,
    DolziDev, PobaruvaDev, Dokid, ValutaID, Kurs, SemaSubID, Datum, Valuta, ObjektID,
    Rasknizeno, GUID, VidID_komp, Vreme, VremePromena, TranzitPartnerID, KP_KI_Broj,
    tmp, VidID_Dok, VkupnoDok, LinkDok
INTO KnigaF_New
FROM KnigaF;

-- Optional: Sicherung der Originaltabelle
EXEC sp_rename 'KnigaF', 'KnigaF_Backup';

-- Neue Tabelle umbenennen
EXEC sp_rename 'KnigaF_New', 'KnigaF';
