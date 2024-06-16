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
        var readerMapConfiguration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Document, DocumentViewModel>()
            .ForMember(dest => dest.Number, opts => opts.MapFrom(src => src.Number))
            .ForMember(dest => dest.ToDocument, opts => opts.MapFrom(src => src.ToDocument))
            .ForMember(dest => dest.Company, opts => opts.Ignore())
            .ForMember(dest => dest.Books, opts => opts.Ignore())

            .ForMember(dest => dest.DateReceived, opts => opts.MapFrom(src => src.DateReceived))
            .ForMember(dest => dest.Id, opts => opts.MapFrom(src => src.Id)).ReverseMap();


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

            cfg.CreateMap<Customer, CustomerViewModel>()
                .ForMember(dest => dest.CustomerInfo, opts => opts.MapFrom(src => src.CustomerInfo))
                .ForMember(dest => dest.Email, opts => opts.MapFrom(src => src.Email))
                .ForMember(dest => dest.PhoneNumber, opts => opts.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.Web, opts => opts.MapFrom(src => src.Web))
                .ForMember(dest => dest.Adress, opts => opts.MapFrom(src => src.Adress))
                .ForMember(dest => dest.Inactive, opts => opts.MapFrom(src => src.Inactive))
                .ForMember(dest => dest.InactiveDatum, opts => opts.MapFrom(src => src.InactiveDatum))
                .ForMember(dest => dest.Id, opts => opts.MapFrom(src => src.Id))
                .ForMember(dest => dest.BuildingId, opts => opts.MapFrom(src => src.BuildingId))
                                .ForMember(dest => dest.Building, opts => opts.MapFrom(src => src.Building))

                .ForMember(dest => dest.ActivityId, opts => opts.MapFrom(src => src.ActivityId))
                .ForMember(dest => dest.PhysicalPerson, opts => opts.MapFrom(src => src.PhysicalPerson)).ReverseMap();
            #endregion

            #region Building

            cfg.CreateMap<Building, BuildingViewModel>()
          .ForMember(dest => dest.BankAccount, opts => opts.MapFrom(src => src.BankAccount))
          .ForMember(dest => dest.Name, opts => opts.MapFrom(src => src.Name))
          .ForMember(dest => dest.Customers, opts => opts.MapFrom(src => src.Customers))
          .ForMember(dest => dest.Id, opts => opts.MapFrom(src => src.Id)).ReverseMap();

            #endregion

            #region BookFinancial

            cfg.CreateMap<BookFinancial, BookFinancialViewModel>().ReverseMap();

            #endregion

            #region Book

            cfg.CreateMap<Book, BookViewModel>().ReverseMap();

            #endregion

            #region Article

            cfg.CreateMap<Article, ArticleViewModel>().ReverseMap();

            #endregion

            #region Activity

            cfg.CreateMap<Activity, ActivityViewModel>().ReverseMap();

            #endregion

            #region Document

            cfg.CreateMap<Document, DocumentViewModel>().ForMember(dest => dest.Company, opts => opts.Ignore()).ReverseMap();

            #endregion

            #region Invoice
            cfg.CreateMap<Invoice, InvoiceViewModel>().ReverseMap();

            #endregion

        });

        FullMapper = configuration.CreateMapper();
        ReaderMapper = readerMapConfiguration.CreateMapper();
        configuration.AssertConfigurationIsValid();
        readerMapConfiguration.AssertConfigurationIsValid();
    }

    #region Properties

    public static IMapper ReaderMapper { get; set; }
    public static IMapper FullMapper { get; set; }

    #endregion
}
