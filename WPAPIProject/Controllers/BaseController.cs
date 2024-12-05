using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using WPAPIProject.Logic.Interfaces;

namespace WPAPIProject.Controllers
{
    public class BaseController : Controller
    {
        protected ISqls _sql;

        public BaseController(ISqls sql)
        {
            _sql = sql;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var hasAllowAnonymous = context.ActionDescriptor.EndpointMetadata
            .Any(em => em is AllowAnonymousAttribute);

            if (hasAllowAnonymous)
            {
                base.OnActionExecuting(context);
                return;
            }

            var kullanici = _sql.KullaniciGetir("Kullanici");
            if (kullanici == null)
            {
                context.Result = new RedirectToActionResult("Login", "Home", null);
            }

            base.OnActionExecuting(context);
        }
    }
}