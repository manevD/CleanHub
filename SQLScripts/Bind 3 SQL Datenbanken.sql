

--SELECT COLUMN_NAME
--FROM INFORMATION_SCHEMA.COLUMNS
--WHERE TABLE_NAME = 'Partneri_Oddeli'
--ORDER BY ORDINAL_POSITION;

ALTER TABLE Kniga
DROP CONSTRAINT DF_Kniga_Kolicina41;

ALTER TABLE Kniga
DROP CONSTRAINT DF_Kniga_Cena3_1;

ALTER TABLE Kniga
DROP CONSTRAINT DF_Kniga_Kolicina51;

ALTER TABLE Kniga
DROP COLUMN Kolicina6;


SET IDENTITY_INSERT Kniga ON;

-- Insert from 2019MartiHigiena
INSERT INTO Kniga (
    KnigaID, Dokid, ArtikalID, Vlez, Izlez, Kolicina, Cenav, Cenai, Cena, Cena2,
    Rabat, Danok, Kutii, Marza, ArtikalRepro, Vkupno, PrenosID, VratenaRoba, ArtikalZabeleska,
    NaimID, CenaNab, PomosnaID, DokIDP, Kolicina2, Kolicina3, Kolicina4, DatumRok,
    Serija, EdmK, SN, Hidden, PrenosKnGUID, SmetkaID, RezervacijaKol,
    Sync_GUID, SyncID_GUID, tmp, Rabat1, Rabat2, Rabat3, Da_Ne
)
SELECT 
    KnigaID, Dokid, ArtikalID, Vlez, Izlez, Kolicina, Cenav, Cenai, Cena, Cena2,
    Rabat, Danok, Kutii, Marza, ArtikalRepro, Vkupno, PrenosID, VratenaRoba, ArtikalZabeleska,
    NaimID, CenaNab, PomosnaID, DokIDP, Kolicina2, Kolicina3, Kolicina4, DatumRok,
    Serija, EdmK, SN, Hidden, PrenosKnGUID, SmetkaID, RezervacijaKol,
    Sync_GUID, SyncID_GUID, tmp, Rabat1, Rabat2, Rabat3, Da_Ne
FROM [2019MartiHigiena].dbo.Kniga
WHERE KnigaID NOT IN (SELECT KnigaID FROM Kniga);

-- Insert from 2021MartiHigiena
INSERT INTO Kniga (
    KnigaID, Dokid, ArtikalID, Vlez, Izlez, Kolicina, Cenav, Cenai, Cena, Cena2,
    Rabat, Danok, Kutii, Marza, ArtikalRepro, Vkupno, PrenosID, VratenaRoba, ArtikalZabeleska,
    NaimID, CenaNab, PomosnaID, DokIDP, Kolicina2, Kolicina3, Kolicina4, DatumRok,
    Serija, EdmK, SN, Hidden, PrenosKnGUID, SmetkaID, RezervacijaKol,
    Sync_GUID, SyncID_GUID, tmp, Rabat1, Rabat2, Rabat3, Da_Ne
)
SELECT 
    KnigaID, Dokid, ArtikalID, Vlez, Izlez, Kolicina, Cenav, Cenai, Cena, Cena2,
    Rabat, Danok, Kutii, Marza, ArtikalRepro, Vkupno, PrenosID, VratenaRoba, ArtikalZabeleska,
    NaimID, CenaNab, PomosnaID, DokIDP, Kolicina2, Kolicina3, Kolicina4, DatumRok,
    Serija, EdmK, SN, Hidden, PrenosKnGUID, SmetkaID, RezervacijaKol,
    Sync_GUID, SyncID_GUID, tmp, Rabat1, Rabat2, Rabat3, Da_Ne
FROM [2021MartiHigiena].dbo.Kniga
WHERE KnigaID NOT IN (SELECT KnigaID FROM Kniga);

SET IDENTITY_INSERT Kniga OFF;



-- Partneri_Dejnosti
SET IDENTITY_INSERT Partneri_Dejnosti ON;

-- Insert from 2019MartiHigiena
INSERT INTO Partneri_Dejnosti (DejnostID, Dejnost)
SELECT DejnostID, Dejnost
FROM [2019MartiHigiena].dbo.Partneri_Dejnosti
WHERE DejnostID NOT IN (SELECT DejnostID FROM Partneri_Dejnosti);

