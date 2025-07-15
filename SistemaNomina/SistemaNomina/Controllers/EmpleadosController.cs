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
                // ✅ VALIDACIÓN DE EDAD - Debe ser mayor de 18 años
                if (empleado.fecha_nacimiento != null)
                {
                    var edad = DateTime.Today.Year - empleado.fecha_nacimiento.Year;
                    if (empleado.fecha_nacimiento.Date > DateTime.Today.AddYears(-edad)) edad--;

                    if (edad < 18)
                    {
                        ModelState.AddModelError("fecha_nacimiento",
                            $"❌ El empleado debe ser mayor de edad. Edad actual: {edad} años. Se requiere mínimo 18 años para poder ser contratado.");
                        CargarListas();
                        ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                        return View(empleado);
                    }
                }

                // ✅ VALIDACIÓN BÁSICA DE CÉDULA (solo que no esté vacía y sea numérica)
                if (string.IsNullOrWhiteSpace(empleado.cedula))
                {
                    ModelState.AddModelError("cedula", "❌ La cédula es obligatoria.");
                    CargarListas();
                    ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                    return View(empleado);
                }

                if (!empleado.cedula.All(char.IsDigit))
                {
                    ModelState.AddModelError("cedula", "❌ La cédula solo puede contener números.");
                    CargarListas();
                    ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                    return View(empleado);
                }

                if (empleado.cedula.Length < 5)
                {
                    ModelState.AddModelError("cedula", "❌ La cédula debe tener al menos 5 dígitos.");
                    CargarListas();
                    ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                    return View(empleado);
                }

                if (empleado.cedula.Length > 20)
                {
                    ModelState.AddModelError("cedula", "❌ La cédula no puede tener más de 20 dígitos.");
                    CargarListas();
                    ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                    return View(empleado);
                }

                // ✅ VALIDACIÓN DE CÉDULA DUPLICADA
                var cedulaExistente = db.Empleados.FirstOrDefault(e => e.cedula == empleado.cedula);
                if (cedulaExistente != null)
                {
                    ModelState.AddModelError("cedula",
                        $"❌ Ya existe un empleado registrado con la cédula {empleado.cedula}. " +
                        $"Empleado existente: {cedulaExistente.nombre1} {cedulaExistente.apellido1}. " +
                        "Cada empleado debe tener una cédula única.");
                    CargarListas();
                    ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                    return View(empleado);
                }

                // ✅ VALIDACIÓN DE PUESTO ÚNICO PARA JEFE DE DEPARTAMENTO
                if (empleado.id_puesto > 0)
                {
                    var puesto = db.Puestos.Find(empleado.id_puesto);
                    if (puesto != null && puesto.es_jefe == true)
                    {
                        // Verificar si ya existe alguien en un puesto de jefatura en el mismo departamento
                        var jefeExistente = db.Empleados
                            .Include(e => e.Puestos)
                            .FirstOrDefault(e => e.Puestos.es_jefe == true &&
                                                e.Puestos.id_departamento == puesto.id_departamento &&
                                                e.estado == "ACTIVO");

                        if (jefeExistente != null)
                        {
                            ModelState.AddModelError("id_puesto",
                                $"❌ Ya existe un jefe activo en este departamento: {jefeExistente.nombre1} {jefeExistente.apellido1} " +
                                $"({jefeExistente.Puestos.nombre_puesto}). Solo puede haber un jefe por departamento. " +
                                "Para asignar este puesto, primero debe cambiar el puesto del jefe actual.");
                            CargarListas();
                            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                            return View(empleado);
                        }
                    }

                    // ✅ VALIDACIÓN ESPECIAL PARA GERENTE GENERAL - SOLO UNO EN TODA LA EMPRESA
                    if (puesto != null && puesto.nombre_puesto.ToUpper().Contains("GERENTE GENERAL"))
                    {
                        var gerenteGeneralExistente = db.Empleados
                            .Include(e => e.Puestos)
                            .FirstOrDefault(e => e.Puestos.nombre_puesto.ToUpper().Contains("GERENTE GENERAL") &&
                                                e.estado == "ACTIVO");

                        if (gerenteGeneralExistente != null)
                        {
                            ModelState.AddModelError("id_puesto",
                                $"❌ Ya existe un Gerente General activo en la empresa: {gerenteGeneralExistente.nombre1} {gerenteGeneralExistente.apellido1}. " +
                                $"Solo puede existir un Gerente General en toda la organización. " +
                                "Para asignar este puesto, primero debe cambiar el puesto del Gerente General actual o inactivar al empleado.");
                            CargarListas();
                            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                            return View(empleado);
                        }
                    }
                }

                // Revisar que todo esté bien, o sea, validaciones del modelo
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
                    if (currentUserId != null)
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

                    // Mostrar mensaje de éxito y volver a la lista
                    TempData["SuccessMessage"] = $"✅ Empleado {empleado.nombre1} {empleado.apellido1} creado exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (DbUpdateException ex)
            {
                // Manejo específico para errores de base de datos
                var innerException = ex.InnerException?.InnerException ?? ex.InnerException ?? ex;
                string errorMessage = "❌ Error al guardar el empleado en la base de datos.";

                if (innerException.Message.Contains("UNIQUE") || innerException.Message.Contains("duplicate"))
                {
                    if (innerException.Message.Contains("cedula"))
                    {
                        errorMessage = "❌ Esta cédula ya está registrada en el sistema. Cada empleado debe tener una cédula única.";
                    }
                    else if (innerException.Message.Contains("correo"))
                    {
                        errorMessage = "❌ Este correo electrónico ya está registrado en el sistema. Cada empleado debe tener un correo único.";
                    }
                    else
                    {
                        errorMessage = "❌ Ya existe un registro con esta información. Verifique que los datos sean únicos.";
                    }
                }
                else if (innerException.Message.Contains("CHECK") || innerException.Message.Contains("constraint"))
                {
                    errorMessage = "❌ Los datos ingresados no cumplen con las reglas del sistema. Verifique la información.";
                }

                ModelState.AddModelError("", errorMessage);
                System.Diagnostics.Debug.WriteLine($"Error al guardar empleado: {innerException.Message}");
            }
            catch (Exception ex)
            {
                // Si pasa otro error inesperado, mostrar mensaje más amigable
                ModelState.AddModelError("", "❌ Ocurrió un error inesperado al crear el empleado. Por favor, inténtelo nuevamente.");
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
                // ✅ VALIDACIÓN DE EDAD - Debe ser mayor de 18 años
                if (empleado.fecha_nacimiento != null)
                {
                    var edad = DateTime.Today.Year - empleado.fecha_nacimiento.Year;
                    if (empleado.fecha_nacimiento.Date > DateTime.Today.AddYears(-edad)) edad--;

                    if (edad < 18)
                    {
                        ModelState.AddModelError("fecha_nacimiento",
                            $"❌ El empleado debe ser mayor de edad. Edad actual: {edad} años. Se requiere mínimo 18 años para poder trabajar.");
                        CargarListas(empleado);
                        return View(empleado);
                    }
                }

                // ✅ VALIDACIÓN BÁSICA DE CÉDULA
                if (string.IsNullOrWhiteSpace(empleado.cedula))
                {
                    ModelState.AddModelError("cedula", "❌ La cédula es obligatoria.");
                    CargarListas(empleado);
                    return View(empleado);
                }

                if (!empleado.cedula.All(char.IsDigit))
                {
                    ModelState.AddModelError("cedula", "❌ La cédula solo puede contener números.");
                    CargarListas(empleado);
                    return View(empleado);
                }

                if (empleado.cedula.Length < 5)
                {
                    ModelState.AddModelError("cedula", "❌ La cédula debe tener al menos 5 dígitos.");
                    CargarListas(empleado);
                    return View(empleado);
                }

                if (empleado.cedula.Length > 20)
                {
                    ModelState.AddModelError("cedula", "❌ La cédula no puede tener más de 20 dígitos.");
                    CargarListas(empleado);
                    return View(empleado);
                }

                // ✅ VALIDACIÓN DE CÉDULA DUPLICADA (excluyendo el empleado actual)
                var cedulaExistente = db.Empleados.FirstOrDefault(e => e.cedula == empleado.cedula && e.id_empleado != empleado.id_empleado);
                if (cedulaExistente != null)
                {
                    ModelState.AddModelError("cedula",
                        $"❌ Ya existe otro empleado registrado con la cédula {empleado.cedula}. " +
                        $"Empleado existente: {cedulaExistente.nombre1} {cedulaExistente.apellido1}. " +
                        "Cada empleado debe tener una cédula única.");
                    CargarListas(empleado);
                    return View(empleado);
                }

                // ✅ VALIDACIÓN DE PUESTO ÚNICO PARA JEFE DE DEPARTAMENTO
                if (empleado.id_puesto > 0)
                {
                    var puesto = db.Puestos.Find(empleado.id_puesto);
                    if (puesto != null && puesto.es_jefe == true)
                    {
                        // Verificar si ya existe alguien en un puesto de jefatura en el mismo departamento (excluyendo el empleado actual)
                        var jefeExistente = db.Empleados
                            .Include(e => e.Puestos)
                            .FirstOrDefault(e => e.Puestos.es_jefe == true &&
                                                e.Puestos.id_departamento == puesto.id_departamento &&
                                                e.estado == "ACTIVO" &&
                                                e.id_empleado != empleado.id_empleado);

                        if (jefeExistente != null)
                        {
                            ModelState.AddModelError("id_puesto",
                                $"❌ Ya existe un jefe activo en este departamento: {jefeExistente.nombre1} {jefeExistente.apellido1} " +
                                $"({jefeExistente.Puestos.nombre_puesto}). Solo puede haber un jefe por departamento. " +
                                "Para asignar este puesto, primero debe cambiar el puesto del jefe actual.");
                            CargarListas(empleado);
                            return View(empleado);
                        }
                    }

                    // ✅ VALIDACIÓN ESPECIAL PARA GERENTE GENERAL - SOLO UNO EN TODA LA EMPRESA (excluyendo el empleado actual)
                    if (puesto != null && puesto.nombre_puesto.ToUpper().Contains("GERENTE GENERAL"))
                    {
                        var gerenteGeneralExistente = db.Empleados
                            .Include(e => e.Puestos)
                            .FirstOrDefault(e => e.Puestos.nombre_puesto.ToUpper().Contains("GERENTE GENERAL") &&
                                                e.estado == "ACTIVO" &&
                                                e.id_empleado != empleado.id_empleado);

                        if (gerenteGeneralExistente != null)
                        {
                            ModelState.AddModelError("id_puesto",
                                $"❌ Ya existe un Gerente General activo en la empresa: {gerenteGeneralExistente.nombre1} {gerenteGeneralExistente.apellido1}. " +
                                $"Solo puede existir un Gerente General en toda la organización. " +
                                "Para asignar este puesto, primero debe cambiar el puesto del Gerente General actual o inactivar al empleado.");
                            CargarListas(empleado);
                            return View(empleado);
                        }
                    }
                }

                // Revisar que todo esté bien, o sea, validaciones del modelo
                if (ModelState.IsValid)
                {
                    // Actualizar la fecha de modificación
                    empleado.fecha_actualizacion = DateTime.Now;

                    // Limpiar campos opcionales si están vacíos
                    empleado.nombre2 = string.IsNullOrWhiteSpace(empleado.nombre2) ? null : empleado.nombre2;
                    empleado.apellido2 = string.IsNullOrWhiteSpace(empleado.apellido2) ? null : empleado.apellido2;
                    empleado.direccion = string.IsNullOrWhiteSpace(empleado.direccion) ? null : empleado.direccion;
                    empleado.correo = string.IsNullOrWhiteSpace(empleado.correo) ? null : empleado.correo;
                    empleado.telefono = string.IsNullOrWhiteSpace(empleado.telefono) ? null : empleado.telefono;

                    // Marcar el empleado como modificado para actualizar en bd
                    db.Entry(empleado).State = EntityState.Modified;

                    // Guardar cambios
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Empleado editado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId != null)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_EMPLEADO",
                            $"Editado empleado: {empleado.nombre1} {empleado.apellido1} (ID: {empleado.id_empleado})",
                            currentUserId.Value);
                    }

                    // Mostrar mensaje de éxito y volver a la lista
                    TempData["SuccessMessage"] = $"✅ Empleado {empleado.nombre1} {empleado.apellido1} actualizado exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                // Si otro usuario ya modificó ese empleado, mostrar mensaje
                ModelState.AddModelError("", "❌ Otro usuario modificó este registro mientras usted lo editaba. Por favor, recargue la página e intente nuevamente.");
            }
            catch (DbUpdateException ex)
            {
                // Manejo específico para errores de base de datos
                var innerException = ex.InnerException?.InnerException ?? ex.InnerException ?? ex;
                string errorMessage = "❌ Error al actualizar el empleado en la base de datos.";

                if (innerException.Message.Contains("UNIQUE") || innerException.Message.Contains("duplicate"))
                {
                    if (innerException.Message.Contains("cedula"))
                    {
                        errorMessage = "❌ Esta cédula ya está registrada por otro empleado. Cada empleado debe tener una cédula única.";
                    }
                    else if (innerException.Message.Contains("correo"))
                    {
                        errorMessage = "❌ Este correo electrónico ya está registrado por otro empleado. Cada empleado debe tener un correo único.";
                    }
                    else
                    {
                        errorMessage = "❌ Ya existe otro registro con esta información. Verifique que los datos sean únicos.";
                    }
                }

                ModelState.AddModelError("", errorMessage);
                System.Diagnostics.Debug.WriteLine($"Error al actualizar empleado: {innerException.Message}");
            }
            catch (Exception ex)
            {
                // Si pasa otro error inesperado, mostrar mensaje más amigable
                ModelState.AddModelError("", "❌ Ocurrió un error inesperado al actualizar el empleado. Por favor, inténtelo nuevamente.");
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
            if (currentUserId != null)
            {
                BitacoraHelper.RegistrarAccion("INACTIVAR_EMPLEADO",
                    $"Empleado inactivado: {empleado.nombre1} {empleado.apellido1} (ID: {empleado.id_empleado})",
                    currentUserId.Value);
            }

            // Mostrar mensaje de éxito y volver a la lista
            TempData["SuccessMessage"] = $"✅ Empleado {empleado.nombre1} {empleado.apellido1} desactivado exitosamente.";
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

        // Método específico para que los empleados vean solo su información personal
       

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
                    var diasDisfrutados = vacaciones.dias_disfrutados;
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