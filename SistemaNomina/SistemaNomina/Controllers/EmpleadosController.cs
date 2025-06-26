using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using SistemaNomina.Models;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH", "IT")]
    public class EmpleadosController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Mostrar la lista de empleados
        public ActionResult Index()
        {
            // Traer empleados con info de estado civil, horario y puesto
            var empleados = db.Empleados.Include(e => e.EstadoCivil)
                                       .Include(e => e.Horarios)
                                       .Include(e => e.Puestos)
                                       .OrderBy(e => e.apellido1)
                                       .ThenBy(e => e.apellido2);

            // Enviar la lista a la vista para mostrar
            return View(empleados.ToList());
        }

        // Mostrar datos de un empleado específico
        public ActionResult Details(int? id)
        {
            // Si no hay id, mostrar error
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            // Buscar empleado por id
            Empleados empleado = db.Empleados.Find(id);

            // Si no lo encuentra, mostrar error
            if (empleado == null) return HttpNotFound();

            // Mostrar los datos en la vista
            return View(empleado);
        }

        // Mostrar formulario para agregar empleado nuevo
        public ActionResult Create()
        {
            CargarListas(); // Traer datos para las listas desplegables 

            // Crear nuevo empleado con datos por defecto
            var model = new Empleados
            {
                fecha_creacion = DateTime.Now,
                fecha_actualizacion = DateTime.Now,
                estado = "ACTIVO",
                fecha_ingreso = DateTime.Today,
                cantidad_hijos = 0
            };

            // Guardar la URL de retorno si viene de otro lugar
            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];

            return View(model);
        }

        // Guardar empleado nuevo enviado desde el formulario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "cedula,nombre1,nombre2,apellido1,apellido2,fecha_nacimiento,direccion,correo,telefono,id_estado_civil,cantidad_hijos,id_puesto,id_horario,fecha_ingreso,estado")] Empleados empleado)
        {
            try
            {
                // Revisar que todo esté bien, o sea, validaciones
                if (ModelState.IsValid)
                {
                    // Poner fechas actuales
                    empleado.fecha_creacion = DateTime.Now;
                    empleado.fecha_actualizacion = DateTime.Now;

                    // Si estos campos están vacíos, ponerles NULL para no guardar cadena vacía
                    empleado.nombre2 = string.IsNullOrWhiteSpace(empleado.nombre2) ? null : empleado.nombre2;
                    empleado.apellido2 = string.IsNullOrWhiteSpace(empleado.apellido2) ? null : empleado.apellido2;
                    empleado.direccion = string.IsNullOrWhiteSpace(empleado.direccion) ? null : empleado.direccion;
                    empleado.correo = string.IsNullOrWhiteSpace(empleado.correo) ? null : empleado.correo;
                    empleado.telefono = string.IsNullOrWhiteSpace(empleado.telefono) ? null : empleado.telefono;

                    // Agregar el empleado a la bd
                    db.Empleados.Add(empleado);

                    // Guardar los cambios en la bd
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Empleado creado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("CREAR_EMPLEADO",
                            $"Creado empleado: {empleado.nombre1} {empleado.apellido1} (Cédula: {empleado.cedula})",
                            currentUserId.Value);
                    }

                    // 🔄 VERIFICAR SI VIENE DE CREAR USUARIO
                    string returnUrl = Request.QueryString["returnUrl"];
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        // Regresar a crear usuario con parámetro de actualización
                        return Redirect(returnUrl + "?refresh=true");
                    }

                    // Volver a la lista de empleados
                    return RedirectToAction("Index");
                }
            }
            catch (DbUpdateException ex)
            {
                // Si hay error guardando, mostrar mensaje con la causa
                var innerException = ex.InnerException?.InnerException ?? ex.InnerException ?? ex;
                ModelState.AddModelError("", $"Error al guardar: {innerException.Message}");
                System.Diagnostics.Debug.WriteLine($"Error al guardar empleado: {innerException.Message}");
            }
            catch (Exception ex)
            {
                // Si pasa otro error inesperado, mostrar mensaje
                ModelState.AddModelError("", $"Error inesperado: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error inesperado: {ex.Message}");
            }

            // Si hay error, volver a cargar listas y mostrar formulario con datos
            CargarListas();
            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
            return View(empleado);
        }

        // Mostrar formulario para editar empleado
        public ActionResult Edit(int? id)
        {
            // Si no hay id, mostrar error
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            // Buscar empleado por id
            Empleados empleado = db.Empleados.Find(id);

            // Si no lo encuentra, mostrar error
            if (empleado == null) return HttpNotFound();

            // Cargar listas para las opciones del formulario y seleccionar el valor actual
            CargarListas(empleado);
            return View(empleado);
        }

        // Guardar cambios hechos en el empleado editado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_empleado,cedula,nombre1,nombre2,apellido1,apellido2,fecha_nacimiento,direccion,correo,telefono,id_estado_civil,cantidad_hijos,id_puesto,id_horario,fecha_ingreso,estado,fecha_creacion")] Empleados empleado)
        {
            try
            {
                // Revisar que todo esté bien, o sea, validaciones
                if (ModelState.IsValid)
                {
                    // Actualizar la fecha de modificación
                    empleado.fecha_actualizacion = DateTime.Now;

                    // Marcar el empleado como modificado para actualizar en bd
                    db.Entry(empleado).State = EntityState.Modified;

                    // Guardar cambios
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Empleado editado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_EMPLEADO",
                            $"Editado empleado: {empleado.nombre1} {empleado.apellido1} (ID: {empleado.id_empleado})",
                            currentUserId.Value);
                    }

                    // Volver a la lista de empleados
                    return RedirectToAction("Index");
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                // Si otro usuario ya modificó ese empleado, mostrar mensaje
                ModelState.AddModelError("", "Otro usuario modificó este registro. Recarga la página.");
            }
            catch (DbUpdateException ex)
            {
                // Si hay error guardando, mostrar mensaje con la causa
                var innerException = ex.InnerException?.InnerException ?? ex.InnerException ?? ex;
                ModelState.AddModelError("", $"Error al guardar: {innerException.Message}");
                System.Diagnostics.Debug.WriteLine($"Error al actualizar empleado: {innerException.Message}");
            }
            catch (Exception ex)
            {
                // Si pasa otro error inesperado, mostrar mensaje
                ModelState.AddModelError("", $"Error inesperado: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error inesperado: {ex.Message}");
            }

            // Si hay error, recargar listas y mostrar formulario con datos actuales
            CargarListas(empleado);
            return View(empleado);
        }

        // Mostrar pantalla para confirmar eliminar empleado
        public ActionResult Delete(int? id)
        {
            // Si no hay id, mostrar error
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            // Buscar empleado por id
            Empleados empleado = db.Empleados.Find(id);

            // Si no lo encuentra, mostrar error
            if (empleado == null) return HttpNotFound();

            // Mostrar confirmación para eliminar
            return View(empleado);
        }

        // Cambiar estado del empleado a INACTIVO en vez de borrar la fila
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            // Buscar empleado por id
            Empleados empleado = db.Empleados.Find(id);
            if (empleado == null) return HttpNotFound();

            // Cambiar estado para no eliminar realmente
            empleado.estado = "INACTIVO";
            empleado.fecha_actualizacion = DateTime.Now;

            // Marcar como modificado y guardar
            db.Entry(empleado).State = EntityState.Modified;
            db.SaveChanges();

            // 📋 LOG AUTOMÁTICO - Empleado inactivado
            var currentUserId = (int?)Session["UserId"];
            if (currentUserId.HasValue)
            {
                BitacoraHelper.RegistrarAccion("INACTIVAR_EMPLEADO",
                    $"Empleado inactivado: {empleado.nombre1} {empleado.apellido1} (ID: {empleado.id_empleado})",
                    currentUserId.Value);
            }

            // Volver a la lista de empleados
            return RedirectToAction("Index");
        }

        // Limpiar memoria cuando ya no se use el controlador
        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // Método que carga datos para listas desplegables en los formularios
        private void CargarListas(Empleados empleado = null)
        {
            // Lista para estado civil, ordenada por id
            ViewBag.id_estado_civil = new SelectList(db.EstadoCivil.OrderBy(e => e.id_estado_civil),
                                                     "id_estado_civil", "nombre",
                                                     empleado?.id_estado_civil);

            // Lista para horarios
            ViewBag.id_horario = new SelectList(db.Horarios.OrderBy(h => h.id_horario),
                                               "id_horario", "nombre",
                                               empleado?.id_horario);

            // Lista para puestos
            ViewBag.id_puesto = new SelectList(db.Puestos.OrderBy(p => p.id_puesto),
                                              "id_puesto", "nombre_puesto",
                                              empleado?.id_puesto);

            // Lista para estado ACTIVO o INACTIVO
            ViewBag.estado = new SelectList(new[] { "ACTIVO", "INACTIVO" },
                                           empleado?.estado ?? "ACTIVO");
        }

        public ActionResult DetalleVacaciones(int id_empleado)
        {
            try
            {
                using (var db = new smartbuilding_rhEntities())
                {
                    var empleado = db.Empleados.Include("Vacaciones")
                                 .FirstOrDefault(e => e.id_empleado == id_empleado);

                    if (empleado == null)
                    {
                        return HttpNotFound("Empleado no encontrado.");
                    }

                    // Vacaciones más recientes
                    var vacaciones = empleado.Vacaciones
                                      .OrderByDescending(v => v.periodo)
                                      .FirstOrDefault();

                    // Si no hay registro de vacaciones, crearlo automáticamente
                    if (vacaciones == null)
                    {
                        var fechaIngreso = empleado.fecha_ingreso;
                        var semanasTrabajadas = (DateTime.Now - fechaIngreso).TotalDays / 7;
                        var diasAcumuladosPorLey = (int)(semanasTrabajadas / 50 * 12); // cada 50 semanas = 12 días

                        vacaciones = new Vacaciones
                        {
                            id_empleado = empleado.id_empleado,
                            periodo = DateTime.Now.Year.ToString(),
                            dias_disponibles = diasAcumuladosPorLey,
                            dias_disfrutados = 0
                        };

                        db.Vacaciones.Add(vacaciones);
                        db.SaveChanges();
                    }

                    // Cálculo según el Código de Trabajo
                    var fechaIngresoEmpleado = empleado.fecha_ingreso;
                    var semanasTrabajadasEmpleado = (DateTime.Now - fechaIngresoEmpleado).TotalDays / 7;
                    var diasAcumuladosPorLeyEmpleado = (int)(semanasTrabajadasEmpleado / 50 * 12); // cada 50 semanas = 12 días

                    // Días restantes según registro
                    var diasDisfrutados = vacaciones.dias_disfrutados ?? 0;
                    var saldoDisponible = vacaciones.dias_disponibles - diasDisfrutados;

                    ViewBag.NombreEmpleado = $"{empleado.nombre1} {empleado.apellido1}";
                    ViewBag.FechaIngreso = empleado.fecha_ingreso.ToShortDateString();
                    ViewBag.Periodo = vacaciones.periodo;
                    ViewBag.DiasAcumuladosPorLey = diasAcumuladosPorLeyEmpleado;
                    ViewBag.SaldoActual = saldoDisponible;

                    return View("DetalleVacaciones");
                }
            }
            catch (Exception ex)
            {
                // Registrar el error para depuración
                System.Diagnostics.Debug.WriteLine($"Error en DetalleVacaciones: {ex.Message}");

                // Mostrar un mensaje de error genérico al usuario
                ViewBag.Error = "Ocurrió un error al procesar la solicitud. Por favor, inténtelo de nuevo más tarde.";
                return View("Error");
            }
        }
    }
}