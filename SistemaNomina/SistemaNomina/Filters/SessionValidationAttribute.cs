using System;
using System.Web.Mvc;
using SistemaNomina.Helpers;

namespace SistemaNomina.Filters
{
    public class SessionValidationAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Excluir acciones que no requieren autenticación
            var controller = filterContext.Controller.GetType().Name;
            var action = filterContext.ActionDescriptor.ActionName;

            if (IsPublicAction(controller, action))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            // 🔍 VALIDAR SESIÓN ANTES DE CADA ACCIÓN
            if (!SessionHelper.IsSessionValid())
            {
                // 🚨 SESIÓN INVÁLIDA - Si es petición AJAX, devolver JSON
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    filterContext.Result = new JsonResult
                    {
                        Data = new
                        {
                            success = false,
                            sessionExpired = true,
                            message = "Su sesión ha expirado. La página se recargará."
                        },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
                else
                {
                    // Redirigir al login
                    filterContext.Result = SessionHelper.RedirectToLogin();
                }
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        private bool IsPublicAction(string controller, string action)
        {
            // Acciones que no requieren autenticación
            return (controller == "UsuariosController" && action == "Login") ||
                   (controller == "HomeController" && action == "Index");
        }
    }
}