-- Insert from 2021MartiHigiena
INSERT INTO Partneri_Dejnosti (DejnostID, Dejnost)
SELECT DejnostID, Dejnost
FROM [2021MartiHigiena].dbo.Partneri_Dejnosti
WHERE DejnostID NOT IN (SELECT DejnostID FROM Partneri_Dejnosti);

SET IDENTITY_INSERT Partneri_Dejnosti OFF;


SET IDENTITY_INSERT Banki ON;

-- Insert from 2019MartiHigiena
INSERT INTO Banki (BankaID, Banka)
SELECT BankaID, Banka
FROM [2019MartiHigiena].dbo.Banki
WHERE BankaID NOT IN (SELECT BankaID FROM Banki);

-- Insert from 2021MartiHigiena
INSERT INTO Banki (BankaID, Banka)
SELECT BankaID, Banka
FROM [2021MartiHigiena].dbo.Banki
WHERE BankaID NOT IN (SELECT BankaID FROM Banki);

SET IDENTITY_INSERT Banki OFF;



SET IDENTITY_INSERT KnigaF ON;

-- Insert from 2019MartiHigiena
INSERT INTO KnigaF (
    KnigaFID, Nalog, SmetkaID, PartnerID, VidID, Broj, Opis, DatumF, Dolzi, Pobaruva,
    DolziDev, PobaruvaDev, Dokid, ValutaID, Kurs, SemaSubID, Datum, Valuta, ObjektID, 
    Rasknizeno, GUID, VidID_komp, Vreme, VremePromena, TranzitPartnerID, KP_KI_Broj, 
    tmp, VidID_Dok, VkupnoDok, LinkDok
)
SELECT 
    KnigaFID, Nalog, SmetkaID, PartnerID, VidID, Broj, Opis, DatumF, Dolzi, Pobaruva,
    DolziDev, PobaruvaDev, Dokid, ValutaID, Kurs, SemaSubID, Datum, Valuta, ObjektID, 
    Rasknizeno, GUID, VidID_komp, Vreme, VremePromena, TranzitPartnerID, KP_KI_Broj, 
    tmp, VidID_Dok, VkupnoDok, LinkDok
FROM [2019MartiHigiena].dbo.KnigaF
WHERE KnigaFID NOT IN (SELECT KnigaFID FROM KnigaF);

-- Insert from 2021MartiHigiena
INSERT INTO KnigaF (
    KnigaFID, Nalog, SmetkaID, PartnerID, VidID, Broj, Opis, DatumF, Dolzi, Pobaruva,
    DolziDev, PobaruvaDev, Dokid, ValutaID, Kurs, SemaSubID, Datum, Valuta, ObjektID, 
    Rasknizeno, GUID, VidID_komp, Vreme, VremePromena, TranzitPartnerID, KP_KI_Broj, 
    tmp, VidID_Dok, VkupnoDok, LinkDok
)
SELECT 
    KnigaFID, Nalog, SmetkaID, PartnerID, VidID, Broj, Opis, DatumF, Dolzi, Pobaruva,
    DolziDev, PobaruvaDev, Dokid, ValutaID, Kurs, SemaSubID, Datum, Valuta, ObjektID, 
    Rasknizeno, GUID, VidID_komp, Vreme, VremePromena, TranzitPartnerID, KP_KI_Broj, 
    tmp, VidID_Dok, VkupnoDok, LinkDok
FROM [2021MartiHigiena].dbo.KnigaF
WHERE KnigaFID NOT IN (SELECT KnigaFID FROM KnigaF);

SET IDENTITY_INSERT KnigaF OFF;


SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Partneri'
ORDER BY ORDINAL_POSITION;

SET IDENTITY_INSERT Partneri ON;

