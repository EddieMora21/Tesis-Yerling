using System;
using System.Linq;
using System.Web;
using SistemaNomina.Models;  

namespace SistemaNomina.Helpers
{
    public static class RoleHelper
    {
        public static bool UserHasRole(string roleName)
        {
            if (!HttpContext.Current.User.Identity.IsAuthenticated)
                return false;

            var db = new smartbuilding_rhEntities();
            var username = HttpContext.Current.User.Identity.Name;
            var user = db.Usuarios
                        .Include("Roles")
                        .FirstOrDefault(u => u.usuario == username);

            if (user?.Roles == null) return false;

            return user.Roles.nombre.Equals(roleName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool UserHasAnyRole(params string[] roleNames)
        {
            if (!HttpContext.Current.User.Identity.IsAuthenticated)
                return false;

            var db = new smartbuilding_rhEntities();
            var username = HttpContext.Current.User.Identity.Name;
            var user = db.Usuarios
                        .Include("Roles")
                        .FirstOrDefault(u => u.usuario == username);

            if (user?.Roles == null) return false;

            return roleNames.Any(r => user.Roles.nombre.Equals(r, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetCurrentUserRole()
        {
            if (!HttpContext.Current.User.Identity.IsAuthenticated)
                return string.Empty;

            var username = HttpContext.Current.User.Identity.Name;
            var db = new smartbuilding_rhEntities();
            var user = db.Usuarios
                        .Include("Roles")
                        .FirstOrDefault(u => u.usuario == username);

            return user?.Roles?.nombre ?? string.Empty;
        }
    }
}