using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CleanHub.Extensions
{
    public static class ControllerExtensions
    {
        public static string RenderPartialViewToString(this Controller controller, string partialViewName, object model, ControllerContext context)
        {
            controller.ControllerContext = context; // Set the controller context

            controller.ViewData.Model = model;

            using (var sw = new StringWriter())
            {
                var engine = controller.HttpContext.RequestServices.GetService(typeof(ICompositeViewEngine)) as ICompositeViewEngine;
                var viewResult = engine.FindView(controller.ControllerContext, partialViewName, false);

                if (viewResult.View == null)
                {
                    throw new ArgumentNullException($"{partialViewName} does not match any available view");
                }

                var viewContext = new ViewContext(
                    controller.ControllerContext,
                    viewResult.View,
                    controller.ViewData,
                    controller.TempData,
                    sw,
                    new HtmlHelperOptions()
                );

                viewResult.View.RenderAsync(viewContext).GetAwaiter().GetResult();
                return sw.ToString();
            }
        }
    }
}