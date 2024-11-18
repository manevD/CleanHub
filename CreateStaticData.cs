using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.Helpers;
using CleanHub.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CleanHub
{
    public class CreateStaticData
    {
        #region Methods

        public static void SetDocumentStatus()
        {
            using (var context = new ApplicationDbContext())
            {
                var customers =  context.Customers/*.Include(x => x.Activity).*/.Include(d => d.Documents).ToList();
                foreach (var customer in customers)
                {
                    foreach (var doc in customer.Documents)
                    {
                        if (doc.ToDocument != null)
                        {
                            var year = DocumentService.ExtractYear(doc.ToDocument);
                            var month = DocumentService.GetMonthAsInteger(doc.ToDocument);

                            var searchCriteria = string.Concat(month, "/", year);
                            string likePattern = $"%{searchCriteria.Replace("/", "%")}%";
                            if (!string.IsNullOrEmpty(likePattern))
                            {
                                var bookFinancial = context.BookFinancials
                                    .FirstOrDefault(x => EF.Functions.Like(x.Description, likePattern)
                                                         && x.InvoiceId == Constants.Recieve
                                                         && x.CustomerId == customer.Id);
                                if (bookFinancial == null)
                                {
                                    doc.PaymentStatus = PaymentStatus.Неплатено;
                                }
                                else
                                {
                                    doc.PaymentStatus = DocumentService.GetStatus(bookFinancial, doc);
                                }
                            }
                        }
                    }
                }
                context.SaveChanges();
            }
        }

        public static async Task CreateUsers(IServiceProvider serviceProvider)
        {
            _userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            IdentityUser anabelaUserExist = await _userManager.FindByEmailAsync("dimitar@email.com");

            if (anabelaUserExist == null)
            {
                var user = new IdentityUser
                {
                    Id= "1",
                    UserName = "dimitar@email.com",
                    Email = "dimitar@email.com",
                    NormalizedUserName = "dimitar@email.com",
                    NormalizedEmail= "dimitar@email.com",
                    EmailConfirmed = true,
                    TwoFactorEnabled = false,
                    LockoutEnabled = true,
                    PhoneNumberConfirmed=true,
                    AccessFailedCount=1
                };
                const string userPassword = "Hallo123!";
                await _userManager.CreateAsync(user, userPassword);
            }
        }

        #endregion

        #region Fields

        private static UserManager<IdentityUser> _userManager;

        #endregion
    }
}
