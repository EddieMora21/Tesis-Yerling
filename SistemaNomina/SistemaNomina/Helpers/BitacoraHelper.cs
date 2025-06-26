using System;
using System.Web;
using SistemaNomina.Models;

namespace SistemaNomina.Helpers
{
    public static class BitacoraHelper
    {
        /// <summary>
        /// Registra una acción en la bitácora automáticamente
        /// </summary>
        public static void RegistrarAccion(string accion, string detalle = null, int? userId = null)
        {
            try
            {
                using (var db = new smartbuilding_rhEntities())
                {
                    // Si no se proporciona userId, intentar obtenerlo de la sesión
                    if (!userId.HasValue && HttpContext.Current?.Session["UserId"] != null)
                    {
                        userId = (int?)HttpContext.Current.Session["UserId"];
                    }

                    var bitacora = new Bitacora
                    {
                        id_usuario = userId,
                        accion = accion,
                        detalle = detalle ?? "",
                        fecha_hora = DateTime.Now
                    };

                    db.Bitacora.Add(bitacora);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                // En caso de error, no fallar la aplicación
                // En producción, aquí se podría logging a archivo
                System.Diagnostics.Debug.WriteLine($"Error al registrar bitácora: {ex.Message}");
            }
        }

        /// <summary>
        /// Registra login exitoso
        /// </summary>
        public static void RegistrarLogin(int userId, string username)
        {
            string detalle = $"Usuario '{username}' inició sesión exitosamente desde IP: {GetClientIP()}";
            RegistrarAccion("LOGIN_EXITOSO", detalle, userId);
        }

        /// <summary>
        /// Registra intento de login fallido
        /// </summary>
        public static void RegistrarLoginFallido(string username)
        {
            string detalle = $"Intento de login fallido para usuario '{username}' desde IP: {GetClientIP()}";
            RegistrarAccion("LOGIN_FALLIDO", detalle);
        }

        /// <summary>
        /// Registra logout
        /// </summary>
        public static void RegistrarLogout(int userId, string username)
        {
            string detalle = $"Usuario '{username}' cerró sesión";
            RegistrarAccion("LOGOUT", detalle, userId);
        }

        /// <summary>
        /// Registra creación de usuario
        /// </summary>
        public static void RegistrarCreacionUsuario(int userId, string nuevoUsuario)
        {
            string detalle = $"Creado nuevo usuario: '{nuevoUsuario}'";
            RegistrarAccion("CREAR_USUARIO", detalle, userId);
        }

        /// <summary>
        /// Registra cambio de contraseña
        /// </summary>
        public static void RegistrarCambioContrasena(int userId, string username)
        {
            string detalle = $"Usuario '{username}' cambió su contraseña";
            RegistrarAccion("CAMBIO_CONTRASENA", detalle, userId);
        }

        /// <summary>
        /// Registra eliminación de usuario
        /// </summary>
        public static void RegistrarEliminacionUsuario(int userId, string usuarioEliminado)
        {
            string detalle = $"Eliminado usuario: '{usuarioEliminado}'";
            RegistrarAccion("ELIMINAR_USUARIO", detalle, userId);
        }

        /// <summary>
        /// Obtiene la IP del cliente
        /// </summary>
        private static string GetClientIP()
        {
            try
            {
                var request = HttpContext.Current?.Request;
                if (request == null) return "Desconocida";

                string ip = request.ServerVariables["HTTP_X_FORWARDED_FOR"];

                if (string.IsNullOrEmpty(ip))
                    ip = request.ServerVariables["REMOTE_ADDR"];

                if (string.IsNullOrEmpty(ip))
                    ip = request.UserHostAddress;

                return ip ?? "Desconocida";
            }
            catch
            {
                return "Desconocida";
            }
        }
    }
}