using System;
using System.Web.Mvc;
using System.Web.Security;
using SistemaNomina.Models;
using System.Linq;
using System.Data.Entity;

namespace SistemaNomina.Controllers
{
    public class HomeController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        public ActionResult Index()
        {
            try
            {
                // Variables por defecto
                ViewBag.Empleado = null;
                ViewBag.AsistenciaHoy = null;
                ViewBag.EsFeriado = false;
                ViewBag.Feriado = null;

                // Solo si hay sesión válida, buscar datos
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    var empleado = db.Empleados
                        .Include(e => e.Puestos)
                        .Include(e => e.Puestos.Departamentos)
                        .FirstOrDefault(e => e.Usuarios.Any(u => u.id_usuario == currentUserId.Value));

                    if (empleado != null)
                    {
                        ViewBag.Empleado = empleado;

                        // Buscar asistencia de hoy
                        var hoy = DateTime.Today;
                        var asistenciaHoy = db.Asistencia.FirstOrDefault(a =>
                            a.id_empleado == empleado.id_empleado &&
                            DbFunctions.TruncateTime(a.fecha) == hoy);

                        ViewBag.AsistenciaHoy = asistenciaHoy;

                        // Buscar feriado
                        var feriado = db.Feriados.FirstOrDefault(f =>
                            DbFunctions.TruncateTime(f.fecha) == hoy);

                        ViewBag.EsFeriado = feriado != null;
                        ViewBag.Feriado = feriado;
                    }
                }

                return View();
            }
            catch
            {
                // En caso de cualquier error, mostrar página vacía
                ViewBag.Empleado = null;
                ViewBag.AsistenciaHoy = null;
                ViewBag.EsFeriado = false;
                ViewBag.Feriado = null;
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarcarEntrada()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (!currentUserId.HasValue)
                {
                    return Json(new { success = false, message = "No hay sesión activa" });
                }

                var empleado = db.Empleados
                    .FirstOrDefault(e => e.Usuarios.Any(u => u.id_usuario == currentUserId.Value));

                if (empleado == null)
                {
                    return Json(new { success = false, message = "Empleado no encontrado" });
                }

                var hoy = DateTime.Today;
                var yaMarco = db.Asistencia.Any(a =>
                    a.id_empleado == empleado.id_empleado &&
                    DbFunctions.TruncateTime(a.fecha) == hoy);

                if (yaMarco)
                {
                    return Json(new { success = false, message = "Ya marcó entrada hoy" });
                }

                var nuevaAsistencia = new Asistencia
                {
                    id_empleado = empleado.id_empleado,
                    fecha = hoy,
                    hora_entrada = DateTime.Now.TimeOfDay,
                    fecha_registro = DateTime.Now,
                    es_feriado = false
                };

                db.Asistencia.Add(nuevaAsistencia);
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Entrada marcada",
                    hora = DateTime.Now.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarcarSalida()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (!currentUserId.HasValue)
                {
                    return Json(new { success = false, message = "No hay sesión activa" });
                }

                var empleado = db.Empleados
                    .FirstOrDefault(e => e.Usuarios.Any(u => u.id_usuario == currentUserId.Value));

                if (empleado == null)
                {
                    return Json(new { success = false, message = "Empleado no encontrado" });
                }

                var hoy = DateTime.Today;
                var asistencia = db.Asistencia.FirstOrDefault(a =>
                    a.id_empleado == empleado.id_empleado &&
                    DbFunctions.TruncateTime(a.fecha) == hoy);

                if (asistencia == null)
                {
                    return Json(new { success = false, message = "Debe marcar entrada primero" });
                }

                if (asistencia.hora_salida.HasValue)
                {
                    return Json(new { success = false, message = "Ya marcó salida hoy" });
                }

                asistencia.hora_salida = DateTime.Now.TimeOfDay;
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Salida marcada",
                    hora = DateTime.Now.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult About()
        {
            ViewBag.Message = "Descripción de la aplicación.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Página de contacto.";
            return View();
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "Usuarios");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}