-- Insert from 2019MartiHigiena
INSERT INTO Partneri (
    PartnerID, Partner, parAdresa, parMestoID, parNaselbaID, parTel, parKontakt, parEmail, 
    parWeb, DB, parRabat, parDena, parVidCenaID, PartnerOpis, parNeaktiven, parNeaktivenDatum,
    OddelID, LimitDena, LimitIznos, LimitDospean, DejnostID, PartnerLatin, FizickoLice, MB,
    LK, LK_MVR, PotrosenaVoda, Komunalii, Kanalizacija, OpstinaID, PartnerBroj, DrzavaID, OldID,
    Pausalno, EMB, KarticaVid, KarticaKod, KarticaValidnaOd, KarticaValidnaDo
)
SELECT 
    PartnerID, Partner, parAdresa, parMestoID, parNaselbaID, parTel, parKontakt, parEmail, 
    parWeb, DB, parRabat, parDena, parVidCenaID, PartnerOpis, parNeaktiven, parNeaktivenDatum,
    OddelID, LimitDena, LimitIznos, LimitDospean, DejnostID, PartnerLatin, FizickoLice, MB,
    LK, LK_MVR, PotrosenaVoda, Komunalii, Kanalizacija, OpstinaID, PartnerBroj, DrzavaID, OldID,
    Pausalno, EMB, KarticaVid, KarticaKod, KarticaValidnaOd, KarticaValidnaDo
FROM [2019MartiHigiena].dbo.Partneri
WHERE PartnerID NOT IN (SELECT PartnerID FROM Partneri);

-- Insert from 2021MartiHigiena
INSERT INTO Partneri (
    PartnerID, Partner, parAdresa, parMestoID, parNaselbaID, parTel, parKontakt, parEmail, 
    parWeb, DB, parRabat, parDena, parVidCenaID, PartnerOpis, parNeaktiven, parNeaktivenDatum,
    OddelID, LimitDena, LimitIznos, LimitDospean, DejnostID, PartnerLatin, FizickoLice, MB,
    LK, LK_MVR, PotrosenaVoda, Komunalii, Kanalizacija, OpstinaID, PartnerBroj, DrzavaID, OldID,
    Pausalno, EMB, KarticaVid, KarticaKod, KarticaValidnaOd, KarticaValidnaDo
)
SELECT 
    PartnerID, Partner, parAdresa, parMestoID, parNaselbaID, parTel, parKontakt, parEmail, 
    parWeb, DB, parRabat, parDena, parVidCenaID, PartnerOpis, parNeaktiven, parNeaktivenDatum,
    OddelID, LimitDena, LimitIznos, LimitDospean, DejnostID, PartnerLatin, FizickoLice, MB,
    LK, LK_MVR, PotrosenaVoda, Komunalii, Kanalizacija, OpstinaID, PartnerBroj, DrzavaID, OldID,
    Pausalno, EMB, KarticaVid, KarticaKod, KarticaValidnaOd, KarticaValidnaDo
FROM [2021MartiHigiena].dbo.Partneri
WHERE PartnerID NOT IN (SELECT PartnerID FROM Partneri);

SET IDENTITY_INSERT Partneri OFF;


SET IDENTITY_INSERT VidDokumenti ON;

-- Insert from 2019MartiHigiena
INSERT INTO VidDokumenti (VidID, VidDokument, IndeksID, Skr)
SELECT VidID, VidDokument, IndeksID, Skr
FROM [2019MartiHigiena].dbo.VidDokumenti
WHERE VidID NOT IN (SELECT VidID FROM VidDokumenti);

-- Insert from 2021MartiHigiena
INSERT INTO VidDokumenti (VidID, VidDokument, IndeksID, Skr)
SELECT VidID, VidDokument, IndeksID, Skr
FROM [2021MartiHigiena].dbo.VidDokumenti
WHERE VidID NOT IN (SELECT VidID FROM VidDokumenti);

SET IDENTITY_INSERT VidDokumenti OFF;

-- Smetki

-- Insert from 2019MartiHigiena
INSERT INTO Smetki (SmetkaID, Smetka, KarticaPar, Dev, IsNab, IsTrosok, SmetkaID_old, VonBilans, ParObjekt)
SELECT SmetkaID, Smetka, KarticaPar, Dev, IsNab, IsTrosok, SmetkaID_old, VonBilans, ParObjekt
FROM [2019MartiHigiena].dbo.Smetki
WHERE SmetkaID NOT IN (SELECT SmetkaID FROM Smetki);

-- Insert from 2021MartiHigiena
INSERT INTO Smetki (SmetkaID, Smetka, KarticaPar, Dev, IsNab, IsTrosok, SmetkaID_old, VonBilans, ParObjekt)
SELECT SmetkaID, Smetka, KarticaPar, Dev, IsNab, IsTrosok, SmetkaID_old, VonBilans, ParObjekt
FROM [2021MartiHigiena].dbo.Smetki
WHERE SmetkaID NOT IN (SELECT SmetkaID FROM Smetki);

