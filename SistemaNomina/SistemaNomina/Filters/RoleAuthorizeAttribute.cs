using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SistemaNomina.Models;

namespace SistemaNomina.Filters
{
    // Atributo que puede usarse en clases (controladores) o métodos (acciones)
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class RoleAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string[] allowedRoles;

        // Constructor que acepta uno o varios roles
        public RoleAuthorizeAttribute(params string[] roles)
        {
            this.allowedRoles = roles;
        }

        // Método principal que realiza la autorización
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // Primero verifica si está autenticado
            if (!httpContext.User.Identity.IsAuthenticated)
                return false;

            using (var db = new smartbuilding_rhEntities())
            {
                var username = httpContext.User.Identity.Name;
                var user = db.Usuarios.FirstOrDefault(u => u.usuario == username);

                // Si no encuentra usuario o rol, acceso denegado
                if (user == null || user.Roles == null)
                    return false;

                // Verifica si el rol del usuario está entre los permitidos
                return allowedRoles.Contains(user.Roles.nombre);
            }
        }

        // Maneja el caso cuando el acceso es denegado
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                // Usuario autenticado pero sin permisos
                filterContext.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/Unauthorized.cshtml"
                };
            }
            else
            {
                // Usuario no autenticado - redirige al login
                base.HandleUnauthorizedRequest(filterContext);
            }
        }
    }
}