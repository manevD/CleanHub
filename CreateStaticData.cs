using CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.Entities.Enums;
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

        /// <summary>
        /// ДЕМО: 5 тест корисници (demo1…demo5@example.com, лозинка Demo2027!).
        /// Вистинските корисници (dimitar/marti) не постојат во демото — ако ги има, се бришат.
        /// </summary>
        public static async Task CreateUsers(IServiceProvider serviceProvider)
        {
            _userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            foreach (var oldEmail in new[] { "dimitar@email.com", "marti@email.com" })
            {
                var old = await _userManager.FindByEmailAsync(oldEmail);
                if (old != null) await _userManager.DeleteAsync(old);
            }

            const string demoPassword = "Demo2027!";
            for (var i = 1; i <= 5; i++)
            {
                var email = $"demo{i}@example.com";
                if (await _userManager.FindByEmailAsync(email) != null) continue;

                var user = new IdentityUser
                {
                    Id = $"demo-{i}",
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    TwoFactorEnabled = false,
                    LockoutEnabled = false,
                    PhoneNumberConfirmed = true
                };
                await _userManager.CreateAsync(user, demoPassword);
            }
        }

        #endregion

        #region Fields

        private static UserManager<IdentityUser> _userManager;

        #endregion
    }
}