-- Dokumenti
SET IDENTITY_INSERT Dokumenti ON;

-- Insert from 2019MartiHigiena
INSERT INTO Dokumenti (
    Dokid, Broj, Datum, PartnerID, IndeksID, ObjektID, ObjektPrenosID, PoDokument, 
    OpisDok, RabatDok, ValutaID, Kurs, Valuta, KorisnikId, SmenaID, VkupnoVl, VkupnoIz, 
    Plateno, Kompjuter, TMP, Tranzit, Fis, DokidP, PrenosID, VidCenaID, 
    Vreme, Kasa, VratenaRoba_Dok, BezArtikli, IntIspPriCeniZakluceni, VkupnoFaktura, 
    VkupnoValuta, DDV_Uvoz, DDV_Doma, Prevoz, Carina, ZavisniTroskovi, VidPlakane, 
    Carinarnica, Spedicija, Transport, PrenosGUID, SmetkaID, BezDanok, VremePromena, 
    DatumFis, AutoLock, RabatNavremeno, Rati, BrRati, Naracka, strTrasport, Uslovi, bCekanje, 
    bZavrsen, bJaveno, bPodignato, Avans, bSopstvenServis, PartnerObjektID, PrenosDokID, 
    bNaturalenRabat, DistributerID, VoziloID, VozacID, LotBr, IzrabotilID, bStorno, Sync_GUID, 
    Vreme_1, Vreme_2, SyncID_GUID, KodPlakanje, bHideMat
)
SELECT 
    Dokid, Broj, Datum, PartnerID, IndeksID, ObjektID, ObjektPrenosID, PoDokument, 
    OpisDok, RabatDok, ValutaID, Kurs, Valuta, KorisnikId, SmenaID, VkupnoVl, VkupnoIz, 
    Plateno, Kompjuter, TMP, Tranzit, Fis, DokidP, PrenosID, VidCenaID, 
    Vreme, Kasa, VratenaRoba_Dok, BezArtikli, IntIspPriCeniZakluceni, VkupnoFaktura, 
    VkupnoValuta, DDV_Uvoz, DDV_Doma, Prevoz, Carina, ZavisniTroskovi, VidPlakane, 
    Carinarnica, Spedicija, Transport, PrenosGUID, SmetkaID, BezDanok, VremePromena, 
    DatumFis, AutoLock, RabatNavremeno, Rati, BrRati, Naracka, strTrasport, Uslovi, bCekanje, 
    bZavrsen, bJaveno, bPodignato, Avans, bSopstvenServis, PartnerObjektID, PrenosDokID, 
    bNaturalenRabat, DistributerID, VoziloID, VozacID, LotBr, IzrabotilID, bStorno, Sync_GUID, 
    Vreme_1, Vreme_2, SyncID_GUID, KodPlakanje, bHideMat
FROM [2019MartiHigiena].dbo.Dokumenti
WHERE Dokid NOT IN (SELECT Dokid FROM Dokumenti);

-- Insert from 2021MartiHigiena
INSERT INTO Dokumenti (
    Dokid, Broj, Datum, PartnerID, IndeksID, ObjektID, ObjektPrenosID, PoDokument, 
    OpisDok, RabatDok, ValutaID, Kurs, Valuta, KorisnikId, SmenaID, VkupnoVl, VkupnoIz, 
    Plateno, Kompjuter, TMP, Tranzit, Fis, DokidP, PrenosID, VidCenaID, 
    Vreme, Kasa, VratenaRoba_Dok, BezArtikli, IntIspPriCeniZakluceni, VkupnoFaktura, 
    VkupnoValuta, DDV_Uvoz, DDV_Doma, Prevoz, Carina, ZavisniTroskovi, VidPlakane, 
    Carinarnica, Spedicija, Transport, PrenosGUID, SmetkaID, BezDanok, VremePromena, 
    DatumFis, AutoLock, RabatNavremeno, Rati, BrRati, Naracka, strTrasport, Uslovi, bCekanje, 
    bZavrsen, bJaveno, bPodignato, Avans, bSopstvenServis, PartnerObjektID, PrenosDokID, 
    bNaturalenRabat, DistributerID, VoziloID, VozacID, LotBr, IzrabotilID, bStorno, Sync_GUID, 
    Vreme_1, Vreme_2, SyncID_GUID, KodPlakanje, bHideMat, StatusDokID
)
SELECT 
    Dokid, Broj, Datum, PartnerID, IndeksID, ObjektID, ObjektPrenosID, PoDokument, 
    OpisDok, RabatDok, ValutaID, Kurs, Valuta, KorisnikId, SmenaID, VkupnoVl, VkupnoIz, 
    Plateno, Kompjuter, TMP, Tranzit, Fis, DokidP, PrenosID, VidCenaID, 
    Vreme, Kasa, VratenaRoba_Dok, BezArtikli, IntIspPriCeniZakluceni, VkupnoFaktura, 
    VkupnoValuta, DDV_Uvoz, DDV_Doma, Prevoz, Carina, ZavisniTroskovi, VidPlakane, 
    Carinarnica, Spedicija, Transport, PrenosGUID, SmetkaID, BezDanok, VremePromena, 
    DatumFis, AutoLock, RabatNavremeno, Rati, BrRati, Naracka, strTrasport, Uslovi, bCekanje, 
    bZavrsen, bJaveno, bPodignato, Avans, bSopstvenServis, PartnerObjektID, PrenosDokID, 
    bNaturalenRabat, DistributerID, VoziloID, VozacID, LotBr, IzrabotilID, bStorno, Sync_GUID, 
    Vreme_1, Vreme_2, SyncID_GUID, KodPlakanje, bHideMat, StatusDokID
