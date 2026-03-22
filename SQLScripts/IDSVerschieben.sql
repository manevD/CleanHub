BEGIN TRY
BEGIN TRANSACTION;

--------------------------------------------------
-- Calculate safe offset
--------------------------------------------------
DECLARE @maxDokid INT = (SELECT MAX(Dokid) FROM Dokumenti);
DECLARE @offset INT = 2147483647 - @maxDokid - 1000;

--------------------------------------------------
-- Mapping table (Old -> New Dokid)
--------------------------------------------------
IF OBJECT_ID('tempdb..#DokMapping') IS NOT NULL DROP TABLE #DokMapping;

SELECT 
    Dokid AS OldDokid,
    Dokid + @offset AS NewDokid
INTO #DokMapping
FROM Dokumenti;

--------------------------------------------------
-- Dokumenti NEW
--------------------------------------------------
SELECT 
    m.NewDokid AS Dokid,
    d.Broj, d.Datum, d.PartnerID, d.IndeksID, d.ObjektID, d.ObjektPrenosID,
    d.PoDokument, d.OpisDok, d.RabatDok, d.ValutaID, d.Kurs, d.Valuta,
    d.KorisnikId, d.SmenaID, d.VkupnoVl, d.VkupnoIz, d.Plateno,
    d.Kompjuter, d.TMP, d.Tranzit, d.TranzitPartner, d.Fis,
    d.DokidP, d.PrenosID, d.VidCenaID, d.Vreme, d.Kasa,
    d.VratenaRoba_Dok, d.BezArtikli, d.IntIspPriCeniZakluceni,
    d.VkupnoFaktura, d.VkupnoValuta, d.DDV_Uvoz, d.DDV_Doma,
    d.Prevoz, d.Carina, d.ZavisniTroskovi, d.VidPlakane,
    d.Carinarnica, d.Spedicija, d.Transport, d.PrenosGUID,
    d.SmetkaID, d.BezDanok, d.VremePromena, d.DatumFis,
    d.AutoLock, d.RabatNavremeno, d.Rati, d.BrRati,
    d.Naracka, d.strTrasport, d.Uslovi, d.bCekanje,
    d.bZavrsen, d.bJaveno, d.bPodignato, d.Avans,
    d.bSopstvenServis, d.PartnerObjektID, d.PrenosDokID,
    d.bNaturalenRabat, d.DistributerID, d.VoziloID,
    d.VozacID, d.LotBr, d.IzrabotilID, d.bStorno,
    d.Sync_GUID, d.Vreme_1, d.Vreme_2, d.SyncID_GUID,
    d.KodPlakanje, d.bHideMat, d.StatusDokID
INTO Dokumenti_New
FROM Dokumenti d
JOIN #DokMapping m ON d.Dokid = m.OldDokid;

--------------------------------------------------
-- Kniga NEW
--------------------------------------------------
SELECT 
    k.KnigaID + @offset AS KnigaID,
    ISNULL(m.NewDokid, k.Dokid + @offset) AS Dokid,
    k.ArtikalID, k.Vlez, k.Izlez, k.Kolicina,
    k.Cenav, k.Cenai, k.Cena, k.Cena2,
    k.Rabat, k.Danok, k.Kutii, k.Marza,
    k.ArtikalRepro, k.Vkupno, k.PrenosID,
    k.VratenaRoba, k.ArtikalZabeleska,
    k.NaimID, k.CenaNab, k.PomosnaID,
    k.DokIDP, k.Kolicina2, k.Kolicina3, k.Kolicina4,
    k.DatumRok, k.Serija, k.EdmK, k.SN,
    k.Hidden, k.PrenosKnGUID, k.SmetkaID,
    k.RezervacijaKol, k.Sync_GUID, k.SyncID_GUID,
    k.tmp, k.Rabat1, k.Rabat2, k.Rabat3, k.Da_Ne
INTO Kniga_New
FROM Kniga k
LEFT JOIN #DokMapping m ON k.Dokid = m.OldDokid;

--------------------------------------------------
-- KnigaF NEW (FIXED 🔥)
--------------------------------------------------
SELECT 
    kf.KnigaFID + @offset AS KnigaFID,
    kf.Nalog, kf.SmetkaID, kf.PartnerID, kf.VidID,
    kf.Broj, kf.Opis, kf.DatumF,
    kf.Dolzi, kf.Pobaruva, kf.DolziDev, kf.PobaruvaDev,
    ISNULL(m.NewDokid, kf.Dokid + @offset) AS Dokid,
    kf.ValutaID, kf.Kurs, kf.SemaSubID, kf.Datum,
    kf.Valuta, kf.ObjektID, kf.Rasknizeno,
    kf.GUID, kf.VidID_komp, kf.Vreme, kf.VremePromena,
    kf.TranzitPartnerID, kf.KP_KI_Broj,
    kf.tmp, kf.VidID_Dok, kf.VkupnoDok, kf.LinkDok
INTO KnigaF_New
FROM KnigaF kf
LEFT JOIN #DokMapping m ON kf.Dokid = m.OldDokid;

--------------------------------------------------
-- Backup old tables
--------------------------------------------------
EXEC sp_rename 'Dokumenti', 'Dokumenti_Backup';
EXEC sp_rename 'Kniga', 'Kniga_Backup';
EXEC sp_rename 'KnigaF', 'KnigaF_Backup';

--------------------------------------------------
-- Rename new tables
--------------------------------------------------
EXEC sp_rename 'Dokumenti_New', 'Dokumenti';
EXEC sp_rename 'Kniga_New', 'Kniga';
EXEC sp_rename 'KnigaF_New', 'KnigaF';

COMMIT;

END TRY
BEGIN CATCH
    ROLLBACK;
    PRINT ERROR_MESSAGE();
END CATCH;