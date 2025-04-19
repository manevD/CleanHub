// Copyright (c) Webolar. All Rights Reserved. 
// Licensed under the Apache License, version 2.0

using AutoMapper;
using CleanHub.Entities;
using CleanHub.ViewModels;

namespace CleanHub;

public class App : Profile
{
    public App()
    {
        var readerSmallMapConfiguration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Customer, CustomerViewModel>()
                .ForMember(dest => dest.CustomerInfo, opts => opts.MapFrom(src => src.CustomerInfo))
                .ForMember(dest => dest.Email, opts => opts.Ignore())
                .ForMember(dest => dest.PhoneNumber, opts => opts.Ignore())
                .ForMember(dest => dest.Adress, opts => opts.Ignore())
                .ForMember(dest => dest.Inactive, opts => opts.Ignore())
                .ForMember(dest => dest.Building, opts => opts.Ignore())
                .ForMember(dest => dest.Activity, opts => opts.Ignore())
                .ForMember(dest => dest.Id, opts => opts.Ignore()).ReverseMap();
        });
        var readerMapConfiguration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Document, DocumentViewModel>()
                .ForMember(dest => dest.TotalBuildingOwes, opts => opts.Ignore())

                .ForMember(dest => dest.TotalBuildingDemands, opts => opts.Ignore())
                .ForMember(dest => dest.BuildingId, opts => opts.Ignore())
                .ForMember(dest => dest.CreatedTime, opts => opts.Ignore()) // Liste von Buildings
                // Ignoriere die anderen Felder, die nur im ViewModel sind
                .ForMember(dest => dest.Buildings, opts => opts.Ignore()) // Liste von Buildings
                .ForMember(dest => dest.Building, opts => opts.Ignore())  // Einzelnes Building-Objekt

                .ReverseMap();
            //.ForMember(dest => dest.Number, opts => opts.MapFrom(src => src.Number))
            //.ForMember(dest => dest.ToDocument, opts => opts.MapFrom(src => src.ToDocument))
            //.ForMember(dest => dest.Company, opts => opts.Ignore())
            // .ForMember(dest => dest.IsForPdf, opts => opts.Ignore())
            //.ForMember(dest => dest.Books, opts => opts.Ignore())
            //.ForMember(dest => dest.DateReceived, opts => opts.MapFrom(src => src.DateReceived))
            //.ForMember(dest => dest.Id, opts => opts.MapFrom(src => src.Id)).ReverseMap();


            cfg.CreateMap<BookFinancial, BookFinancialViewModel>().ReverseMap();

            cfg.CreateMap<Customer, CustomerViewModel>()
                .ForMember(dest => dest.CustomerInfo, opts => opts.MapFrom(src => src.CustomerInfo))
                .ForMember(dest => dest.Email, opts => opts.MapFrom(src => src.Email))
                .ForMember(dest => dest.PhoneNumber, opts => opts.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.Adress, opts => opts.MapFrom(src => src.Adress))
                .ForMember(dest => dest.Inactive, opts => opts.MapFrom(src => src.Inactive))
                 .ForMember(dest => dest.Building, opts => opts.Ignore())
                 .ForMember(dest => dest.Activity, opts => opts.Ignore())
                .ForMember(dest => dest.Id, opts => opts.MapFrom(src => src.Id)).ReverseMap();
        });
        var configuration = new MapperConfiguration(cfg =>
        {
            #region Customer

            cfg.CreateMap<Customer, CustomerViewModel>().ReverseMap();
            #endregion

            #region Product

            cfg.CreateMap<Product, ProductViewModel>().ReverseMap();
            #endregion

            #region BuildingProduct

            cfg.CreateMap<BuildingProduct, BuildingProductViewModel>()
                .ForMember(dest => dest.PriceWithTaxTotal, opts => opts.Ignore())
                .ForMember(dest => dest.GetFromReserve, opts => opts.Ignore())
                .ReverseMap();
            cfg.CreateMap<BuildingProduct, Product>().ReverseMap();
            cfg.CreateMap<BuildingProductViewModel, Product>().ReverseMap();

            #endregion

            #region Building
            cfg.CreateMap<Building, BuildingViewModel>()
                .ForMember(dest => dest.ReserveTotal, opts => opts.Ignore())
                .ForMember(dest => dest.ReserveFund, opts => opts.MapFrom(src => src.ReserveFund))
                .ForMember(dest => dest.BankAccount, opts => opts.MapFrom(src => src.BankAccount))
                .ForMember(dest => dest.Name, opts => opts.MapFrom(src => src.Name))
                .ForMember(dest => dest.Customers, opts => opts.MapFrom(src => src.Customers))
                .ForMember(dest => dest.BuildingProducts, opts => opts.MapFrom(src => src.BuildingProducts))
                .ForMember(dest => dest.Id, opts => opts.MapFrom(src => src.Id))
                .ReverseMap();
            #endregion

            #region BookFinancial

            cfg.CreateMap<BookFinancial, BookFinancialViewModel>().ReverseMap();

            #endregion

            #region Book

            cfg.CreateMap<Book, BookViewModel>().ReverseMap();

            #endregion

            #region Activity

            cfg.CreateMap<Activity, ActivityViewModel>().ReverseMap();

            #endregion

            #region Document

            cfg.CreateMap<DocumentViewModel, Document>();

            cfg.CreateMap<Document, DocumentViewModel>()

                .ForMember(dest => dest.TotalBuildingOwes, opts => opts.Ignore())
                .ForMember(dest => dest.TotalBuildingDemands, opts => opts.Ignore())
                      .ForMember(dest => dest.Company, opt => opt.Ignore()) // Ignore CompanyConfig in DocumentViewModel
                      .ForMember(dest => dest.IsForPdf, opt => opt.MapFrom(src => false)) // Set IsForPdf to false by default
                      .ForMember(dest => dest.Buildings, opt => opt.Ignore())
                      .ForMember(dest => dest.Building, opt => opt.Ignore())
                      .ForMember(dest => dest.Delay, opt => opt.Ignore())
                      .ForMember(dest => dest.ChargesInPercent, opt => opt.Ignore())
                      .ForMember(dest => dest.NewTotal, opt => opt.Ignore())
                      .ForMember(dest => dest.BuildingId, opt => opt.Ignore())
                      .ForMember(dest => dest.Books, opt => opt.MapFrom(src => src.Books));
            #endregion

            #region Invoice
            cfg.CreateMap<Invoice, InvoiceViewModel>().ReverseMap();

            #endregion

            #region SpecialInvoice

            cfg.CreateMap<SpecialInvoice, SpecialInvoiceViewModel>()
                .ForMember(dest => dest.Invoices, opt => opt.Ignore())
                .ForMember(dest => dest.BuildingName, opt => opt.MapFrom(src => src.Building.Name))
                .ReverseMap();

            cfg.CreateMap<BookFinancial, SpecialInvoiceViewModel>()
                .ForMember(dest => dest.BuildingName, opt => opt.MapFrom(src => src.Customer.Building.Name))
                .ForMember(dest => dest.BuildingId, opts => opts.Ignore())
                .ForMember(dest => dest.Building, opt => opt.MapFrom(src => src.Customer.Building))
                .ForMember(dest => dest.ForDate, opt => opt.MapFrom(src => src.DatumF))
                .ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.Document.TotalOutput))
                .ForMember(dest => dest.Invoices, opts => opts.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Document.PaymentStatus));

            #endregion

        });
        ReaderSmall = readerSmallMapConfiguration.CreateMapper();
        FullMapper = configuration.CreateMapper();
        ReaderMapper = readerMapConfiguration.CreateMapper();
        configuration.AssertConfigurationIsValid();
        // readerMapConfiguration.AssertConfigurationIsValid();
    }

    #region Properties
    public static IMapper ReaderSmall { get; set; } = null!;

    public static IMapper ReaderMapper { get; set; } = null!;
    public static IMapper FullMapper { get; set; } = null!;

    #endregion
}