FROM [2021MartiHigiena].dbo.Dokumenti
WHERE Dokid NOT IN (SELECT Dokid FROM Dokumenti);

SET IDENTITY_INSERT Dokumenti OFF;



SET IDENTITY_INSERT KnigaF_sub ON;

-- Insert from 2019MartiHigiena
INSERT INTO KnigaF_sub (
    KnigaF_subID, KnigaFID, DatumSub, DolziSub, PobaruvaSub
)
SELECT 
    KnigaF_subID, KnigaFID, DatumSub, DolziSub, PobaruvaSub
FROM [2019MartiHigiena].dbo.KnigaF_sub
WHERE KnigaF_subID NOT IN (SELECT KnigaF_subID FROM KnigaF_sub);

-- Insert from 2021MartiHigiena
INSERT INTO KnigaF_sub (
    KnigaF_subID, KnigaFID, DatumSub, DolziSub, PobaruvaSub
)
SELECT 
    KnigaF_subID, KnigaFID, DatumSub, DolziSub, PobaruvaSub
FROM [2021MartiHigiena].dbo.KnigaF_sub
WHERE KnigaF_subID NOT IN (SELECT KnigaF_subID FROM KnigaF_sub);

SET IDENTITY_INSERT KnigaF_sub OFF;


-- Insert from 2019MartiHigiena
INSERT INTO Artikli_Vidovi (
    VidArtID, VidArtikal, PresmNab, Skr
)
SELECT 
    VidArtID, VidArtikal, PresmNab, Skr
FROM [2019MartiHigiena].dbo.Artikli_Vidovi
WHERE VidArtID NOT IN (SELECT VidArtID FROM Artikli_Vidovi);

-- Insert from 2021MartiHigiena
INSERT INTO Artikli_Vidovi (
    VidArtID, VidArtikal, PresmNab, Skr
)
SELECT 
    VidArtID, VidArtikal, PresmNab, Skr
FROM [2021MartiHigiena].dbo.Artikli_Vidovi
WHERE VidArtID NOT IN (SELECT VidArtID FROM Artikli_Vidovi);


SET IDENTITY_INSERT Partneri_Oddeli ON;

-- Insert from 2019MartiHigiena
INSERT INTO Partneri_Oddeli (
    OddelID, Oddel, OddelBanka, OddelPartnerID
)
SELECT 
    OddelID, Oddel, OddelBanka, OddelPartnerID
FROM [2019MartiHigiena].dbo.Partneri_Oddeli
WHERE OddelID NOT IN (SELECT OddelID FROM Partneri_Oddeli);

-- Insert from 2021MartiHigiena
INSERT INTO Partneri_Oddeli (
    OddelID, Oddel, OddelBanka, OddelPartnerID
)
SELECT 
    OddelID, Oddel, OddelBanka, OddelPartnerID
FROM [2021MartiHigiena].dbo.Partneri_Oddeli
WHERE OddelID NOT IN (SELECT OddelID FROM Partneri_Oddeli);

SET IDENTITY_INSERT Partneri_Oddeli OFF;