BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT [2025MartiTest].dbo.Customers ON;

    INSERT INTO [2025MartiTest].dbo.Customers (Id,CustomerInfo, Adress, PhoneNumber, Email, Web, Inactive, InactiveDatum, ActivityId, PhysicalPerson, BuildingId)
    SELECT PartnerID,Partner, parAdresa, parTel, parEmail, parWeb, parNeaktiven, parNeaktivenDatum, DejnostID, FizickoLice, OddelID
    FROM [2025MartiHigiena].dbo.Partneri
      update Customers set Inactive = 0 where Inactive is null
    COMMIT TRANSACTION;
	SET IDENTITY_INSERT [2025MartiTest].dbo.Customers OFF;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;


update Customers set ActivityId = 3 where Id not In(
  184, 188, 183, 181, 187, 182, 190, 189, 191, 35, 36, 133, 185, 186, 180, 228, 229, 238, 240, 241,
  266, 276, 292, 308, 320, 368, 334, 734, 370, 372, 449, 382, 417, 448, 450, 451, 460, 484, 532,
  545, 546, 549, 550, 551, 552, 531, 192, 554, 553, 557, 575, 600, 596, 605, 607, 600, 612, 627,
  611, 648, 650, 649, 239, 669, 670, 687, 690, 692, 696, 1167, 711, 733, 712, 781, 782, 823, 827,
  844, 845, 847, 852, 873, 883, 931, 979, 1043, 1047, 1050, 1138, 1137, 1117, 1119, 1139, 1146,
  1155, 1172, 1189, 1191, 1213, 1227, 1228, 1265, 1276, 1277, 1278, 1281, 1282, 1285, 1289, 1298,
  1300, 1503, 1340, 1388, 1432, 1508, 1471, 1500, 1501, 1504, 1505, 1509, 1510, 1529, 1560, 1586,
  1587, 1623, 1661, 1664, 1700, 1701, 1707, 1719, 1731, 1812, 1813, 1814, 1816, 1817, 1837, 1840,
  1845, 74019
) 

update Customers set ActivityId = 1 where Id in (240,320,546,334,549,448,550,551,688,691,694,531,192,554,553,596,605,600,611,650,239,669,687,690,692,696,711,733,823,827,845,847,1043,1047,1050,1117,1119,133,1139,1146,1155,1189,1227,1265,1277,1281,1282,1289,1500,1501,1504,1505,1586,1623,1701,1813,1814,1816,1840)

update Customers set Hide = 1 where ActivityId != 3




update Customers set ApartmentUnit = 1 where ActivityId = 3

--bitno za tie so nemat ActiveDatum nema da gi pokazuva pri kreiranje na faktura
update Customers set ActiveDatum = '2026-01-01' where ActiveDatum is null and Inactive = 0
update Customers set ActivityId = 3 where ActivityId is null and id not in (2117,2116,2092,2091,2089,2061,2042,2019,2012,1983,1980,1979,1934,1913,1909,1908,1907,1906,1896,1881,1879,1875,1867,1866)
