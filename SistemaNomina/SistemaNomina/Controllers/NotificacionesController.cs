// Controllers/NotificacionesController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using SistemaNomina.Services;
using SistemaNomina.Models;

namespace SistemaNomina.Controllers
{
    [Authorize]
    public class NotificacionesController : Controller
    {
        private readonly NotificacionService _notificacionService; // CAMBIADO: Sin interfaz
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        public NotificacionesController()
        {
            _notificacionService = new NotificacionService(); // CAMBIADO: Sin interfaz
        }

        // Método helper para obtener el ID del empleado desde la sesión
        private int? GetEmpleadoId()
        {
            var userId = (int?)Session["UserId"];
            if (userId!= null)
            {
                // Buscar el empleado asociado al usuario
                var usuario = db.Usuarios.FirstOrDefault(u => u.id_usuario == userId.Value);
                return usuario?.id_empleado;
            }
            return null;
        }

        // GET: /Notificaciones
        public async Task<ActionResult> Index()
        {
            var empleadoId = GetEmpleadoId();
            if (empleadoId == null)
                return RedirectToAction("Login", "Usuarios");

            var notificaciones = await _notificacionService.ObtenerNotificacionesUsuario(empleadoId.Value);

            ViewBag.Title = "Mis Notificaciones";
            return View(notificaciones);
        }

        // GET: /Notificaciones/GetNotificacionesCount
        [HttpGet]
        public async Task<ActionResult> GetNotificacionesCount()
        {
            var empleadoId = GetEmpleadoId();
            if (empleadoId == null)
                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);

            var count = await _notificacionService.ContarNotificacionesNoLeidas(empleadoId.Value);
            return Json(new { count }, JsonRequestBehavior.AllowGet);
        }

        // GET: /Notificaciones/GetNotificacionesRecientes
        [HttpGet]
        public async Task<ActionResult> GetNotificacionesRecientes()
        {
            var empleadoId = GetEmpleadoId();
            if (empleadoId == null)
                return Json(new { notificaciones = new object[0] }, JsonRequestBehavior.AllowGet);

            var notificaciones = await _notificacionService.ObtenerNotificacionesUsuario(empleadoId.Value, true, 5);

            var result = notificaciones.Select(n => new
            {
                id = n.id_notificacion,
                titulo = n.titulo,
                mensaje = n.mensaje.Length > 100 ? n.mensaje.Substring(0, 100) + "..." : n.mensaje,
                fecha = n.TiempoTranscurrido,
                icono = n.icono,
                color = n.color,
                leido = n.leido,
                prioridad = n.prioridad
            }).ToArray();

            return Json(new { notificaciones = result }, JsonRequestBehavior.AllowGet);
        }

        // POST: /Notificaciones/MarcarComoLeida
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> MarcarComoLeida(int id)
        {
            var empleadoId = GetEmpleadoId();
            if (empleadoId == null)
                return Json(new { success = false, message = "Sesión expirada" });

            var resultado = await _notificacionService.MarcarComoLeida(id, empleadoId.Value);
            return Json(new { success = resultado });
        }

        // POST: /Notificaciones/MarcarTodasComoLeidas
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> MarcarTodasComoLeidas()
        {
            var empleadoId = GetEmpleadoId();
            if (empleadoId == null)
                return Json(new { success = false, message = "Sesión expirada" });

            var resultado = await _notificacionService.MarcarTodasComoLeidas(empleadoId.Value);
            return Json(new
            {
                success = resultado,
                message = resultado ? "Todas las notificaciones han sido marcadas como leídas" : "Error al procesar la solicitud"
            });
        }

        // POST: /Notificaciones/Eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Eliminar(int id)
        {
            var empleadoId = GetEmpleadoId();
            if (empleadoId == null)
                return Json(new { success = false, message = "Sesión expirada" });

            var resultado = await _notificacionService.EliminarNotificacion(id, empleadoId.Value);
            return Json(new
            {
                success = resultado,
                message = resultado ? "Notificación eliminada correctamente" : "Error al eliminar la notificación"
            });
        }

        // GET: /Notificaciones/Detalles/{id}
        public async Task<ActionResult> Detalles(int id)
        {
            var empleadoId = GetEmpleadoId();
            if (empleadoId == null)
                return RedirectToAction("Login", "Usuarios");

            var notificaciones = await _notificacionService.ObtenerNotificacionesUsuario(empleadoId.Value);
            var notificacion = notificaciones.FirstOrDefault(n => n.id_notificacion == id);

            if (notificacion == null)
                return HttpNotFound();

            // Marcar como leída automáticamente al ver los detalles
            // Marcar como leída automáticamente al ver los detalles
            if (notificacion.leido.HasValue && !notificacion.leido.Value)
            {
                await _notificacionService.MarcarComoLeida(id, empleadoId.Value);
            }

            ViewBag.Title = "Detalles de Notificación";
            return View(notificacion);
        }

        // GET: /Notificaciones/NoLeidas
        public async Task<ActionResult> NoLeidas()
        {
            var empleadoId = GetEmpleadoId();
            if (empleadoId == null)
                return RedirectToAction("Login", "Usuarios");

            var notificaciones = await _notificacionService.ObtenerNotificacionesUsuario(empleadoId.Value, true);

            ViewBag.Title = "Notificaciones No Leídas";
            return View("Index", notificaciones);
        }

        // Método para testing - Solo Admin
        [HttpPost]
        public async Task<ActionResult> EnviarNotificacionPrueba(int destinatarioId, string titulo, string mensaje)
        {
            var currentUserRole = Session["RolUsuario"] as string;
            if (currentUserRole != "Admin")
                return Json(new { success = false, message = "Sin permisos" });

            var empleadoId = GetEmpleadoId();
            if (empleadoId == null)
                return Json(new { success = false, message = "Sesión expirada" });

            var resultado = await _notificacionService.CrearNotificacion(
                empleadoId.Value,
                destinatarioId,
                titulo ?? "Notificación de Prueba",
                mensaje ?? "Esta es una notificación de prueba del sistema.",
                "informativa",
                null,
                null,
                "normal"
            );

            return Json(new
            {
                success = resultado,
                message = resultado ? "Notificación de prueba enviada" : "Error al enviar notificación"
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
                _notificacionService?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}