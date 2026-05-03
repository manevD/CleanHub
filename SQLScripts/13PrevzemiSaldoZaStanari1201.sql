
select Sum(Pobaruva) from KnigaF where PartnerID = 640 and SmetkaID = 1201
select Sum(Vkupno) from Kniga where Dokid in (select DokId from Dokumenti where PartnerID = 640)  and ArtikalZabeleska like N'%Резервен%'
