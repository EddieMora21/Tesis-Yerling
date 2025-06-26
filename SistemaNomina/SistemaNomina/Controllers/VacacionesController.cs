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
    public class VacacionesController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Vacaciones
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult Index()
        {
            try
            {
                var vacaciones = db.Vacaciones
                    .Include(v => v.Empleados)
                    .Include(v => v.Empleados.Puestos)
                    .Include(v => v.Empleados.Puestos.Departamentos)
                    .OrderByDescending(v => v.fecha_actualizacion)
                    .ToList();

                return View(vacaciones);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar registros de vacaciones.";
                LogExceptionDetails(ex);
                return View(new List<Vacaciones>());
            }
        }

        // 🔥 NUEVA: Vista para empleados - Ver sus propias vacaciones
        public ActionResult MisVacaciones()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (!currentUserId.HasValue)
                {
                    return RedirectToAction("Login", "Usuarios");
                }

                // Buscar empleado del usuario actual
                var empleado = db.Usuarios.Include(u => u.Empleados)
                                        .FirstOrDefault(u => u.id_usuario == currentUserId.Value)?.Empleados;

                if (empleado == null)
                {
                    TempData["Error"] = "No se encontró información del empleado asociado.";
                    return RedirectToAction("Index", "Home");
                }

                // Calcular días automáticamente si no existe registro
                var vacacionesActuales = ObtenerOCrearVacaciones(empleado.id_empleado);

                ViewBag.NombreEmpleado = $"{empleado.nombre1} {empleado.apellido1}";
                ViewBag.FechaIngreso = empleado.fecha_ingreso.ToShortDateString();

                return View(vacacionesActuales);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar información de vacaciones.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index", "Home");
            }
        }

        // 🔥 NUEVA: Solicitar vacaciones (caso de uso 1)
        public ActionResult SolicitarVacaciones()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (!currentUserId.HasValue)
                {
                    return RedirectToAction("Login", "Usuarios");
                }

                var empleado = db.Usuarios.Include(u => u.Empleados)
                                        .FirstOrDefault(u => u.id_usuario == currentUserId.Value)?.Empleados;

                if (empleado == null)
                {
                    TempData["Error"] = "No se encontró información del empleado asociado.";
                    return RedirectToAction("Index", "Home");
                }

                // Obtener o crear registro de vacaciones
                var vacaciones = ObtenerOCrearVacaciones(empleado.id_empleado);

                ViewBag.DiasDisponibles = vacaciones.dias_disponibles - (vacaciones.dias_disfrutados ?? 0);
                ViewBag.NombreEmpleado = $"{empleado.nombre1} {empleado.apellido1}";
                ViewBag.IdVacacion = vacaciones.id_vacacion;

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar formulario de solicitud.";
                LogExceptionDetails(ex);
                return RedirectToAction("MisVacaciones");
            }
        }

        // 🔥 NUEVA: Procesar solicitud de vacaciones
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SolicitarVacaciones(DateTime fecha_inicio, DateTime fecha_fin, string comentario_solicitud)
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

                // 🔍 VALIDACIONES DE NEGOCIO
                var validationResult = ValidarSolicitudVacaciones(empleado.id_empleado, fecha_inicio, fecha_fin);
                if (!validationResult.esValida)
                {
                    return Json(new { success = false, message = validationResult.mensaje });
                }

                // Obtener registro de vacaciones
                var vacaciones = ObtenerOCrearVacaciones(empleado.id_empleado);

                // Crear solicitud
                var solicitud = new SolicitudesVacaciones
                {
                    id_vacacion = vacaciones.id_vacacion,
                    fecha_inicio = fecha_inicio,
                    fecha_fin = fecha_fin,
                    fecha_solicitud = DateTime.Now,
                    comentario_solicitud = comentario_solicitud?.Trim(),
                    id_estado = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Vacaciones")?.id_estado ?? 1,
                    fecha_creacion = DateTime.Now,
                    fecha_actualizacion = DateTime.Now
                };

                db.SolicitudesVacaciones.Add(solicitud);
                db.SaveChanges();

                // 📋 LOG AUTOMÁTICO
                BitacoraHelper.RegistrarAccion("SOLICITAR_VACACIONES",
                    $"Empleado {empleado.nombre1} {empleado.apellido1} solicitó {validationResult.diasSolicitados} días de vacaciones del {fecha_inicio:dd/MM/yyyy} al {fecha_fin:dd/MM/yyyy}",
                    currentUserId.Value);

                return Json(new
                {
                    success = true,
                    message = $"Solicitud de {validationResult.diasSolicitados} días enviada exitosamente. Será revisada por su supervisor."
                });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error al procesar solicitud: " + ex.Message });
            }
        }

        // GET: Vacaciones/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Vacaciones vacaciones = db.Vacaciones
                .Include(v => v.Empleados)
                .Include(v => v.Empleados.Puestos)
                .Include(v => v.Empleados.Puestos.Departamentos)
                .FirstOrDefault(v => v.id_vacacion == id);

            if (vacaciones == null)
                return HttpNotFound();

            return View(vacaciones);
        }

        // GET: Vacaciones/Create
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Create()
        {
            CargarListasDesplegables();
            var model = new Vacaciones
            {
                fecha_creacion = DateTime.Now,
                fecha_actualizacion = DateTime.Now,
                periodo = DateTime.Now.Year.ToString()
            };
            return View(model);
        }

        // POST: Vacaciones/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Create([Bind(Include = "id_empleado,periodo,dias_disponibles,dias_disfrutados")] Vacaciones vacaciones)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔍 VALIDACIÓN DE NEGOCIO - No duplicar periodo por empleado
                    var existeRegistro = db.Vacaciones.Any(v => v.id_empleado == vacaciones.id_empleado
                                                             && v.periodo == vacaciones.periodo);
                    if (existeRegistro)
                    {
                        ModelState.AddModelError("periodo", $"Ya existe un registro de vacaciones para el empleado en el período {vacaciones.periodo}.");
                        CargarListasDesplegables(vacaciones);
                        return View(vacaciones);
                    }

                    // 📅 Fechas automáticas
                    vacaciones.fecha_creacion = DateTime.Now;
                    vacaciones.fecha_actualizacion = DateTime.Now;
                    vacaciones.dias_disfrutados = vacaciones.dias_disfrutados ?? 0;

                    db.Vacaciones.Add(vacaciones);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        var empleado = db.Empleados.Find(vacaciones.id_empleado);
                        BitacoraHelper.RegistrarAccion("CREAR_VACACIONES",
                            $"Creado registro de vacaciones para {empleado?.nombre1} {empleado?.apellido1} - Período: {vacaciones.periodo} - Días: {vacaciones.dias_disponibles}",
                            currentUserId.Value);
                    }

                    TempData["Success"] = "Registro de vacaciones creado exitosamente.";
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

            CargarListasDesplegables(vacaciones);
            return View(vacaciones);
        }

        // GET: Vacaciones/Edit/5
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Vacaciones vacaciones = db.Vacaciones.Find(id);
            if (vacaciones == null)
                return HttpNotFound();

            CargarListasDesplegables(vacaciones);
            return View(vacaciones);
        }

        // POST: Vacaciones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit([Bind(Include = "id_vacacion,id_empleado,periodo,dias_disponibles,dias_disfrutados,fecha_creacion")] Vacaciones vacaciones)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔍 VALIDACIÓN DE NEGOCIO - No duplicar periodo por empleado (excluyendo el actual)
                    var existeRegistro = db.Vacaciones.Any(v => v.id_empleado == vacaciones.id_empleado
                                                             && v.periodo == vacaciones.periodo
                                                             && v.id_vacacion != vacaciones.id_vacacion);
                    if (existeRegistro)
                    {
                        ModelState.AddModelError("periodo", $"Ya existe otro registro de vacaciones para el empleado en el período {vacaciones.periodo}.");
                        CargarListasDesplegables(vacaciones);
                        return View(vacaciones);
                    }

                    // 📅 Actualizar fecha de modificación
                    vacaciones.fecha_actualizacion = DateTime.Now;
                    vacaciones.dias_disfrutados = vacaciones.dias_disfrutados ?? 0;

                    db.Entry(vacaciones).State = EntityState.Modified;
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_VACACIONES",
                            $"Editado registro de vacaciones (ID: {vacaciones.id_vacacion})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = "Registro de vacaciones actualizado exitosamente.";
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

            CargarListasDesplegables(vacaciones);
            return View(vacaciones);
        }

        // GET: Vacaciones/Delete/5
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Vacaciones vacaciones = db.Vacaciones
                .Include(v => v.Empleados)
                .FirstOrDefault(v => v.id_vacacion == id);

            if (vacaciones == null)
                return HttpNotFound();

            // 🔍 VALIDACIÓN DE NEGOCIO - Verificar si hay solicitudes asociadas
            var solicitudesAsociadas = db.SolicitudesVacaciones.Count(s => s.id_vacacion == id);
            ViewBag.SolicitudesAsociadas = solicitudesAsociadas;

            return View(vacaciones);
        }

        // POST: Vacaciones/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                Vacaciones vacaciones = db.Vacaciones.Find(id);
                if (vacaciones == null)
                    return HttpNotFound();

                // 🛡️ VALIDACIÓN DE NEGOCIO - No eliminar si hay solicitudes asociadas
                var solicitudesAsociadas = db.SolicitudesVacaciones.Count(s => s.id_vacacion == id);
                if (solicitudesAsociadas > 0)
                {
                    TempData["Error"] = $"No se puede eliminar el registro porque tiene {solicitudesAsociadas} solicitud(es) de vacaciones asociada(s).";
                    return RedirectToAction("Index");
                }

                // 📋 LOG AUTOMÁTICO
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("ELIMINAR_VACACIONES",
                        $"Eliminado registro de vacaciones (ID: {vacaciones.id_vacacion})",
                        currentUserId.Value);
                }

                db.Vacaciones.Remove(vacaciones);
                db.SaveChanges();

                TempData["Success"] = "Registro de vacaciones eliminado exitosamente.";
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
                TempData["Error"] = "Error al eliminar el registro. Verifique que no esté siendo utilizado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error inesperado al eliminar: {ex.Message}";
                LogExceptionDetails(ex);
            }

            return RedirectToAction("Index");
        }

        #region Métodos auxiliares

        // 🎯 MÉTODO CLAVE: Obtener o crear vacaciones automáticamente (caso de uso 2)
        private Vacaciones ObtenerOCrearVacaciones(int idEmpleado)
        {
            var periodoActual = DateTime.Now.Year.ToString();
            var vacacionesExistentes = db.Vacaciones.FirstOrDefault(v => v.id_empleado == idEmpleado && v.periodo == periodoActual);

            if (vacacionesExistentes != null)
            {
                return vacacionesExistentes;
            }

            // Crear nuevo registro calculando días automáticamente
            var empleado = db.Empleados.Find(idEmpleado);
            if (empleado == null) return null;

            var diasCalculados = CalcularDiasVacaciones(empleado.fecha_ingreso);

            var nuevasVacaciones = new Vacaciones
            {
                id_empleado = idEmpleado,
                periodo = periodoActual,
                dias_disponibles = diasCalculados,
                dias_disfrutados = 0,
                fecha_creacion = DateTime.Now,
                fecha_actualizacion = DateTime.Now
            };

            db.Vacaciones.Add(nuevasVacaciones);
            db.SaveChanges();

            return nuevasVacaciones;
        }

        // 📊 MÉTODO: Calcular días de vacaciones según antigüedad (Código de Trabajo CR)
        private int CalcularDiasVacaciones(DateTime fechaIngreso)
        {
            var semanasTranscurridas = (DateTime.Now - fechaIngreso).TotalDays / 7;

            // Según Código de Trabajo: 2 semanas (12 días) por cada 50 semanas trabajadas
            var ciclosCompletos = (int)(semanasTranscurridas / 50);
            var diasAcumulados = ciclosCompletos * 12;

            // Mínimo 0, máximo razonable 24 días por año
            return Math.Max(0, Math.Min(24, diasAcumulados));
        }

        // 🔍 MÉTODO: Validar solicitud de vacaciones
        private (bool esValida, string mensaje, int diasSolicitados) ValidarSolicitudVacaciones(int idEmpleado, DateTime fechaInicio, DateTime fechaFin)
        {
            // Validar fechas
            if (fechaInicio < DateTime.Today)
                return (false, "La fecha de inicio no puede ser anterior a hoy.", 0);

            if (fechaFin <= fechaInicio)
                return (false, "La fecha de fin debe ser posterior a la fecha de inicio.", 0);

            // Calcular días solicitados (solo días laborales)
            var diasSolicitados = 0;
            for (var fecha = fechaInicio; fecha <= fechaFin; fecha = fecha.AddDays(1))
            {
                if (fecha.DayOfWeek != DayOfWeek.Saturday && fecha.DayOfWeek != DayOfWeek.Sunday)
                    diasSolicitados++;
            }

            if (diasSolicitados == 0)
                return (false, "Debe solicitar al menos un día laboral.", 0);

            // Verificar días disponibles
            var vacaciones = ObtenerOCrearVacaciones(idEmpleado);
            var diasDisponibles = vacaciones.dias_disponibles - (vacaciones.dias_disfrutados ?? 0);

            if (diasSolicitados > diasDisponibles)
                return (false, $"Solo tiene {diasDisponibles} día(s) disponible(s). Está solicitando {diasSolicitados} día(s).", diasSolicitados);

            // Verificar solapamientos con solicitudes existentes
            var solapamiento = db.SolicitudesVacaciones
                .Where(s => s.id_vacacion == vacaciones.id_vacacion)
                .Where(s => s.id_estado != db.Estados.FirstOrDefault(e => e.nombre == "Rechazado").id_estado)
                .Any(s => (fechaInicio <= s.fecha_fin && fechaFin >= s.fecha_inicio));

            if (solapamiento)
                return (false, "Ya tiene una solicitud pendiente o aprobada que se solapa con estas fechas.", diasSolicitados);

            return (true, "Válida", diasSolicitados);
        }

        private void CargarListasDesplegables(Vacaciones vacaciones = null)
        {
            ViewBag.id_empleado = new SelectList(
                db.Empleados.Where(e => e.estado == "ACTIVO")
                    .Select(e => new {
                        e.id_empleado,
                        NombreCompleto = e.nombre1 + " " + e.apellido1 + " - " + e.cedula
                    })
                    .OrderBy(e => e.NombreCompleto),
                "id_empleado", "NombreCompleto", vacaciones?.id_empleado);
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
            System.Diagnostics.Debug.WriteLine($"Error en VacacionesController: {ex.Message}");
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