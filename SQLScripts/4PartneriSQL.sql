BEGIN TRANSACTION;

BEGIN TRY
    SET IDENTITY_INSERT Customers ON;

    INSERT INTO Customers (Id,CustomerInfo, Adress, PhoneNumber, Email, Web, Inactive, InactiveDatum, ActivityId, PhysicalPerson, BuildingId,SetCost,ApartmentUnit,Hide,Garage)
    SELECT PartnerID,Partner, parAdresa, parTel, parEmail, parWeb, parNeaktiven, parNeaktivenDatum, DejnostID, FizickoLice, OddelID,0,1,0,0
    FROM [2026MartiNew].dbo.Partneri where PartnerId > 2343
     --vazno inace nema da gi dava stanarite pri kreiranje na smetka !!
      update Customers set ActiveDatum = '2025-01-31' where Inactive = 0 and ActiveDatum is null
      update Customers set Inactive = 0 where Inactive is null
    COMMIT TRANSACTION;
	SET IDENTITY_INSERT Customers OFF;
    PRINT 'Data inserted successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred. Transaction rolled back.';
    -- Optionally, you can re-throw the error to see more details
    THROW;
END CATCH;

--Bitno : Site sto nemaat ActivityId da se dodadat vo ActivityId = 1 (ostanati) i da se dodadat vo ActivityId = 3 (fizicko lice)
  update Customers set ActivityId = 1 WHERE ActivityId IS NULL
  AND CustomerInfo NOT LIKE N'%ст.%'
  AND CustomerInfo NOT LIKE N'%ст%'
  AND CustomerInfo NOT LIKE N'%стан%';
 update Customers set ActivityId = 3 WHERE ActivityId IS NULL

 -- Bitno tie se firmi so se kako stanari dodadeni a  mora da imaat ActivityId 1 
 Update Customers set ActivityId = 1 where Id in (
619,35,36,133,600,1505,
771 ,
1117 ,
1155 ,
1227  ,
1282 ,
1586,
1906,1908,1909,1979,2042,2117,2184,2185 )



Update Customers set ActivityId = 3  where Id not In(
  184, 188, 183, 181, 187, 182, 190, 189, 191, 35, 36, 133, 185, 186, 180, 228, 229, 238, 240, 241,
  266, 276, 292, 308, 320, 368, 334, 734, 370, 372, 449, 382, 417, 448, 450, 451, 460, 484, 532,
  545, 546, 549, 550, 551, 552, 531, 192, 554, 553, 557, 575, 600, 596, 605, 607, 600, 612, 627,
  611, 648, 650, 649, 239, 669, 670, 687, 690, 692, 696, 1167, 711, 733, 712, 781, 782, 823, 827,
  844, 845, 847, 852, 873, 883, 931, 979, 1043, 1047, 1050, 1138, 1137, 1117, 1119, 1139, 1146,
  1155, 1172, 1189, 1191, 1213, 1227, 1228, 1265, 1276, 1277, 1278, 1281, 1282, 1285, 1289, 1298,
  1300, 1503, 1340, 1388, 1432, 1508, 1471, 1500, 1501, 1504, 1505, 1509, 1510, 1529, 1560, 1586,
  1587, 1623, 1661, 1664, 1700, 1701, 1707, 1719, 1731, 1812, 1813, 1814, 1816, 1817, 1837, 1840,
  1845, 74019,775,776,777,778,846,847,850,882,905,945,951,953,954,977,1023
)  and ActivityId is null

--Update Subscription
UPDATE c
SET c.Subscription = cs.Subscription
FROM Customers c
INNER JOIN [NewTestOld].dbo.Customers cs
    ON c.Id = cs.Id;


select Id,CustomerInfo,Adress from Customers where ActivityId is null

Update Customers set ActivityId = 3   where Id in (1700,1837)
Update Customers set ActivityId = 1   where Id in (1278,1340,1388,1471,1503)
Update Customers set ActivityId = 2   where Id in (1138)

Update Customers set ActivityId = 1   where Id in (1278,1285,1509,240,320,546,334,549,448,550,551,688,691,694,531,192,554,553,596,605,600,611,650,239,669,687,690,692,696,711,733,823,827,845,847,1043,1047,1050,1117,1119,133,1139,1146,1155,1189,1227,1265,1277,1281,1282,1289,1500,1501,1504,1505,1586,1623,1701,1813,1814,1816,1840) and ActivityId is null





update Customers set ApartmentUnit = 1 where ActivityId = 3

--bitno za tie so nemat ActiveDatum nema da gi pokazuva pri kreiranje na faktura
update Customers set ActiveDatum = '2026-01-01' where ActiveDatum is null and Inactive = 0
update Customers set ActivityId = 3 where ActivityId is null and id not in (2117,2116,2092,2091,2089,2061,2042,2019,2012,1983,1980,1979,1934,1913,1909,1908,1907,1906,1896,1881,1879,1875,1867,1866)
