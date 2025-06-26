using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using SistemaNomina.Models; // Asegúrate de incluir tu namespace

namespace SistemaNomina
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Registro de áreas, filtros, rutas, etc.
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Crear usuario administrador automáticamente si no existe
            using (var db = new smartbuilding_rhEntities())
            {
                try
                {
                    // Verificar si ya existe un usuario con el nombre 'admin'
                    if (!db.Usuarios.Any(u => u.usuario == "admin"))
                    {
                        // Crear el nuevo usuario administrador
                        var adminUser = new Usuarios
                        {
                            id_empleado = 1, // Asegúrate de que exista el empleado con este ID
                            usuario = "admin",
                            contrasena = "admin123", // Contraseña temporal
                            id_rol = 1, // Asegúrate de que exista el rol con este ID
                            primer_ingreso = true,
                            fecha_creacion = DateTime.Now,
                            fecha_actualizacion = DateTime.Now
                        };

                        db.Usuarios.Add(adminUser);
                        db.SaveChanges(); // Guardar el usuario en la base de datos
                    }
                }
                catch (Exception ex)
                {
                    // En caso de error, se puede registrar o mostrar en la consola el detalle del error
                    System.Diagnostics.Debug.WriteLine($"Error al crear el usuario administrador: {ex.Message}");
                }
            }
        }

    }
}
