using System;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using SistemaNomina.Models;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH", "Supervisor", "Empleado")]
    public class AsistenciaController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Vista principal de asistencia
        public ActionResult Index()
        {
            var asistencia = db.Asistencia.Include(a => a.Empleados)
                                        .Include(a => a.Feriados)
                                        .OrderByDescending(a => a.fecha)
                                        .ThenByDescending(a => a.fecha_registro);
            return View(asistencia.ToList());
        }

        // 🔥 NUEVA: Vista para marcar entrada/salida
        public ActionResult Marcar()
        {
            var currentUserId = (int?)Session["UserId"];
            if (!currentUserId.HasValue)
            {
                return RedirectToAction("Login", "Usuarios");
            }

            // Buscar empleado del usuario actual
            var empleado = db.Usuarios.Include(u => u.Empleados)
                                    .Include(u => u.Empleados.Puestos)
                                    .Include(u => u.Empleados.Puestos.Departamentos)
                                    .FirstOrDefault(u => u.id_usuario == currentUserId.Value)?.Empleados;

            if (empleado == null)
            {
                TempData["Error"] = "No se encontró información del empleado asociado.";
                return RedirectToAction("Index", "Home");
            }

            // Verificar asistencia del día actual
            var hoy = DateTime.Today;
            var asistenciaHoy = db.Asistencia.FirstOrDefault(a => a.id_empleado == empleado.id_empleado
                                                              && DbFunctions.TruncateTime(a.fecha) == hoy);

            // Verificar si es feriado
            var feriado = db.Feriados.FirstOrDefault(f => DbFunctions.TruncateTime(f.fecha) == hoy);

            ViewBag.Empleado = empleado;
            ViewBag.AsistenciaHoy = asistenciaHoy;
            ViewBag.EsFeriado = feriado != null;
            ViewBag.Feriado = feriado;

            return View();
        }

        // 🔥 NUEVA: Marcar entrada
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarEntrada()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (!currentUserId.HasValue)
                {
                    return Json(new { success = false, message = "Sesión expirada" });
                }

                var empleado = db.Usuarios.Include(u => u.Empleados)
                                        .FirstOrDefault(u => u.id_usuario == currentUserId.Value)?.Empleados;

                if (empleado == null)
                {
                    return Json(new { success = false, message = "Empleado no encontrado" });
                }

                var hoy = DateTime.Today;
                var asistenciaExistente = db.Asistencia.FirstOrDefault(a => a.id_empleado == empleado.id_empleado
                                                                         && DbFunctions.TruncateTime(a.fecha) == hoy);

                if (asistenciaExistente != null)
                {
                    return Json(new { success = false, message = "Ya marcó entrada hoy" });
                }

                var feriado = db.Feriados.FirstOrDefault(f => DbFunctions.TruncateTime(f.fecha) == hoy);

                var nuevaAsistencia = new Asistencia
                {
                    id_empleado = empleado.id_empleado,
                    fecha = DateTime.Now.Date,
                    hora_entrada = DateTime.Now.TimeOfDay,
                    es_feriado = feriado != null,
                    id_feriado = feriado?.id_feriado,
                    fecha_registro = DateTime.Now
                };

                db.Asistencia.Add(nuevaAsistencia);
                db.SaveChanges();

                // 📋 LOG AUTOMÁTICO
                if (System.IO.File.Exists(Server.MapPath("~/Helpers/BitacoraHelper.cs")))
                {
                    BitacoraHelper.RegistrarAccion("MARCAR_ENTRADA",
                        $"Empleado {empleado.nombre1} {empleado.apellido1} marcó entrada",
                        currentUserId.Value);
                }

                return Json(new
                {
                    success = true,
                    message = "Entrada marcada exitosamente",
                    hora = DateTime.Now.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al marcar entrada: " + ex.Message });
            }
        }

        // 🔥 NUEVA: Marcar salida
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarSalida()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (!currentUserId.HasValue)
                {
                    return Json(new { success = false, message = "Sesión expirada" });
                }

                var empleado = db.Usuarios.Include(u => u.Empleados)
                                        .FirstOrDefault(u => u.id_usuario == currentUserId.Value)?.Empleados;

                if (empleado == null)
                {
                    return Json(new { success = false, message = "Empleado no encontrado" });
                }

                var hoy = DateTime.Today;
                var asistenciaHoy = db.Asistencia.FirstOrDefault(a => a.id_empleado == empleado.id_empleado
                                                               && DbFunctions.TruncateTime(a.fecha) == hoy);

                if (asistenciaHoy == null)
                {
                    return Json(new { success = false, message = "Debe marcar entrada primero" });
                }

                if (asistenciaHoy.hora_salida.HasValue)
                {
                    return Json(new { success = false, message = "Ya marcó salida hoy" });
                }

                asistenciaHoy.hora_salida = DateTime.Now.TimeOfDay;
                db.Entry(asistenciaHoy).State = EntityState.Modified;
                db.SaveChanges();

                // 📋 LOG AUTOMÁTICO
                if (System.IO.File.Exists(Server.MapPath("~/Helpers/BitacoraHelper.cs")))
                {
                    BitacoraHelper.RegistrarAccion("MARCAR_SALIDA",
                        $"Empleado {empleado.nombre1} {empleado.apellido1} marcó salida",
                        currentUserId.Value);
                }

                return Json(new
                {
                    success = true,
                    message = "Salida marcada exitosamente",
                    hora = DateTime.Now.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al marcar salida: " + ex.Message });
            }
        }

        // 🔥 NUEVA: Reportes de asistencia
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult Reportes()
        {
            ViewBag.Empleados = new SelectList(db.Empleados.Where(e => e.estado == "ACTIVO"),
                                             "id_empleado", "cedula");
            ViewBag.Departamentos = new SelectList(db.Departamentos, "id_departamento", "nombre");
            return View();
        }

        // 🔥 NUEVA: Generar reporte filtrado
        [HttpPost]
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult GenerarReporte(int? empleadoId, int? departamentoId,
                                         DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                var query = db.Asistencia.Include(a => a.Empleados)
                                       .Include(a => a.Empleados.Puestos)
                                       .Include(a => a.Empleados.Puestos.Departamentos)
                                       .Include(a => a.Feriados)
                                       .AsQueryable();

                // Filtros
                if (empleadoId.HasValue)
                {
                    query = query.Where(a => a.id_empleado == empleadoId.Value);
                }

                if (departamentoId.HasValue)
                {
                    query = query.Where(a => a.Empleados.Puestos.id_departamento == departamentoId.Value);
                }

                if (fechaInicio.HasValue)
                {
                    query = query.Where(a => a.fecha >= fechaInicio.Value);
                }

                if (fechaFin.HasValue)
                {
                    query = query.Where(a => a.fecha <= fechaFin.Value);
                }

                var resultado = query.OrderBy(a => a.fecha)
                                   .ThenBy(a => a.Empleados.apellido1)
                                   .ToList();

                return PartialView("_ReporteResultado", resultado);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al generar reporte: " + ex.Message });
            }
        }

        // Métodos CRUD existentes (mantener)
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Asistencia asistencia = db.Asistencia.Find(id);
            if (asistencia == null)
                return HttpNotFound();

            return View(asistencia);
        }

        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Create()
        {
            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "ACTIVO"),
                                               "id_empleado", "cedula");
            ViewBag.id_feriado = new SelectList(db.Feriados, "id_feriado", "nombre");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Create([Bind(Include = "id_empleado,fecha,hora_entrada,hora_salida,es_feriado,id_feriado")] Asistencia asistencia)
        {
            if (ModelState.IsValid)
            {
                asistencia.fecha_registro = DateTime.Now;
                db.Asistencia.Add(asistencia);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "ACTIVO"),
                                               "id_empleado", "cedula", asistencia.id_empleado);
            ViewBag.id_feriado = new SelectList(db.Feriados, "id_feriado", "nombre", asistencia.id_feriado);
            return View(asistencia);
        }

        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Asistencia asistencia = db.Asistencia.Find(id);
            if (asistencia == null)
                return HttpNotFound();

            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "ACTIVO"),
                                               "id_empleado", "cedula", asistencia.id_empleado);
            ViewBag.id_feriado = new SelectList(db.Feriados, "id_feriado", "nombre", asistencia.id_feriado);
            return View(asistencia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit([Bind(Include = "id_asistencia,id_empleado,fecha,hora_entrada,hora_salida,es_feriado,fecha_registro,id_feriado")] Asistencia asistencia)
        {
            if (ModelState.IsValid)
            {
                db.Entry(asistencia).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "ACTIVO"),
                                               "id_empleado", "cedula", asistencia.id_empleado);
            ViewBag.id_feriado = new SelectList(db.Feriados, "id_feriado", "nombre", asistencia.id_feriado);
            return View(asistencia);
        }

        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Asistencia asistencia = db.Asistencia.Find(id);
            if (asistencia == null)
                return HttpNotFound();

            return View(asistencia);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult DeleteConfirmed(int id)
        {
            Asistencia asistencia = db.Asistencia.Find(id);
            db.Asistencia.Remove(asistencia);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}