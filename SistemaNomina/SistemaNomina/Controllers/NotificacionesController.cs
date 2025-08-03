// Controllers/NotificacionesController.cs
using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using SistemaNomina.Models;

namespace SistemaNomina.Controllers
{
    public class NotificacionesController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Notificaciones
        public ActionResult Index()
        {
            var currentUserId = (int)Session["UserId"];
            var usuario = db.Usuarios.FirstOrDefault(u => u.id_usuario == currentUserId);

            if (usuario == null)
                return RedirectToAction("Login", "Usuarios");

            var notificaciones = db.Notificaciones
                .Where(n => n.id_destinatario == usuario.id_empleado)
                .OrderByDescending(n => n.fecha_creacion)
                .Take(50)
                .ToList();

            return View(notificaciones);
        }

        // GET: Obtener notificaciones no leídas
        [HttpGet]
        public ActionResult GetNotificaciones()
        {
            try
            {
                var currentUserId = (int)Session["UserId"];
                var usuario = db.Usuarios.FirstOrDefault(u => u.id_usuario == currentUserId);

                if (usuario == null)
                    return Json(new { success = false }, JsonRequestBehavior.AllowGet);

                var notificaciones = db.Notificaciones
                    .Where(n => n.id_destinatario == usuario.id_empleado && n.leido == false)
                    .OrderByDescending(n => n.fecha_creacion)
                    .Take(10)
                    .Select(n => new
                    {
                        id = n.id_notificacion,
                        titulo = n.titulo,
                        mensaje = n.mensaje.Length > 100 ? n.mensaje.Substring(0, 100) + "..." : n.mensaje,
                        icono = n.icono,
                        tiempo = n.TiempoTranscurrido
                    })
                    .ToList();

                return Json(new { success = true, notificaciones = notificaciones }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Marcar como leída
        [HttpPost]
        public ActionResult MarcarComoLeida(int id)
        {
            try
            {
                var notificacion = db.Notificaciones.Find(id);
                if (notificacion != null)
                {
                    notificacion.leido = true;
                    notificacion.fecha_leido = DateTime.Now;
                    db.SaveChanges();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}