using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using SistemaNomina.Models;
using SistemaNomina.Helpers;

namespace SistemaNomina.Controllers
{
    public class SolicitudesVacacionesController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // =====================================================
        // ACCIONES PRINCIPALES
        // =====================================================

        // GET: SolicitudesVacaciones
        public ActionResult Index()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                if (currentUserId == null)
                {
                    return RedirectToAction("Login", "Usuarios");
                }

                IQueryable<SolicitudesVacaciones> solicitudesQuery = db.SolicitudesVacaciones
                    .Include(s => s.Estados)
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .Include(s => s.Vacaciones.Empleados.Puestos)
                    .Include(s => s.Vacaciones.Empleados.Puestos.Departamentos)
                    .Include(s => s.Usuarios)
                    .Include(s => s.Usuarios.Empleados);

                // Filtrar según rol
                switch (currentUserRole)
                {
                    case "Empleado":
                        // Solo sus propias solicitudes
                        var usuarioEmpleado = db.Usuarios.FirstOrDefault(u => u.id_usuario == currentUserId);
                        if (usuarioEmpleado != null)
                        {
                            solicitudesQuery = solicitudesQuery
                                .Where(s => s.Vacaciones.id_empleado == usuarioEmpleado.id_empleado);
                        }
                        break;

                    case "Supervisor":
                        // Solicitudes de su departamento
                        var usuarioSupervisor = db.Usuarios
                            .Include(u => u.Empleados)
                            .Include(u => u.Empleados.Puestos)
                            .FirstOrDefault(u => u.id_usuario == currentUserId);

                        if (usuarioSupervisor != null)
                        {
                            var idDepartamento = usuarioSupervisor.Empleados.Puestos.id_departamento;
                            solicitudesQuery = solicitudesQuery
                                .Where(s => s.Vacaciones.Empleados.Puestos.id_departamento == idDepartamento);
                        }
                        break;

                    case "Administrador":
                    case "RRHH":
                        // Todas las solicitudes
                        break;

                    default:
                        return RedirectToAction("Unauthorized", "Home");
                }

                var solicitudes = solicitudesQuery
                    .OrderByDescending(s => s.fecha_solicitud)
                    .ToList();

                ViewBag.CurrentUserRole = currentUserRole;
                ViewBag.CurrentUserId = currentUserId;

