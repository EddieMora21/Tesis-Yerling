using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SistemaNomina.Helpers;

namespace SistemaNomina.Filters
{
    public class RoleAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string[] _allowedRoles;

        public RoleAuthorizeAttribute(params string[] roles)
        {
            _allowedRoles = roles;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // 🔍 VALIDACIÓN COMPLETA DE SESIÓN
            if (!SessionHelper.IsSessionValid())
            {
                return false;
            }

            try
            {
                var userRole = httpContext.Session["RolUsuario"]?.ToString();

                if (string.IsNullOrEmpty(userRole))
                {
                    return false;
                }

                // Verificar si el rol del usuario está en los roles permitidos
                return _allowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (!SessionHelper.IsSessionValid())
            {
                // 🚨 SESIÓN EXPIRADA - Redirigir al login
                filterContext.Result = SessionHelper.RedirectToLogin();
            }
            else
            {
                // 🚫 ACCESO DENEGADO - Mostrar página de error
                filterContext.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/Unauthorized.cshtml"
                };
            }
        }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            // Renovar sesión si es válida
            if (SessionHelper.IsSessionValid())
            {
                SessionHelper.ValidateAndRenewSession();
            }

            base.OnAuthorization(filterContext);
        }
    }
}