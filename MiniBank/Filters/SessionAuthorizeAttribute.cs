

using System;
using System.Web;
using System.Web.Mvc;

namespace MiniBank.Filters
{
    public class SessionAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var ctx = filterContext.HttpContext;
            // If session or user info is missing, redirect to Login
            if (ctx == null || ctx.Session == null || ctx.Session["UserId"] == null)
            {
                // preserve return url for convenience
                var returnUrl = ctx.Request.RawUrl;
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary(
                        new { controller = "Auth", action = "Login", ReturnUrl = returnUrl }
                    )
                );
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}