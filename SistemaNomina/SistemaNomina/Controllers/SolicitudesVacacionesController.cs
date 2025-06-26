using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;
using SistemaNomina.Models;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH", "Supervisor", "Empleado")]
    public class SolicitudesVacacionesController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: SolicitudesVacaciones
        public ActionResult Index()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                IQueryable<SolicitudesVacaciones> query = db.SolicitudesVacaciones
                    .Include(s => s.Estados)
                    .Include(s => s.Usuarios)
                    .Include(s => s.Usuarios.Empleados)
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados);

                // 🔒 FILTRO POR ROL
                if (currentUserRole == "Empleado")
                {
                    // Los empleados solo ven sus propias solicitudes
                    var empleado = db.Usuarios.Include(u => u.Empleados)
                                             .FirstOrDefault(u => u.id_usuario == currentUserId)?.Empleados;
                    if (empleado != null)
                    {
                        query = query.Where(s => s.Vacaciones.id_empleado == empleado.id_empleado);
                    }
                }

                var solicitudes = query.OrderByDescending(s => s.fecha_solicitud)
                                     .ThenByDescending(s => s.fecha_creacion)
                                     .ToList();

                return View(solicitudes);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar solicitudes de vacaciones.";
                LogExceptionDetails(ex);
                return View(new List<SolicitudesVacaciones>());
            }
        }

        // 🔥 NUEVA: Solicitudes pendientes para supervisores
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult SolicitudesPendientes()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                var estadoPendiente = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Vacaciones");
                if (estadoPendiente == null)
                {
                    TempData["Error"] = "No se encontró el estado 'Pendiente' para vacaciones.";
                    return View(new List<SolicitudesVacaciones>());
                }

                IQueryable<SolicitudesVacaciones> query = db.SolicitudesVacaciones
                    .Include(s => s.Estados)
                    .Include(s => s.Usuarios)
                    .Include(s => s.Usuarios.Empleados)
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .Include(s => s.Vacaciones.Empleados.Puestos)
                    .Include(s => s.Vacaciones.Empleados.Puestos.Departamentos)
                    .Where(s => s.id_estado == estadoPendiente.id_estado);

                // 🔒 FILTRO POR ROL: Supervisores solo ven empleados de su departamento
                if (currentUserRole == "Supervisor")
                {
                    var supervisorEmpleado = db.Usuarios.Include(u => u.Empleados)
                                                       .Include(u => u.Empleados.Puestos)
                                                       .FirstOrDefault(u => u.id_usuario == currentUserId)?.Empleados;

                    if (supervisorEmpleado?.Puestos != null)
                    {
                        query = query.Where(s => s.Vacaciones.Empleados.Puestos.id_departamento == supervisorEmpleado.Puestos.id_departamento);
                    }
                }

                var solicitudesPendientes = query.OrderBy(s => s.fecha_solicitud).ToList();

                return View(solicitudesPendientes);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar solicitudes pendientes.";
                LogExceptionDetails(ex);
                return View(new List<SolicitudesVacaciones>());
            }
        }

        // 🎯 CASO DE USO 3: Aprobar solicitud (con notificación automática)
        [HttpPost]
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult Aprobar(int id, string comentario_respuesta = "")
        {
            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                {
                    return Json(new { success = false, message = "Solicitud no encontrada." });
                }

                var currentUserId = (int?)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                // 🔒 VALIDACIÓN: Supervisores solo pueden aprobar de su departamento
                if (currentUserRole == "Supervisor")
                {
                    var supervisorEmpleado = db.Usuarios.Include(u => u.Empleados)
                                                       .Include(u => u.Empleados.Puestos)
                                                       .FirstOrDefault(u => u.id_usuario == currentUserId)?.Empleados;

                    var empleadoSolicitante = db.Empleados.Include(e => e.Puestos)
                                    .FirstOrDefault(e => e.id_empleado == solicitud.Vacaciones.id_empleado);

                    if (supervisorEmpleado?.Puestos?.id_departamento != empleadoSolicitante?.Puestos?.id_departamento)
                    {
                        return Json(new { success = false, message = "No tiene permisos para aprobar esta solicitud." });
                    }
                }

                // 🔍 VALIDACIÓN: Verificar días disponibles nuevamente
                var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
                var diasDisponibles = solicitud.Vacaciones.dias_disponibles - (solicitud.Vacaciones.dias_disfrutados ?? 0);

                if (diasSolicitados > diasDisponibles)
                {
                    return Json(new { success = false, message = $"El empleado no tiene suficientes días disponibles. Disponibles: {diasDisponibles}, Solicitados: {diasSolicitados}" });
                }

                // Aprobar solicitud
                var estadoAprobado = db.Estados.FirstOrDefault(e => e.nombre == "Aprobado" && e.modulo == "Vacaciones");
                if (estadoAprobado != null)
                {
                    solicitud.id_estado = estadoAprobado.id_estado;
                    solicitud.aprobado_por = currentUserId;
                    solicitud.fecha_aprobacion = DateTime.Now;
                    solicitud.comentario_respuesta = comentario_respuesta?.Trim();
                    solicitud.fecha_actualizacion = DateTime.Now;

                    // 📊 Actualizar días disfrutados
                    solicitud.Vacaciones.dias_disfrutados = (solicitud.Vacaciones.dias_disfrutados ?? 0) + diasSolicitados;
                    solicitud.Vacaciones.fecha_actualizacion = DateTime.Now;

                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("APROBAR_VACACIONES",
                            $"Aprobadas vacaciones de {solicitud.Vacaciones.Empleados.nombre1} {solicitud.Vacaciones.Empleados.apellido1} del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy} ({diasSolicitados} días)",
                            currentUserId.Value);
                    }

                    // 🔔 NOTIFICACIÓN AUTOMÁTICA (caso de uso 3)
                    NotificarDecisionVacaciones(solicitud, "APROBADA");

                    return Json(new { success = true, message = "Solicitud aprobada exitosamente. El empleado ha sido notificado." });
                }

                return Json(new { success = false, message = "No se pudo actualizar el estado." });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error al aprobar solicitud: " + ex.Message });
            }
        }

        // 🎯 CASO DE USO 3: Rechazar solicitud (con notificación automática)
        [HttpPost]
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult Rechazar(int id, string comentario_respuesta = "")
        {
            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                {
                    return Json(new { success = false, message = "Solicitud no encontrada." });
                }

                var currentUserId = (int?)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                // 🔒 VALIDACIÓN: Supervisores solo pueden rechazar de su departamento
                if (currentUserRole == "Supervisor")
                {
                    var supervisorEmpleado = db.Usuarios.Include(u => u.Empleados)
                                                       .Include(u => u.Empleados.Puestos)
                                                       .FirstOrDefault(u => u.id_usuario == currentUserId)?.Empleados;

                    var empleadoSolicitante = db.Empleados.Include(e => e.Puestos)
                                    .FirstOrDefault(e => e.id_empleado == solicitud.Vacaciones.id_empleado);

                    if (supervisorEmpleado?.Puestos?.id_departamento != empleadoSolicitante?.Puestos?.id_departamento)
                    {
                        return Json(new { success = false, message = "No tiene permisos para rechazar esta solicitud." });
                    }
                }

                // Rechazar solicitud
                var estadoRechazado = db.Estados.FirstOrDefault(e => e.nombre == "Rechazado" && e.modulo == "Vacaciones");
                if (estadoRechazado != null)
                {
                    solicitud.id_estado = estadoRechazado.id_estado;
                    solicitud.aprobado_por = currentUserId;
                    solicitud.fecha_aprobacion = DateTime.Now;
                    solicitud.comentario_respuesta = comentario_respuesta?.Trim();
                    solicitud.fecha_actualizacion = DateTime.Now;

                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO
                    if (currentUserId.HasValue)
                    {
                        var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
                        BitacoraHelper.RegistrarAccion("RECHAZAR_VACACIONES",
                            $"Rechazadas vacaciones de {solicitud.Vacaciones.Empleados.nombre1} {solicitud.Vacaciones.Empleados.apellido1} del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy} ({diasSolicitados} días)",
                            currentUserId.Value);
                    }

                    // 🔔 NOTIFICACIÓN AUTOMÁTICA (caso de uso 3)
                    NotificarDecisionVacaciones(solicitud, "RECHAZADA");

                    return Json(new { success = true, message = "Solicitud rechazada. El empleado ha sido notificado." });
                }

                return Json(new { success = false, message = "No se pudo actualizar el estado." });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error al rechazar solicitud: " + ex.Message });
            }
        }

        // GET: SolicitudesVacaciones/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            SolicitudesVacaciones solicitud = db.SolicitudesVacaciones
                .Include(s => s.Estados)
                .Include(s => s.Usuarios)
                .Include(s => s.Usuarios.Empleados)
                .Include(s => s.Vacaciones)
                .Include(s => s.Vacaciones.Empleados)
                .Include(s => s.Vacaciones.Empleados.Puestos)
                .Include(s => s.Vacaciones.Empleados.Puestos.Departamentos)
                .FirstOrDefault(s => s.id_solicitud == id);

            if (solicitud == null)
                return HttpNotFound();

            // 🔒 VALIDACIÓN DE ACCESO
            var currentUserId = (int?)Session["UserId"];
            var currentUserRole = Session["RolUsuario"] as string;

            if (currentUserRole == "Empleado")
            {
                var empleado = db.Usuarios.Include(u => u.Empleados)
                                        .FirstOrDefault(u => u.id_usuario == currentUserId)?.Empleados;

                if (empleado == null || solicitud.Vacaciones.id_empleado != empleado.id_empleado)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }
            }

            // Calcular días solicitados
            ViewBag.DiasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);

            return View(solicitud);
        }

        // GET: SolicitudesVacaciones/Create
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Create()
        {
            CargarListasDesplegables();
            return View();
        }

        // POST: SolicitudesVacaciones/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Create([Bind(Include = "id_vacacion,fecha_inicio,fecha_fin,comentario_solicitud")] SolicitudesVacaciones solicitud)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔍 VALIDACIONES DE NEGOCIO
                    var validationResult = ValidarSolicitud(solicitud);
                    if (!validationResult.esValida)
                    {
                        ModelState.AddModelError("", validationResult.mensaje);
                        CargarListasDesplegables(solicitud);
                        return View(solicitud);
                    }

                    // 📅 Datos automáticos
                    solicitud.fecha_solicitud = DateTime.Now;
                    solicitud.id_estado = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Vacaciones")?.id_estado ?? 1;
                    solicitud.fecha_creacion = DateTime.Now;
                    solicitud.fecha_actualizacion = DateTime.Now;

                    db.SolicitudesVacaciones.Add(solicitud);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("CREAR_SOLICITUD_VACACIONES",
                            $"Creada solicitud de vacaciones (ID: {solicitud.id_solicitud})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = "Solicitud de vacaciones creada exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error inesperado: {ex.Message}");
                LogExceptionDetails(ex);
            }

            CargarListasDesplegables(solicitud);
            return View(solicitud);
        }

        // GET: SolicitudesVacaciones/Edit/5
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            SolicitudesVacaciones solicitud = db.SolicitudesVacaciones.Find(id);
            if (solicitud == null)
                return HttpNotFound();

            // 🔒 Solo permitir editar solicitudes pendientes
            var estadoPendiente = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Vacaciones");
            if (solicitud.id_estado != estadoPendiente?.id_estado)
            {
                TempData["Error"] = "Solo se pueden editar solicitudes en estado Pendiente.";
                return RedirectToAction("Index");
            }

            CargarListasDesplegables(solicitud);
            return View(solicitud);
        }

        // POST: SolicitudesVacaciones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit([Bind(Include = "id_solicitud,id_vacacion,fecha_inicio,fecha_fin,comentario_solicitud,id_estado,fecha_solicitud,fecha_creacion")] SolicitudesVacaciones solicitud)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔍 VALIDACIONES DE NEGOCIO
                    var validationResult = ValidarSolicitud(solicitud);
                    if (!validationResult.esValida)
                    {
                        ModelState.AddModelError("", validationResult.mensaje);
                        CargarListasDesplegables(solicitud);
                        return View(solicitud);
                    }

                    // 📅 Actualizar fecha de modificación
                    solicitud.fecha_actualizacion = DateTime.Now;

                    db.Entry(solicitud).State = EntityState.Modified;
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_SOLICITUD_VACACIONES",
                            $"Editada solicitud de vacaciones (ID: {solicitud.id_solicitud})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = "Solicitud de vacaciones actualizada exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "Otro usuario modificó este registro. Recarga la página.");
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error inesperado: {ex.Message}");
                LogExceptionDetails(ex);
            }

            CargarListasDesplegables(solicitud);
            return View(solicitud);
        }

        // GET: SolicitudesVacaciones/Delete/5
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            SolicitudesVacaciones solicitud = db.SolicitudesVacaciones
                .Include(s => s.Estados)
                .Include(s => s.Usuarios)
                .Include(s => s.Vacaciones)
                .Include(s => s.Vacaciones.Empleados)
                .FirstOrDefault(s => s.id_solicitud == id);

            if (solicitud == null)
                return HttpNotFound();

            // 🔍 Verificar si ya fue aprobada
            var estadoAprobado = db.Estados.FirstOrDefault(e => e.nombre == "Aprobado" && e.modulo == "Vacaciones");
            ViewBag.EsAprobada = solicitud.id_estado == estadoAprobado?.id_estado;

            return View(solicitud);
        }

        // POST: SolicitudesVacaciones/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                SolicitudesVacaciones solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                    return HttpNotFound();

                // 🛡️ VALIDACIÓN: No eliminar si ya fue aprobada y afectó días disfrutados
                var estadoAprobado = db.Estados.FirstOrDefault(e => e.nombre == "Aprobado" && e.modulo == "Vacaciones");
                if (solicitud.id_estado == estadoAprobado?.id_estado)
                {
                    TempData["Error"] = "No se pueden eliminar solicitudes ya aprobadas que afectaron los días de vacaciones.";
                    return RedirectToAction("Index");
                }

                // 📋 LOG AUTOMÁTICO
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("ELIMINAR_SOLICITUD_VACACIONES",
                        $"Eliminada solicitud de vacaciones (ID: {solicitud.id_solicitud})",
                        currentUserId.Value);
                }

                db.SolicitudesVacaciones.Remove(solicitud);
                db.SaveChanges();

                TempData["Success"] = "Solicitud de vacaciones eliminada exitosamente.";
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
                TempData["Error"] = "Error al eliminar la solicitud.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error inesperado al eliminar: {ex.Message}";
                LogExceptionDetails(ex);
            }

            return RedirectToAction("Index");
        }

        #region Métodos auxiliares

        // 🔔 MÉTODO CLAVE: Notificación automática (caso de uso 3)
        private void NotificarDecisionVacaciones(SolicitudesVacaciones solicitud, string decision)
        {
            try
            {
                // En una implementación completa, aquí se enviaría:
                // 1. Email al empleado
                // 2. Notificación en el sistema
                // 3. SMS (opcional)

                var empleado = solicitud.Vacaciones.Empleados;
                var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);

                // Simular notificación - En producción sería un EmailService
                var mensaje = $"Su solicitud de {diasSolicitados} días de vacaciones del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy} ha sido {decision}.";

                if (!string.IsNullOrEmpty(solicitud.comentario_respuesta))
                {
                    mensaje += $" Comentario: {solicitud.comentario_respuesta}";
                }

                // 📋 Registrar la notificación en bitácora
                BitacoraHelper.RegistrarAccion("NOTIFICAR_DECISION_VACACIONES",
                    $"Notificación enviada a {empleado.nombre1} {empleado.apellido1}: {mensaje}",
                    solicitud.aprobado_por);

                System.Diagnostics.Debug.WriteLine($"NOTIFICACIÓN ENVIADA: {mensaje}");
            }
            catch (Exception ex)
            {
                // No fallar si la notificación falla
                LogExceptionDetails(ex);
            }
        }

        private int CalcularDiasLaborales(DateTime fechaInicio, DateTime fechaFin)
        {
            int dias = 0;
            for (var fecha = fechaInicio; fecha <= fechaFin; fecha = fecha.AddDays(1))
            {
                if (fecha.DayOfWeek != DayOfWeek.Saturday && fecha.DayOfWeek != DayOfWeek.Sunday)
                    dias++;
            }
            return dias;
        }

        private (bool esValida, string mensaje) ValidarSolicitud(SolicitudesVacaciones solicitud)
        {
            // Validar fechas
            if (solicitud.fecha_inicio < DateTime.Today)
                return (false, "La fecha de inicio no puede ser anterior a hoy.");

            if (solicitud.fecha_fin <= solicitud.fecha_inicio)
                return (false, "La fecha de fin debe ser posterior a la fecha de inicio.");

            // Calcular días solicitados
            var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
            if (diasSolicitados == 0)
                return (false, "Debe solicitar al menos un día laboral.");

            // Verificar días disponibles
            var vacaciones = db.Vacaciones.Find(solicitud.id_vacacion);
            if (vacaciones != null)
            {
                var diasDisponibles = vacaciones.dias_disponibles - (vacaciones.dias_disfrutados ?? 0);
                if (diasSolicitados > diasDisponibles)
                    return (false, $"Solo hay {diasDisponibles} día(s) disponible(s). Se están solicitando {diasSolicitados} día(s).");
            }

            return (true, "Válida");
        }

        private void CargarListasDesplegables(SolicitudesVacaciones solicitud = null)
        {
            ViewBag.id_estado = new SelectList(
                db.Estados.Where(e => e.modulo == "Vacaciones"),
                "id_estado", "nombre", solicitud?.id_estado);

            ViewBag.aprobado_por = new SelectList(
                db.Usuarios.Include(u => u.Empleados)
                    .Where(u => u.Empleados != null)
                    .Select(u => new {
                        u.id_usuario,
                        NombreCompleto = u.Empleados.nombre1 + " " + u.Empleados.apellido1
                    })
                    .OrderBy(x => x.NombreCompleto),
                "id_usuario", "NombreCompleto", solicitud?.aprobado_por);

            ViewBag.id_vacacion = new SelectList(
                db.Vacaciones.Include(v => v.Empleados)
                    .Select(v => new {
                        v.id_vacacion,
                        Descripcion = v.Empleados.nombre1 + " " + v.Empleados.apellido1 + " - " + v.periodo
                    })
                    .OrderBy(x => x.Descripcion),
                "id_vacacion", "Descripcion", solicitud?.id_vacacion);
        }

        private void HandleDbUpdateException(DbUpdateException dbEx)
        {
            var innerException = dbEx.InnerException?.InnerException ?? dbEx.InnerException;

            if (innerException != null)
            {
                if (innerException.Message.Contains("FK_"))
                {
                    ModelState.AddModelError("", "No se puede realizar esta acción porque hay registros relacionados.");
                }
                else if (innerException.Message.Contains("IX_") || innerException.Message.Contains("UNIQUE"))
                {
                    ModelState.AddModelError("", "Ya existe un registro con estos valores.");
                }
                else
                {
                    ModelState.AddModelError("", $"Error de base de datos: {innerException.Message}");
                }
            }
            else
            {
                ModelState.AddModelError("", $"Error de base de datos: {dbEx.Message}");
            }
        }

        private void LogExceptionDetails(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en SolicitudesVacacionesController: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}