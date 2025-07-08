using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace SistemaNomina.Helpers
{
    public static class SessionHelper
    {
        /// <summary>
        /// Valida si la sesión está activa y es válida
        /// </summary>
        public static bool IsSessionValid()
        {
            try
            {
                var currentUserId = HttpContext.Current.Session["UserId"];
                var currentUserRole = HttpContext.Current.Session["RolUsuario"];
                var currentUsername = HttpContext.Current.Session["Usuario"];

                // Verificar que todos los datos esenciales estén presentes
                return currentUserId != null &&
                       currentUserRole != null &&
                       !string.IsNullOrEmpty(currentUsername?.ToString()) &&
                       HttpContext.Current.User.Identity.IsAuthenticated;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Limpia la sesión completamente
        /// </summary>
        public static void ClearSession()
        {
            try
            {
                HttpContext.Current.Session.Clear();
                HttpContext.Current.Session.Abandon();
                FormsAuthentication.SignOut();
            }
            catch
            {
                // Si hay error, al menos intentar limpiar FormsAuth
                FormsAuthentication.SignOut();
            }
        }

        /// <summary>
        /// Redirige al login con mensaje apropiado
        /// </summary>
        public static ActionResult RedirectToLogin(string message = "Su sesión ha expirado. Por favor, inicie sesión nuevamente.")
        {
            ClearSession();
            var controller = new Controllers.UsuariosController();
            controller.TempData["SessionExpired"] = message;
            return new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "Usuarios", action = "Login" }));
        }

        /// <summary>
        /// Valida y renueva la sesión
        /// </summary>
        public static bool ValidateAndRenewSession()
        {
            if (!IsSessionValid())
            {
                return false;
            }

            // 🔧 RENOVAR EL TICKET DE AUTENTICACIÓN CORRECTAMENTE
            try
            {
                if (HttpContext.Current.User.Identity.IsAuthenticated)
                {
                    // Obtener el cookie de autenticación
                    var authCookie = HttpContext.Current.Request.Cookies[FormsAuthentication.FormsCookieName];

                    if (authCookie != null && !string.IsNullOrEmpty(authCookie.Value))
                    {
                        // Desencriptar el ticket
                        var authTicket = FormsAuthentication.Decrypt(authCookie.Value);

                        if (authTicket != null && !authTicket.Expired)
                        {
                            // Renovar el ticket si es necesario
                            FormsAuthentication.RenewTicketIfOld(authTicket);
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Si hay error en la renovación, la sesión sigue siendo válida
                // pero no se pudo renovar el ticket
            }

            return true;
        }

        /// <summary>
        /// 🔧 MÉTODO MEJORADO PARA RENOVAR SESIÓN
        /// </summary>
        public static bool RenewAuthenticationTicket()
        {
            try
            {
                if (HttpContext.Current.User.Identity.IsAuthenticated)
                {
                    var username = HttpContext.Current.User.Identity.Name;

                    // Crear un nuevo ticket de autenticación
                    var authTicket = new FormsAuthenticationTicket(
                        1, // versión
                        username,
                        DateTime.Now,
                        DateTime.Now.AddMinutes(FormsAuthentication.Timeout.TotalMinutes),
                        false, // isPersistent
                        string.Empty // userData
                    );

                    // Encriptar el ticket
                    var encryptedTicket = FormsAuthentication.Encrypt(authTicket);

                    // Crear nuevo cookie
                    var authCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
                    {
                        HttpOnly = true,
                        Secure = HttpContext.Current.Request.IsSecureConnection,
                        Path = FormsAuthentication.FormsCookiePath
                    };

                    // Actualizar el cookie en la respuesta
                    HttpContext.Current.Response.Cookies.Set(authCookie);

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al renovar ticket de autenticación: {ex.Message}");
            }

            return false;
        }
    }
}