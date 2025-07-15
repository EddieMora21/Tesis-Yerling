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
                    .Include(s => s.Vacaciones.Empleados)
                    .Include(s => s.Vacaciones.Empleados.Puestos)
                    .Include(s => s.Vacaciones.Empleados.Puestos.Departamentos);

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

        // 🔥 MEJORADA: Solicitudes pendientes con jerarquía
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

                // 🔒 FILTRO JERÁRQUICO MEJORADO
                var solicitudesFiltradas = new List<SolicitudesVacaciones>();

                foreach (var solicitud in query.ToList())
                {
                    if (PuedeAprobarSolicitud(solicitud.id_solicitud, currentUserId.Value))
                    {
                        solicitudesFiltradas.Add(solicitud);
                    }
                }

                return View(solicitudesFiltradas.OrderBy(s => s.fecha_solicitud).ToList());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar solicitudes pendientes.";
                LogExceptionDetails(ex);
                return View(new List<SolicitudesVacaciones>());
            }
        }

        // 🎯 CASO DE USO 3: Aprobar solicitud (con validación jerárquica)
        [HttpPost]
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult Aprobar(int id, string comentario_respuesta = "")
        {
            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .Include(s => s.Vacaciones.Empleados.Puestos)
                    .Include(s => s.Vacaciones.Empleados.Puestos.Departamentos)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                {
                    return Json(new { success = false, message = "Solicitud no encontrada." });
                }

                var currentUserId = (int?)Session["UserId"];

                // 🔒 VALIDACIÓN JERÁRQUICA
                if (!PuedeAprobarSolicitud(id, currentUserId.Value))
                {
                    return Json(new { success = false, message = "No tiene permisos para aprobar esta solicitud según la jerarquía organizacional." });
                }

                // 🔍 VALIDACIÓN: Verificar días disponibles nuevamente
                var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
                var diasDisponibles = solicitud.Vacaciones.dias_disponibles - solicitud.Vacaciones.dias_disfrutados;

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
                    solicitud.Vacaciones.dias_disfrutados = solicitud.Vacaciones.dias_disfrutados + diasSolicitados;
                    solicitud.Vacaciones.fecha_actualizacion = DateTime.Now;

                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO
                    if (currentUserId!= null)
                    {
                        BitacoraHelper.RegistrarAccion("APROBAR_VACACIONES",
                            $"Aprobadas vacaciones de {solicitud.Vacaciones.Empleados.nombre1} {solicitud.Vacaciones.Empleados.apellido1} del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy} ({diasSolicitados} días)",
                            currentUserId.Value);
                    }

                    // 🔔 NOTIFICACIÓN USANDO BITÁCORA (mientras no exista tabla Notificaciones)
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

        // 🎯 CASO DE USO 3: Rechazar solicitud (con validación jerárquica)
        [HttpPost]
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult Rechazar(int id, string comentario_respuesta = "")
        {
            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .Include(s => s.Vacaciones.Empleados.Puestos)
                    .Include(s => s.Vacaciones.Empleados.Puestos.Departamentos)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                {
                    return Json(new { success = false, message = "Solicitud no encontrada." });
                }

                var currentUserId = (int?)Session["UserId"];

                // 🔒 VALIDACIÓN JERÁRQUICA
                if (!PuedeAprobarSolicitud(id, currentUserId.Value))
                {
                    return Json(new { success = false, message = "No tiene permisos para rechazar esta solicitud según la jerarquía organizacional." });
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
                    if (currentUserId!= null)
                    {
                        var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
                        BitacoraHelper.RegistrarAccion("RECHAZAR_VACACIONES",
                            $"Rechazadas vacaciones de {solicitud.Vacaciones.Empleados.nombre1} {solicitud.Vacaciones.Empleados.apellido1} del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy} ({diasSolicitados} días)",
                            currentUserId.Value);
                    }

                    // 🔔 NOTIFICACIÓN USANDO BITÁCORA
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

            // Verificar acceso
            bool tieneAcceso = false;
            if (currentUserRole == "Admin" || currentUserRole == "RRHH")
            {
                tieneAcceso = true;
            }
            else if (currentUserRole == "Empleado")
            {
                // Solo puede ver sus propias solicitudes a través de la relación con Usuarios
                var usuarioActual = db.Usuarios.FirstOrDefault(u => u.id_usuario == currentUserId);
                tieneAcceso = usuarioActual != null && solicitud.Usuarios.id_usuario == usuarioActual.id_usuario;
            }
            else if (currentUserRole == "Supervisor")
            {
                // Puede ver solicitudes que puede aprobar
                tieneAcceso = PuedeAprobarSolicitud(solicitud.id_solicitud, currentUserId.Value);
            }

            if (!tieneAcceso)
            {
                TempData["Error"] = "No tiene permisos para ver esta solicitud.";
                return RedirectToAction("Index");
            }

            // 📊 Información adicional para la vista
            ViewBag.DiasCalculados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
            ViewBag.PuedeAprobar = PuedeAprobarSolicitud(solicitud.id_solicitud, currentUserId.Value);
            ViewBag.CadenaAprobacion = ObtenerCadenaAprobacion(solicitud.Vacaciones.id_empleado);

            return View(solicitud);
        }

        // GET: SolicitudesVacaciones/Create
        public ActionResult Create(int? id_vacacion)
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId == null)
                {
                    return RedirectToAction("Login", "Usuarios");
                }

                // Obtener empleado actual
                var empleadoActual = db.Usuarios
                    .Include(u => u.Empleados)
                    .Include(u => u.Empleados.Puestos)
                    .Include(u => u.Empleados.Puestos.Departamentos)
                    .FirstOrDefault(u => u.id_usuario == currentUserId.Value)?.Empleados;

                if (empleadoActual == null)
                {
                    TempData["Error"] = "No se encontró información del empleado.";
                    return RedirectToAction("Index");
                }

                // 🔍 MOSTRAR CADENA DE APROBACIÓN
                var cadenaAprobacion = ObtenerCadenaAprobacion(empleadoActual.id_empleado);
                ViewBag.CadenaAprobacion = cadenaAprobacion;
                ViewBag.EmpleadoActual = empleadoActual;

                if (id_vacacion!= null)
                {
                    var vacacion = db.Vacaciones.Find(id_vacacion.Value);
                    if (vacacion != null)
                    {
                        ViewBag.VacacionSeleccionada = vacacion;
                    }
                }

                CargarListasDesplegables();
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar formulario de solicitud.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index");
            }
        }

        // POST: SolicitudesVacaciones/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_vacacion,fecha_inicio,fecha_fin,comentario_solicitud")] SolicitudesVacaciones solicitud)
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId == null)
                {
                    return RedirectToAction("Login", "Usuarios");
                }

                // Validar solicitud
                var (esValida, mensaje) = ValidarSolicitud(solicitud);
                if (!esValida)
                {
                    ModelState.AddModelError("", mensaje);
                    CargarListasDesplegables();
                    return View(solicitud);
                }

                if (ModelState.IsValid)
                {
                    var estadoPendiente = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Vacaciones");
                    if (estadoPendiente == null)
                    {
                        ModelState.AddModelError("", "Error: Estado 'Pendiente' no encontrado.");
                        CargarListasDesplegables();
                        return View(solicitud);
                    }

                    // Completar información
                    solicitud.id_estado = estadoPendiente.id_estado;
                    solicitud.fecha_solicitud = DateTime.Now;
                    solicitud.fecha_creacion = DateTime.Now;
                    solicitud.fecha_actualizacion = DateTime.Now;

                    // Crear relación con el usuario actual
                    var usuarioActual = db.Usuarios.FirstOrDefault(u => u.id_usuario == currentUserId.Value);
                    if (usuarioActual != null)
                    {
                        solicitud.Usuarios = usuarioActual;
                    }

                    db.SolicitudesVacaciones.Add(solicitud);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO
                    var vacacion = db.Vacaciones.Include(v => v.Empleados).FirstOrDefault(v => v.id_vacacion == solicitud.id_vacacion);
                    var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);

                    BitacoraHelper.RegistrarAccion("CREAR_SOLICITUD_VACACIONES",
                        $"Solicitud de {diasSolicitados} días del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy}",
                        currentUserId.Value);

                    // 🔔 NOTIFICAR A LA CADENA DE APROBACIÓN
                    NotificarSolicitudCreada(solicitud);

                    TempData["Success"] = "Solicitud de vacaciones enviada exitosamente. Los aprobadores han sido notificados.";
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

            CargarListasDesplegables();
            return View(solicitud);
        }

        // GET: SolicitudesVacaciones/Edit/5
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            SolicitudesVacaciones solicitud = db.SolicitudesVacaciones
                .Include(s => s.Estados)
                .Include(s => s.Usuarios)
                .Include(s => s.Vacaciones)
                .FirstOrDefault(s => s.id_solicitud == id);

            if (solicitud == null)
                return HttpNotFound();

            CargarListasDesplegables(solicitud);
            return View(solicitud);
        }

        // POST: SolicitudesVacaciones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit([Bind(Include = "id_solicitud,id_vacacion,fecha_inicio,fecha_fin,comentario_solicitud,id_estado,comentario_respuesta,fecha_solicitud,fecha_creacion")] SolicitudesVacaciones solicitud)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    solicitud.fecha_actualizacion = DateTime.Now;
                    db.Entry(solicitud).State = EntityState.Modified;
                    db.SaveChanges();

                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId!= null)
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

                var estadoAprobado = db.Estados.FirstOrDefault(e => e.nombre == "Aprobado" && e.modulo == "Vacaciones");
                if (solicitud.id_estado == estadoAprobado?.id_estado)
                {
                    TempData["Error"] = "No se pueden eliminar solicitudes ya aprobadas que afectaron los días de vacaciones.";
                    return RedirectToAction("Index");
                }

                var currentUserId = (int?)Session["UserId"];
                if (currentUserId!= null)
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

        #region Métodos Jerárquicos

        // 🔑 MÉTODO CLAVE: Obtener jefe directo del empleado
        private Empleados ObtenerJefeDirecto(int idEmpleado)
        {
            try
            {
                var empleado = db.Empleados
                    .Include(e => e.Puestos)
                    .Include(e => e.Puestos.Departamentos)
                    .FirstOrDefault(e => e.id_empleado == idEmpleado);

                if (empleado?.Puestos?.id_departamento == null)
                    return null;

                // Buscar el jefe del departamento del empleado
                var jefeDepartamento = db.Empleados
                    .Include(e => e.Puestos)
                    .Include(e => e.Usuarios)
                    .FirstOrDefault(e => e.Puestos.id_departamento == empleado.Puestos.id_departamento
                                      && e.Puestos.es_jefe == true
                                      && e.estado == "Activo");

                return jefeDepartamento;
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return null;
            }
        }

        // 🔑 MÉTODO CLAVE: Obtener cadena completa de aprobación
        private List<Empleados> ObtenerCadenaAprobacion(int idEmpleado)
        {
            var cadenaAprobacion = new List<Empleados>();

            try
            {
                // 1. Jefe directo (del departamento)
                var jefeDirecto = ObtenerJefeDirecto(idEmpleado);
                if (jefeDirecto != null)
                {
                    cadenaAprobacion.Add(jefeDirecto);
                }

                // 2. RRHH (siempre puede aprobar)
                var rrhh = db.Usuarios
                    .Include(u => u.Empleados)
                    .Include(u => u.Roles)
                    .Where(u => u.Roles.nombre == "RRHH" && u.Empleados.estado == "Activo")
                    .Select(u => u.Empleados)
                    .FirstOrDefault();

                if (rrhh != null && !cadenaAprobacion.Any(c => c.id_empleado == rrhh.id_empleado))
                {
                    cadenaAprobacion.Add(rrhh);
                }

                // 3. Admin (siempre puede aprobar)
                var admin = db.Usuarios
                    .Include(u => u.Empleados)
                    .Include(u => u.Roles)
                    .Where(u => u.Roles.nombre == "Admin" && u.Empleados.estado == "Activo")
                    .Select(u => u.Empleados)
                    .FirstOrDefault();

                if (admin != null && !cadenaAprobacion.Any(c => c.id_empleado == admin.id_empleado))
                {
                    cadenaAprobacion.Add(admin);
                }

                return cadenaAprobacion;
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return new List<Empleados>();
            }
        }

        // 🔑 MÉTODO CLAVE: Validar si el usuario puede aprobar la solicitud
        private bool PuedeAprobarSolicitud(int idSolicitud, int idUsuarioAprobador)
        {
            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .Include(s => s.Vacaciones.Empleados.Puestos)
                    .FirstOrDefault(s => s.id_solicitud == idSolicitud);

                if (solicitud == null) return false;

                var usuarioAprobador = db.Usuarios
                    .Include(u => u.Empleados)
                    .Include(u => u.Empleados.Puestos)
                    .Include(u => u.Roles)
                    .FirstOrDefault(u => u.id_usuario == idUsuarioAprobador);

                if (usuarioAprobador == null) return false;

                var rolAprobador = usuarioAprobador.Roles.nombre;
                var empleadoSolicitante = solicitud.Vacaciones.Empleados;

                // 🔐 REGLAS DE APROBACIÓN JERÁRQUICA
                switch (rolAprobador)
                {
                    case "Admin":
                        return true; // Admin puede aprobar cualquier solicitud

                    case "RRHH":
                        return true; // RRHH puede aprobar cualquier solicitud

                    case "Supervisor":
                    case "Jefe Departamento":
                        // Solo puede aprobar empleados de su departamento
                        var departamentoAprobador = usuarioAprobador.Empleados?.Puestos?.id_departamento;
                        var departamentoSolicitante = empleadoSolicitante?.Puestos?.id_departamento;

                        // Verificar que es jefe del departamento
                        var esJefe = usuarioAprobador.Empleados?.Puestos?.es_jefe == true;

                        return departamentoAprobador!= null &&
                               departamentoSolicitante!= null &&
                               departamentoAprobador.Value == departamentoSolicitante.Value &&
                               esJefe;

                    default:
                        return false; // Empleados regulares no pueden aprobar
                }
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return false;
            }
        }

        // 🔔 MÉTODO CLAVE: Notificar solicitud creada a la cadena de aprobación
        private void NotificarSolicitudCreada(SolicitudesVacaciones solicitud)
        {
            try
            {
                var empleadoSolicitante = db.Empleados
                    .Include(e => e.Puestos)
                    .Include(e => e.Puestos.Departamentos)
                    .FirstOrDefault(e => e.id_empleado == solicitud.Vacaciones.id_empleado);

                if (empleadoSolicitante == null) return;

                var cadenaAprobacion = ObtenerCadenaAprobacion(empleadoSolicitante.id_empleado);
                var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);

                // 📧 Registrar notificación en bitácora para cada aprobador
                foreach (var aprobador in cadenaAprobacion)
                {
                    var usuarioAprobador = aprobador.Usuarios.FirstOrDefault();
                    if (usuarioAprobador != null)
                    {
                        BitacoraHelper.RegistrarAccion("NOTIFICACION_SOLICITUD_VACACIONES",
                            $"PARA:{aprobador.nombre1} {aprobador.apellido1} - Nueva solicitud de {empleadoSolicitante.nombre1} {empleadoSolicitante.apellido1} ({empleadoSolicitante.Puestos?.Departamentos?.nombre}) - {diasSolicitados} días del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy}",
                            usuarioAprobador.id_usuario);
                    }
                }

                // LOG general de notificaciones enviadas
                BitacoraHelper.RegistrarAccion("NOTIFICAR_SOLICITUD_CREADA",
                    $"Notificaciones enviadas a {cadenaAprobacion.Count} aprobadores para solicitud de {empleadoSolicitante.nombre1} {empleadoSolicitante.apellido1}",
                    solicitud.Usuarios.id_usuario);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
            }
        }

        #endregion

        #region Métodos auxiliares

        // 🔔 MÉTODO MEJORADO: Notificación automática usando bitácora
        private void NotificarDecisionVacaciones(SolicitudesVacaciones solicitud, string decision)
        {
            try
            {
                var empleado = solicitud.Vacaciones.Empleados;
                var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);

                // Obtener usuario del empleado
                var usuarioEmpleado = empleado.Usuarios.FirstOrDefault();
                if (usuarioEmpleado != null)
                {
                    var mensaje = $"Su solicitud de {diasSolicitados} días de vacaciones del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy} ha sido {decision.ToLower()}.";

                    if (!string.IsNullOrEmpty(solicitud.comentario_respuesta))
                    {
                        mensaje += $" Comentario: {solicitud.comentario_respuesta}";
                    }

                    // 🔔 REGISTRAR NOTIFICACIÓN EN BITÁCORA
                    BitacoraHelper.RegistrarAccion($"NOTIFICACION_VACACIONES_{decision}",
                        $"PARA:{empleado.nombre1} {empleado.apellido1} - {mensaje}",
                        usuarioEmpleado.id_usuario);
                }

                // 📋 Registrar la notificación en bitácora general
                BitacoraHelper.RegistrarAccion("NOTIFICAR_DECISION_VACACIONES",
                    $"Notificación enviada a {empleado.nombre1} {empleado.apellido1}: Solicitud {decision}",
                    solicitud.aprobado_por ?? 0);
            }
            catch (Exception ex)
            {
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
                var diasDisponibles = vacaciones.dias_disponibles - (vacaciones.dias_disfrutados);
                if (diasSolicitados > diasDisponibles)
                    return (false, $"Solo hay {diasDisponibles} día(s) disponible(s). Se están solicitando {diasSolicitados} día(s).");
            }

            return (true, "");
        }

        private void CargarListasDesplegables(SolicitudesVacaciones solicitud = null)
        {
            var currentUserId = (int?)Session["UserId"];
            var currentUserRole = Session["RolUsuario"] as string;

            if (currentUserRole == "Empleado")
            {
                var empleado = db.Usuarios.Include(u => u.Empleados)
                    .FirstOrDefault(u => u.id_usuario == currentUserId)?.Empleados;

                if (empleado != null)
                {
                    ViewBag.id_vacacion = new SelectList(
                        db.Vacaciones.Where(v => v.id_empleado == empleado.id_empleado),
                        "id_vacacion", "periodo", solicitud?.id_vacacion);
                }
            }
            else
            {
                ViewBag.id_vacacion = new SelectList(db.Vacaciones.Include(v => v.Empleados)
                    .Select(v => new {
                        v.id_vacacion,
                        Display = v.Empleados.nombre1 + " " + v.Empleados.apellido1 + " - " + v.periodo
                    }), "id_vacacion", "Display", solicitud?.id_vacacion);
            }

            ViewBag.id_estado = new SelectList(
                db.Estados.Where(e => e.modulo == "Vacaciones"),
                "id_estado", "nombre", solicitud?.id_estado);
        }

        private void HandleDbUpdateException(DbUpdateException ex)
        {
            var sqlEx = ex.GetBaseException() as System.Data.SqlClient.SqlException;
            if (sqlEx != null)
            {
                switch (sqlEx.Number)
                {
                    case 547:
                        ModelState.AddModelError("", "No se puede eliminar porque existen registros relacionados.");
                        break;
                    case 2:
                        ModelState.AddModelError("", "Error de conexión con la base de datos.");
                        break;
                    default:
                        ModelState.AddModelError("", "Error en la base de datos: " + sqlEx.Message);
                        break;
                }
            }
            else
            {
                ModelState.AddModelError("", "Error al actualizar la base de datos.");
            }
            LogExceptionDetails(ex);
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