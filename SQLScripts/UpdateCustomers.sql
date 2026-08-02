
BEGIN TRANSACTION;

BEGIN TRY

    UPDATE c
    SET
        c.CustomerInfo   = p.Partner,
        c.Adress         = p.parAdresa,
        c.PhoneNumber    = p.parTel,
        c.Email          = p.parEmail,
        c.Web            = p.parWeb,
        c.Inactive       = ISNULL(p.parNeaktiven, 0),
        c.InactiveDatum  = p.parNeaktivenDatum,
        c.PhysicalPerson = p.FizickoLice,
        c.BuildingId     = p.OddelID,
        c.SetCost        = 0,
        c.ApartmentUnit  = 1,
        c.Hide           = 0,
        c.Garage         = 0
    FROM Customers c
    INNER JOIN [2026MartiNew].dbo.Partneri p
        ON c.Id = p.PartnerID;

    UPDATE Customers
    SET Inactive = 0
    WHERE Inactive IS NULL;

    COMMIT TRANSACTION;

    PRINT 'Data updated successfully.';

END TRY
BEGIN CATCH

    ROLLBACK TRANSACTION;

    PRINT 'Error occurred. Transaction rolled back.';
    THROW;

END CATCH;