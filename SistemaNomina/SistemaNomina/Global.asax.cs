using System;
using System.ComponentModel;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
// Comentar temporalmente EPPlus hasta verificar instalación
// using OfficeOpenXml;
using SistemaNomina.Helpers;
using SistemaNomina.Models;

namespace SistemaNomina
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // 📋 CONFIGURACIÓN INICIAL DE LA APLICACIÓN
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // 🔧 CONFIGURACIÓN DE EPPlus PARA EXCEL (comentado temporalmente)
            // ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // 👤 CREAR USUARIO ADMINISTRADOR AUTOMÁTICAMENTE SI NO EXISTE
            CrearUsuarioAdministradorInicial();

            // 🛡️ CONFIGURACIÓN DE SEGURIDAD ADICIONAL
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new RazorViewEngine());

            System.Diagnostics.Debug.WriteLine("=== APLICACIÓN INICIADA CORRECTAMENTE ===");
        }

        // 🚨 MANEJAR ERRORES GLOBALES Y SESIONES EXPIRADAS
        protected void Application_Error()
        {
            try
            {
                var exception = Server.GetLastError();
                var httpContext = HttpContext.Current;

                // 📋 LOG DEL ERROR
                System.Diagnostics.Debug.WriteLine($"ERROR GLOBAL: {exception?.Message}");
                System.Diagnostics.Debug.WriteLine($"STACK TRACE: {exception?.StackTrace}");

                if (exception is HttpException httpEx)
                {
                    var statusCode = httpEx.GetHttpCode();
                    System.Diagnostics.Debug.WriteLine($"HTTP STATUS CODE: {statusCode}");

                    switch (statusCode)
                    {
                        case 401: // No autorizado
                            System.Diagnostics.Debug.WriteLine("ERROR 401 - Limpiando sesión y redirigiendo al login");
                            LimpiarSesionCompletamente();
                            RedirectToLogin("Su sesión ha expirado por inactividad.");
                            break;

                        case 403: // Acceso prohibido
                            System.Diagnostics.Debug.WriteLine("ERROR 403 - Acceso denegado");
                            LimpiarSesionCompletamente();
                            RedirectToLogin("Acceso denegado. Por favor, inicie sesión nuevamente.");
                            break;

                        case 404: // Página no encontrada
                            System.Diagnostics.Debug.WriteLine("ERROR 404 - Página no encontrada");
                            Server.ClearError();
                            Response.Redirect("~/Home/Index");
                            break;

                        case 500: // Error interno del servidor
                            System.Diagnostics.Debug.WriteLine("ERROR 500 - Error interno del servidor");
                            // Mantener el comportamiento por defecto para errores 500
                            break;

                        default:
                            System.Diagnostics.Debug.WriteLine($"ERROR {statusCode} - Error no manejado específicamente");
                            break;
                    }
                }
                else if (exception?.Message?.Contains("session") == true ||
                         exception?.Message?.Contains("authentication") == true)
                {
                    // 🕒 ERRORES RELACIONADOS CON SESIÓN O AUTENTICACIÓN
                    System.Diagnostics.Debug.WriteLine("ERROR DE SESIÓN/AUTENTICACIÓN - Limpiando sesión");
                    LimpiarSesionCompletamente();
                    RedirectToLogin("Ha ocurrido un problema con su sesión. Por favor, inicie sesión nuevamente.");
                }
            }
            catch (Exception ex)
            {
                // Si hay error en el manejo de errores, al menos registrarlo
                System.Diagnostics.Debug.WriteLine($"ERROR EN Application_Error: {ex.Message}");
            }
        }

        // ... resto del código permanece igual

        #region Métodos Auxiliares

        /// <summary>
        /// 👤 Crear usuario administrador inicial si no existe
        /// </summary>
        private void CrearUsuarioAdministradorInicial()
        {
            try
            {
                using (var db = new smartbuilding_rhEntities())
                {
                    // Verificar si ya existe un usuario administrador
                    if (!db.Usuarios.Any(u => u.usuario == "admin"))
                    {
                        System.Diagnostics.Debug.WriteLine("Creando usuario administrador inicial...");

                        // 🔐 CREAR CON CONTRASEÑA ENCRIPTADA
                        var adminUser = new Usuarios
                        {
                            id_empleado = 1, // Asegúrate de que existe el empleado con ID 1
                            usuario = "admin",
                            contrasena = SecurityHelper.EncryptPassword("admin123"), // 🔐 CONTRASEÑA ENCRIPTADA
                            id_rol = 1, // Asegúrate de que existe el rol Admin con ID 1
                            primer_ingreso = true,
                            fecha_creacion = DateTime.Now,
                            fecha_actualizacion = DateTime.Now
                        };

                        db.Usuarios.Add(adminUser);
                        db.SaveChanges();

                        System.Diagnostics.Debug.WriteLine("✅ Usuario administrador creado exitosamente");
                        System.Diagnostics.Debug.WriteLine("📋 Usuario: admin | Contraseña: admin123");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ℹ️ Usuario administrador ya existe en la base de datos");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al crear usuario administrador: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                // Si hay un InnerException, también mostrarlo
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// 🧹 Limpiar sesión completamente
        /// </summary>
        private void LimpiarSesionCompletamente()
        {
            try
            {
                if (HttpContext.Current?.Session != null)
                {
                    HttpContext.Current.Session.Clear();
                    HttpContext.Current.Session.Abandon();
                }

                FormsAuthentication.SignOut();
                System.Diagnostics.Debug.WriteLine("Sesión limpiada completamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al limpiar sesión: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔄 Redirigir al login con mensaje
        /// </summary>
        private void RedirectToLogin(string message)
        {
            try
            {
                Server.ClearError();

                if (HttpContext.Current?.Session != null)
                {
                    HttpContext.Current.Session["SessionExpiredMessage"] = message;
                }

                Response.Redirect("~/Usuarios/Login", true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al redirigir al login: {ex.Message}");
            }
        }

        /// <summary>
        /// 🌐 Obtener dirección IP del cliente
        /// </summary>
        private string GetClientIPAddress()
        {
            try
            {
                string ipAddress = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

                if (!string.IsNullOrEmpty(ipAddress))
                {
                    string[] addresses = ipAddress.Split(',');
                    if (addresses.Length != 0)
                    {
                        return addresses[0].Trim();
                    }
                }

                return Request.ServerVariables["REMOTE_ADDR"] ?? "Desconocido";
            }
            catch
            {
                return "Desconocido";
            }
        }

        #endregion
    }
}