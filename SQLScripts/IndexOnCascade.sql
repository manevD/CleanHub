ALTER TABLE SpecialInvoices DROP CONSTRAINT FK_SpecialInvoices_Buildings_BuildingId;

ALTER TABLE SpecialInvoices
ADD CONSTRAINT FK_SpecialInvoices_Buildings_Id
FOREIGN KEY (BuildingId) REFERENCES Buildings(Id) ON DELETE CASCADE;

ALTER TABLE Customers DROP CONSTRAINT FK_Customers_Buildings_BuildingId;
ALTER TABLE Customers
ADD CONSTRAINT FK_Customers_Buildings_BuildingId
FOREIGN KEY (BuildingId) REFERENCES Buildings(Id) ON DELETE CASCADE;

ALTER TABLE Documents DROP CONSTRAINT FK_Documents_Customers_CustomerId;
ALTER TABLE Documents
ADD CONSTRAINT FK_Documents_Customers_CustomerId
FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE;



ALTER TABLE BookFinancials
DROP CONSTRAINT FK_BookFinancials_Documents_DocumentId;

ALTER TABLE BookFinancials
ADD CONSTRAINT FK_BookFinancials_Documents_DocumentId
FOREIGN KEY (DocumentId)
REFERENCES Documents(Id)
ON DELETE CASCADE;

ALTER TABLE BookFinancials
DROP CONSTRAINT FK_BookFinancials_Customers_CustomerId;

ALTER TABLE BookFinancials
ADD CONSTRAINT FK_BookFinancials_Customers_CustomerId
FOREIGN KEY (CustomerId)
REFERENCES Customers(Id)