                return View(solicitudes);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                TempData["Error"] = "Error al cargar las solicitudes: " + ex.Message;
                return View(new List<SolicitudesVacaciones>());
            }
        }

        // GET: SolicitudesVacaciones/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Estados)
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .Include(s => s.Vacaciones.Empleados.Puestos)
                    .Include(s => s.Vacaciones.Empleados.Puestos.Departamentos)
                    .Include(s => s.Usuarios)
                    .Include(s => s.Usuarios.Empleados)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                    return HttpNotFound();

                // Verificar acceso
                var currentUserId = (int?)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                if (!TieneAccesoSolicitud(solicitud, currentUserId, currentUserRole))
                {
                    TempData["Error"] = "No tiene permisos para ver esta solicitud.";
                    return RedirectToAction("Index");
                }

                // Información adicional para la vista
                ViewBag.DiasCalculados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
                ViewBag.PuedeAprobar = PuedeAprobarSolicitud(solicitud.id_solicitud, currentUserId.Value);

                return View(solicitud);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                TempData["Error"] = "Error al cargar los detalles: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: SolicitudesVacaciones/Create
        public ActionResult Create(int? id_vacacion)
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                if (currentUserId == null)
                    return RedirectToAction("Login", "Usuarios");

                if (currentUserRole != "Empleado" && currentUserRole != "Administrador" && currentUserRole != "RRHH")
                {
                    TempData["Error"] = "No tiene permisos para crear solicitudes.";
                    return RedirectToAction("Index");
                }

                // Obtener empleado del usuario actual
                var usuario = db.Usuarios.FirstOrDefault(u => u.id_usuario == currentUserId);
                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction("Index");
                }

                // Obtener o crear vacaciones para el empleado
                var vacaciones = ObtenerOCrearVacaciones(usuario.id_empleado);
                if (vacaciones == null)
                {
                    TempData["Error"] = "No se pudieron obtener las vacaciones del empleado.";
                    return RedirectToAction("Index");
                }

                // Verificar que tenga días disponibles
                if (vacaciones.dias_disponibles <= 0)
                {
                    TempData["Error"] = "No tiene días de vacaciones disponibles.";
                    return RedirectToAction("Index");
                }

                var model = new SolicitudVacacionesViewModel
                {
                    id_vacacion = vacaciones.id_vacacion,
                    fecha_inicio = DateTime.Today.AddDays(1),
                    fecha_fin = DateTime.Today.AddDays(2)
                };

                ViewBag.DiasDisponibles = vacaciones.dias_disponibles;
                ViewBag.EmpleadoNombre = $"{usuario.Empleados.nombre1} {usuario.Empleados.apellido1}";
                ViewBag.Periodo = vacaciones.periodo;

                return View(model);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                TempData["Error"] = "Error al cargar el formulario: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: SolicitudesVacaciones/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SolicitudVacacionesViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                // Validar que el usuario tenga vacaciones
                var vacaciones = db.Vacaciones.Find(model.id_vacacion);
                if (vacaciones == null)
                {
                    ModelState.AddModelError("", "No se encontraron vacaciones para el empleado.");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                // Validar fechas
                if (model.fecha_inicio <= DateTime.Today)
                {
                    ModelState.AddModelError("fecha_inicio", "La fecha de inicio debe ser posterior a hoy.");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                if (model.fecha_fin <= model.fecha_inicio)
                {
                    ModelState.AddModelError("fecha_fin", "La fecha de fin debe ser posterior a la fecha de inicio.");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                // Calcular días solicitados
                var diasSolicitados = CalcularDiasLaborales(model.fecha_inicio, model.fecha_fin);

                // Validar días disponibles
                if (diasSolicitados > vacaciones.dias_disponibles)
                {
                    ModelState.AddModelError("", $"No tiene suficientes días disponibles. Disponibles: {vacaciones.dias_disponibles}, Solicitados: {diasSolicitados}");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                // Verificar que no haya solicitudes pendientes en el mismo período
                var solicitudExistente = db.SolicitudesVacaciones
                    .Include(s => s.Estados)
                    .Where(s => s.id_vacacion == model.id_vacacion)
                    .Where(s => s.Estados.nombre == "Pendiente")
                    .Where(s => (s.fecha_inicio <= model.fecha_fin && s.fecha_fin >= model.fecha_inicio))
                    .FirstOrDefault();

                if (solicitudExistente != null)
                {
                    ModelState.AddModelError("", "Ya existe una solicitud pendiente que se traslapa con estas fechas.");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                // Crear la solicitud
                var estadoPendiente = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Vacaciones");
                if (estadoPendiente == null)
                {
                    ModelState.AddModelError("", "Error de configuración: Estado 'Pendiente' no encontrado.");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                var solicitud = new SolicitudesVacaciones
                {
                    id_vacacion = model.id_vacacion,
                    fecha_inicio = model.fecha_inicio,
                    fecha_fin = model.fecha_fin,
                    comentario_solicitud = model.comentario_solicitud,
                    id_estado = estadoPendiente.id_estado,
                    fecha_creacion = DateTime.Now,
                    fecha_solicitud = DateTime.Now,
                    fecha_actualizacion = DateTime.Now
                };

                db.SolicitudesVacaciones.Add(solicitud);
                db.SaveChanges();

                // Notificar al jefe del departamento
                NotificarSolicitudAlJefe(solicitud, vacaciones.id_empleado, diasSolicitados);

                // Registrar en bitácora
                var currentUserId = (int)Session["UserId"];
                BitacoraHelper.RegistrarAccion("SOLICITUD_VACACIONES_CREADA",
                    $"Solicitud de {diasSolicitados} días del {model.fecha_inicio:dd/MM/yyyy} al {model.fecha_fin:dd/MM/yyyy}",
                    currentUserId);

                TempData["Success"] = "Solicitud de vacaciones creada exitosamente. Se ha notificado a su jefe para revisión.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                ModelState.AddModelError("", "Error al crear la solicitud: " + ex.Message);
                CargarDatosVista(model.id_vacacion);
                return View(model);
            }
        }

        // GET: Contador de solicitudes pendientes
        [HttpGet]
        public ActionResult GetContadorSolicitudes()
        {
            try
            {
                var currentUserId = (int)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                if (currentUserRole != "Supervisor" && currentUserRole != "Administrador" && currentUserRole != "RRHH")
                {
                    return Json(new { success = false, count = 0 }, JsonRequestBehavior.AllowGet);
                }

                // Contar solicitudes pendientes
                IQueryable<SolicitudesVacaciones> solicitudesQuery = db.SolicitudesVacaciones
                    .Include(s => s.Estados)
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .Include(s => s.Vacaciones.Empleados.Puestos)
                    .Where(s => s.Estados.nombre == "Pendiente");

                // Filtrar según rol
                if (currentUserRole == "Supervisor")
                {
                    var usuarioActual = db.Usuarios
                        .Include(u => u.Empleados)
                        .Include(u => u.Empleados.Puestos)
                        .FirstOrDefault(u => u.id_usuario == currentUserId);

                    if (usuarioActual != null)
                    {
                        var idDepartamento = usuarioActual.Empleados.Puestos.id_departamento;
                        solicitudesQuery = solicitudesQuery
                            .Where(s => s.Vacaciones.Empleados.Puestos.id_departamento == idDepartamento);
                    }
                }

                var count = solicitudesQuery.Count();

                return Json(new { success = true, count = count }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, count = 0 }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: SolicitudesVacaciones/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Estados)
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                    return HttpNotFound();

                // Solo se pueden editar solicitudes pendientes
                if (solicitud.Estados.nombre != "Pendiente")
                {
                    TempData["Error"] = "Solo se pueden editar solicitudes pendientes.";
                    return RedirectToAction("Index");
                }

                // Verificar acceso
                var currentUserId = (int?)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                if (!TieneAccesoSolicitud(solicitud, currentUserId, currentUserRole))
                {
                    TempData["Error"] = "No tiene permisos para editar esta solicitud.";
                    return RedirectToAction("Index");
                }

                var model = new SolicitudVacacionesViewModel
                {
                    id_solicitud = solicitud.id_solicitud,
                    id_vacacion = solicitud.id_vacacion,
                    fecha_inicio = solicitud.fecha_inicio,
                    fecha_fin = solicitud.fecha_fin,
                    comentario_solicitud = solicitud.comentario_solicitud
                };

                CargarDatosVista(solicitud.id_vacacion);
                return View(model);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                TempData["Error"] = "Error al cargar la solicitud: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: SolicitudesVacaciones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SolicitudVacacionesViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                var solicitud = db.SolicitudesVacaciones.Find(model.id_solicitud);
                if (solicitud == null)
                {
                    ModelState.AddModelError("", "Solicitud no encontrada.");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                // Verificar que siga siendo editable
                var estado = db.Estados.Find(solicitud.id_estado);
                if (estado?.nombre != "Pendiente")
                {
                    ModelState.AddModelError("", "Solo se pueden editar solicitudes pendientes.");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                // Validar fechas
                if (model.fecha_inicio <= DateTime.Today)
                {
                    ModelState.AddModelError("fecha_inicio", "La fecha de inicio debe ser posterior a hoy.");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                if (model.fecha_fin <= model.fecha_inicio)
                {
                    ModelState.AddModelError("fecha_fin", "La fecha de fin debe ser posterior a la fecha de inicio.");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                // Validar días disponibles
                var vacaciones = db.Vacaciones.Find(model.id_vacacion);
                var diasSolicitados = CalcularDiasLaborales(model.fecha_inicio, model.fecha_fin);

                if (diasSolicitados > vacaciones.dias_disponibles)
                {
                    ModelState.AddModelError("", $"No tiene suficientes días disponibles. Disponibles: {vacaciones.dias_disponibles}, Solicitados: {diasSolicitados}");
                    CargarDatosVista(model.id_vacacion);
                    return View(model);
                }

                // Actualizar la solicitud
                solicitud.fecha_inicio = model.fecha_inicio;
                solicitud.fecha_fin = model.fecha_fin;
                solicitud.comentario_solicitud = model.comentario_solicitud;
                solicitud.fecha_actualizacion = DateTime.Now;

                db.SaveChanges();

                // Registrar en bitácora
                var currentUserId = (int)Session["UserId"];
                BitacoraHelper.RegistrarAccion("SOLICITUD_VACACIONES_EDITADA",
                    $"Solicitud actualizada: {diasSolicitados} días del {model.fecha_inicio:dd/MM/yyyy} al {model.fecha_fin:dd/MM/yyyy}",
                    currentUserId);

                TempData["Success"] = "Solicitud actualizada exitosamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                ModelState.AddModelError("", "Error al actualizar la solicitud: " + ex.Message);
                CargarDatosVista(model.id_vacacion);
                return View(model);
            }
        }

        // GET: SolicitudesVacaciones/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Estados)
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                    return HttpNotFound();

                // Solo se pueden eliminar solicitudes pendientes
                if (solicitud.Estados.nombre != "Pendiente")
                {
                    TempData["Error"] = "Solo se pueden eliminar solicitudes pendientes.";
                    return RedirectToAction("Index");
                }

                // Verificar acceso
                var currentUserId = (int?)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                if (!TieneAccesoSolicitud(solicitud, currentUserId, currentUserRole))
                {
                    TempData["Error"] = "No tiene permisos para eliminar esta solicitud.";
                    return RedirectToAction("Index");
                }

                ViewBag.DiasCalculados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
                return View(solicitud);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                TempData["Error"] = "Error al cargar la solicitud: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: SolicitudesVacaciones/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                var solicitud = db.SolicitudesVacaciones.Find(id);
                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada.";
                    return RedirectToAction("Index");
                }

                // Verificar que siga siendo eliminable
                var estado = db.Estados.Find(solicitud.id_estado);
                if (estado?.nombre != "Pendiente")
                {
                    TempData["Error"] = "Solo se pueden eliminar solicitudes pendientes.";
                    return RedirectToAction("Index");
                }

                db.SolicitudesVacaciones.Remove(solicitud);
                db.SaveChanges();

                // Registrar en bitácora
                var currentUserId = (int)Session["UserId"];
                BitacoraHelper.RegistrarAccion("SOLICITUD_VACACIONES_ELIMINADA",
                    $"Solicitud eliminada: {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy}",
                    currentUserId);

                TempData["Success"] = "Solicitud eliminada exitosamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                TempData["Error"] = "Error al eliminar la solicitud: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =====================================================
        // ACCIONES ESPECÍFICAS PARA APROBACIÓN
        // =====================================================

        // GET: SolicitudesVacaciones/SolicitudesPendientes
        public ActionResult SolicitudesPendientes()
        {
            try
            {
                var currentUserId = (int)Session["UserId"];
                var currentUserRole = Session["RolUsuario"] as string;

                // Solo supervisores, admin y RRHH pueden ver solicitudes pendientes
                if (currentUserRole != "Supervisor" && currentUserRole != "Administrador" && currentUserRole != "RRHH")
                {
                    TempData["Error"] = "No tiene permisos para ver solicitudes pendientes.";
                    return RedirectToAction("Index");
                }

                // Obtener solicitudes pendientes
                IQueryable<SolicitudesVacaciones> solicitudesQuery = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .Include(s => s.Vacaciones.Empleados.Puestos)
                    .Include(s => s.Vacaciones.Empleados.Puestos.Departamentos)
                    .Include(s => s.Estados)
                    .Where(s => s.Estados.nombre == "Pendiente");

                // Filtrar según rol
                if (currentUserRole == "Supervisor")
                {
                    // Solo solicitudes de su departamento
                    var usuarioActual = db.Usuarios
                        .Include(u => u.Empleados)
                        .Include(u => u.Empleados.Puestos)
                        .FirstOrDefault(u => u.id_usuario == currentUserId);

                    if (usuarioActual != null)
                    {
                        var idDepartamento = usuarioActual.Empleados.Puestos.id_departamento;
                        solicitudesQuery = solicitudesQuery
                            .Where(s => s.Vacaciones.Empleados.Puestos.id_departamento == idDepartamento);
                    }
                }
                // Admin y RRHH ven todas las solicitudes

                var solicitudes = solicitudesQuery
                    .OrderByDescending(s => s.fecha_solicitud)
                    .ToList();

                return View(solicitudes);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                TempData["Error"] = "Error al cargar solicitudes pendientes: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: SolicitudesVacaciones/Aprobar
        [HttpPost]
        public ActionResult Aprobar(int id, string comentario = "")
        {
            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada." });

                // Verificar permisos
                var currentUserId = (int)Session["UserId"];
                if (!PuedeAprobarSolicitud(id, currentUserId))
                    return Json(new { success = false, message = "No tiene permisos para aprobar esta solicitud." });

                // Verificar que esté pendiente
                var estadoActual = db.Estados.Find(solicitud.id_estado);
                if (estadoActual?.nombre != "Pendiente")
                    return Json(new { success = false, message = "Solo se pueden aprobar solicitudes pendientes." });

                // Actualizar estado
                var estadoAprobado = db.Estados.FirstOrDefault(e => e.nombre == "Aprobado" && e.modulo == "Vacaciones");
                solicitud.id_estado = estadoAprobado.id_estado;
                solicitud.aprobado_por = currentUserId;
                solicitud.fecha_aprobacion = DateTime.Now;
                solicitud.comentario_respuesta = comentario;
                solicitud.fecha_actualizacion = DateTime.Now;

                // Actualizar días de vacaciones
                var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
                solicitud.Vacaciones.dias_disponibles -= diasSolicitados;
                solicitud.Vacaciones.dias_disfrutados += diasSolicitados;
                solicitud.Vacaciones.fecha_actualizacion = DateTime.Now;

                db.SaveChanges();

                // Notificar al empleado
                NotificarDecisionAlEmpleado(solicitud, "APROBADA", diasSolicitados);

                // Registrar en bitácora
                BitacoraHelper.RegistrarAccion("SOLICITUD_VACACIONES_APROBADA",
                    $"Solicitud aprobada para {solicitud.Vacaciones.Empleados.nombre1} {solicitud.Vacaciones.Empleados.apellido1} - {diasSolicitados} días",
                    currentUserId);

                return Json(new { success = true, message = "Solicitud aprobada exitosamente. El empleado ha sido notificado." });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error al aprobar solicitud: " + ex.Message });
            }
        }

        // POST: SolicitudesVacaciones/Rechazar
        [HttpPost]
        public ActionResult Rechazar(int id, string comentario = "")
        {
            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .FirstOrDefault(s => s.id_solicitud == id);

                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada." });

                // Verificar permisos
                var currentUserId = (int)Session["UserId"];
                if (!PuedeAprobarSolicitud(id, currentUserId))
                    return Json(new { success = false, message = "No tiene permisos para rechazar esta solicitud." });

                // Verificar que esté pendiente
                var estadoActual = db.Estados.Find(solicitud.id_estado);
                if (estadoActual?.nombre != "Pendiente")
                    return Json(new { success = false, message = "Solo se pueden rechazar solicitudes pendientes." });

                // Actualizar estado
                var estadoRechazado = db.Estados.FirstOrDefault(e => e.nombre == "Rechazado" && e.modulo == "Vacaciones");
                solicitud.id_estado = estadoRechazado.id_estado;
                solicitud.aprobado_por = currentUserId;
                solicitud.fecha_aprobacion = DateTime.Now;
                solicitud.comentario_respuesta = comentario;
                solicitud.fecha_actualizacion = DateTime.Now;

                db.SaveChanges();

                // Notificar al empleado
                var diasSolicitados = CalcularDiasLaborales(solicitud.fecha_inicio, solicitud.fecha_fin);
                NotificarDecisionAlEmpleado(solicitud, "RECHAZADA", diasSolicitados);

                // Registrar en bitácora
                BitacoraHelper.RegistrarAccion("SOLICITUD_VACACIONES_RECHAZADA",
                    $"Solicitud rechazada para {solicitud.Vacaciones.Empleados.nombre1} {solicitud.Vacaciones.Empleados.apellido1} - Motivo: {comentario}",
                    currentUserId);

                return Json(new { success = true, message = "Solicitud rechazada exitosamente. El empleado ha sido notificado." });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error al rechazar solicitud: " + ex.Message });
            }
        }

        // =====================================================
        // MÉTODOS AUXILIARES
        // =====================================================

        /// <summary>
        /// Obtiene o crea vacaciones para un empleado en el período actual
        /// </summary>
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

        /// <summary>
        /// Calcula días de vacaciones según antigüedad (Código de Trabajo CR)
        /// </summary>
        private int CalcularDiasVacaciones(DateTime fechaIngreso)
        {
            var semanasTranscurridas = (DateTime.Now - fechaIngreso).TotalDays / 7;
            var ciclosCompletos = (int)(semanasTranscurridas / 50);
            var diasAcumulados = ciclosCompletos * 12;
            return Math.Max(0, Math.Min(24, diasAcumulados));
        }

        /// <summary>
        /// Calcula días laborales entre dos fechas (excluyendo fines de semana)
        /// </summary>
        private int CalcularDiasLaborales(DateTime fechaInicio, DateTime fechaFin)
        {
            int diasLaborales = 0;
            var fechaActual = fechaInicio;

            while (fechaActual <= fechaFin)
            {
                if (fechaActual.DayOfWeek != DayOfWeek.Saturday && fechaActual.DayOfWeek != DayOfWeek.Sunday)
                {
                    diasLaborales++;
                }
                fechaActual = fechaActual.AddDays(1);
            }

            return diasLaborales;
        }

        /// <summary>
        /// Notifica la solicitud de vacaciones al jefe del departamento
        /// </summary>
        private void NotificarSolicitudAlJefe(SolicitudesVacaciones solicitud, int idEmpleado, int diasSolicitados)
        {
            try
            {
                // Obtener datos del empleado solicitante
                var empleado = db.Empleados
                    .Include(e => e.Puestos)
                    .Include(e => e.Puestos.Departamentos)
                    .FirstOrDefault(e => e.id_empleado == idEmpleado);

                if (empleado == null) return;

                // Obtener el jefe del departamento
                var jefeDepartamento = NotificacionesHelper.ObtenerJefeDepartamento(idEmpleado);
                if (jefeDepartamento == null)
                {
                    // Si no hay jefe, notificar a RRHH
                    var usuarioRRHH = db.Usuarios
                        .Include(u => u.Roles)
                        .FirstOrDefault(u => u.Roles.nombre == "RRHH");

                    if (usuarioRRHH != null)
                    {
                        NotificacionesHelper.CrearNotificacion(
                            usuarioRRHH.id_empleado,
                            idEmpleado,
                            "Nueva Solicitud de Vacaciones",
                            $"{empleado.nombre1} {empleado.apellido1} ({empleado.Puestos.Departamentos.nombre}) ha solicitado {diasSolicitados} días de vacaciones del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy}. Revisar y aprobar.",
                            "solicitud",
                            "SolicitudVacaciones",
                            solicitud.id_solicitud
                        );
                    }
                    return;
                }

                // Crear notificación al jefe
                var notificacionExitosa = NotificacionesHelper.CrearNotificacion(
                    jefeDepartamento.id_empleado,
                    idEmpleado,
                    "Nueva Solicitud de Vacaciones",
                    $"{empleado.nombre1} {empleado.apellido1} ha solicitado {diasSolicitados} días de vacaciones del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy}. Comentario: {solicitud.comentario_solicitud ?? "Sin comentarios"}",
                    "solicitud",
                    "SolicitudVacaciones",
                    solicitud.id_solicitud
                );

                if (notificacionExitosa)
                {
                    // Registrar en bitácora
                    BitacoraHelper.RegistrarAccion("NOTIFICACION_JEFE_ENVIADA",
                        $"Notificación enviada a {jefeDepartamento.nombre1} {jefeDepartamento.apellido1} sobre solicitud de {empleado.nombre1} {empleado.apellido1}",
                        (int)Session["UserId"]);
                }
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
            }
        }

        /// <summary>
        /// Notifica al empleado sobre la decisión de su solicitud
        /// </summary>
        private void NotificarDecisionAlEmpleado(SolicitudesVacaciones solicitud, string decision, int diasSolicitados)
        {
            try
            {
                var empleado = solicitud.Vacaciones.Empleados;
                var usuarioAprobador = db.Usuarios
                    .Include(u => u.Empleados)
                    .FirstOrDefault(u => u.id_usuario == solicitud.aprobado_por);

                string mensaje = $"Su solicitud de {diasSolicitados} días de vacaciones del {solicitud.fecha_inicio:dd/MM/yyyy} al {solicitud.fecha_fin:dd/MM/yyyy} ha sido {decision}.";

                if (!string.IsNullOrEmpty(solicitud.comentario_respuesta))
                {
                    mensaje += $"\n\nComentario del supervisor: {solicitud.comentario_respuesta}";
                }

                if (usuarioAprobador != null)
                {
                    mensaje += $"\n\nAprobado por: {usuarioAprobador.Empleados.nombre1} {usuarioAprobador.Empleados.apellido1}";
                }

                // Crear notificación
                NotificacionesHelper.CrearNotificacion(
                    empleado.id_empleado,
                    solicitud.aprobado_por,
                    $"Solicitud de Vacaciones {decision}",
                    mensaje,
                    decision == "APROBADA" ? "aprobacion" : "rechazo",
                    "SolicitudVacaciones",
                    solicitud.id_solicitud
                );
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
            }
        }

        /// <summary>
        /// Verifica si el usuario actual puede aprobar una solicitud
        /// </summary>
        private bool PuedeAprobarSolicitud(int idSolicitud, int idUsuario)
        {
            try
            {
                var solicitud = db.SolicitudesVacaciones
                    .Include(s => s.Vacaciones)
                    .Include(s => s.Vacaciones.Empleados)
                    .FirstOrDefault(s => s.id_solicitud == idSolicitud);

                if (solicitud == null) return false;

                var usuario = db.Usuarios
                    .Include(u => u.Roles)
                    .Include(u => u.Empleados)
                    .FirstOrDefault(u => u.id_usuario == idUsuario);

                if (usuario == null) return false;

                // Administrador y RRHH pueden aprobar cualquier solicitud
                if (usuario.Roles.nombre == "Administrador" || usuario.Roles.nombre == "RRHH")
                    return true;

                // Supervisor puede aprobar solicitudes de su departamento
                if (usuario.Roles.nombre == "Supervisor")
                {
                    var jefeDepartamento = NotificacionesHelper.ObtenerJefeDepartamento(solicitud.Vacaciones.id_empleado);
                    return jefeDepartamento != null && jefeDepartamento.id_empleado == usuario.id_empleado;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return false;
            }
        }

        /// <summary>
        /// Verifica si el usuario tiene acceso a una solicitud específica
        /// </summary>
        private bool TieneAccesoSolicitud(SolicitudesVacaciones solicitud, int? currentUserId, string currentUserRole)
        {
            if (currentUserId == null) return false;

            // Administrador y RRHH tienen acceso total
            if (currentUserRole == "Administrador" || currentUserRole == "RRHH")
                return true;

            // Empleado puede ver sus propias solicitudes
            if (currentUserRole == "Empleado")
            {
                var usuarioActual = db.Usuarios.FirstOrDefault(u => u.id_usuario == currentUserId);
                return usuarioActual != null && solicitud.Vacaciones.id_empleado == usuarioActual.id_empleado;
            }

            // Supervisor puede ver solicitudes de su departamento
            if (currentUserRole == "Supervisor")
            {
                return PuedeAprobarSolicitud(solicitud.id_solicitud, currentUserId.Value);
            }

            return false;
        }

        /// <summary>
        /// Carga datos necesarios para las vistas
        /// </summary>
        private void CargarDatosVista(int idVacacion)
        {
            try
            {
                var vacaciones = db.Vacaciones
                    .Include(v => v.Empleados)
                    .FirstOrDefault(v => v.id_vacacion == idVacacion);

                if (vacaciones != null)
                {
                    ViewBag.DiasDisponibles = vacaciones.dias_disponibles;
                    ViewBag.EmpleadoNombre = $"{vacaciones.Empleados.nombre1} {vacaciones.Empleados.apellido1}";
                    ViewBag.Periodo = vacaciones.periodo;
                }
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
            }
        }

        /// <summary>
        /// Registra detalles de excepción para debugging
        /// </summary>
        private void LogExceptionDetails(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en SolicitudesVacacionesController: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}