using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace CleanHub.Attribute
{
    public class RequireLoginAttribute : TypeFilterAttribute
    {
        public RequireLoginAttribute() : base(typeof(RequireLoginFilter))
        {
        }

        private class RequireLoginFilter : IAsyncActionFilter
        {
            private readonly UserManager<IdentityUser> _userManager;
            private readonly SignInManager<IdentityUser> _signInManager;

            public RequireLoginFilter(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
            {
                _userManager = userManager;
                _signInManager = signInManager;
            }

            public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            {
                if (!_signInManager.IsSignedIn(context.HttpContext.User))
                {
                    // User is not authenticated, redirect to login page
                    context.Result = new RedirectToActionResult("Login", "Accounts", null);
                }
                else
                {
                    // User is authenticated, continue with the action
                    await next();
                }
            }
        }
    }
}
