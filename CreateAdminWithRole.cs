using Microsoft.AspNetCore.Identity;

namespace CleanHub
{
    public class CreateAdminWithRole
    {
        #region Methods

        public static async Task Create(IServiceProvider serviceProvider)
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
