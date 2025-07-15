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
    [RoleAuthorize("Admin", "RRHH", "Supervisor")]
    public class HorasExtrasController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: HorasExtras
        public ActionResult Index()
        {
            var horasExtras = db.HorasExtras
                .Include(h => h.Empleados)
                .Include(h => h.Estados)
                .Include(h => h.Usuarios)
                .Include(h => h.TiposHoraExtra)
                .OrderByDescending(h => h.fecha_creacion)
                .ToList();

            return View(horasExtras);
        }

        // GET: HorasExtras/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            HorasExtras horasExtras = db.HorasExtras
                .Include(h => h.Empleados)
                .Include(h => h.Estados)
                .Include(h => h.Usuarios)
                .Include(h => h.Usuarios.Empleados)
                .Include(h => h.TiposHoraExtra)
                .FirstOrDefault(h => h.id_hora_extra == id);

            if (horasExtras == null)
                return HttpNotFound();

            return View(horasExtras);
        }

        // GET: HorasExtras/Create
        public ActionResult Create()
        {
            var model = new HorasExtras();
            CargarListasDesplegables();
            return View(model);
        }

        // POST: HorasExtras/Create
        // POST: HorasExtras/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_empleado,id_tipo,fecha,hora_inicio,hora_fin,motivo")] HorasExtras horasExtras)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔍 VALIDACIONES DE NEGOCIO
                    if (!ValidarHorasExtras(horasExtras))
                    {
                        CargarListasDesplegables(horasExtras);
                        return View(horasExtras);
                    }

                    // 📊 CÁLCULOS AUTOMÁTICOS
                    CalcularHorasYMonto(horasExtras);

                    // 📅 Fechas automáticas
                    horasExtras.fecha_creacion = DateTime.Now;
                    horasExtras.fecha_actualizacion = DateTime.Now;

                    // 📝 Estado inicial: Pendiente (ID 10 para HorasExtras)
                    horasExtras.id_estado = 10; // ✅ Usar el ID correcto que vimos en la imagen

                    // 🚫 ASEGURAR QUE ESTOS CAMPOS ESTÉN EN NULL PARA CREACIÓN
                    horasExtras.aprobado_por = null;
                    horasExtras.fecha_aprobacion = null;

                    db.HorasExtras.Add(horasExtras);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId!= null)
                    {
                        var empleado = db.Empleados.Find(horasExtras.id_empleado);
                        BitacoraHelper.RegistrarAccion("CREAR_HORAS_EXTRAS",
                            $"Registradas {horasExtras.horas:F2} horas extras para {empleado?.nombre1} {empleado?.apellido1} el {horasExtras.fecha:dd/MM/yyyy}",
                            currentUserId.Value);
                    }

                    TempData["Success"] = $"Horas extras registradas exitosamente. Total calculado: ₡{horasExtras.total:N2}";
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

            CargarListasDesplegables(horasExtras);
            return View(horasExtras);
        }
        // GET: HorasExtras/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            HorasExtras horasExtras = db.HorasExtras
                .Include(h => h.Empleados)
                .Include(h => h.Estados)
                .Include(h => h.TiposHoraExtra)
                .FirstOrDefault(h => h.id_hora_extra == id);

            if (horasExtras == null)
                return HttpNotFound();

            // 🔒 VALIDACIÓN ESPECÍFICA CON LOS IDs EXACTOS
            if (horasExtras.id_estado != 10) // ID 10 = Pendiente para HorasExtras
            {
                TempData["Error"] = $"Solo se pueden editar horas extras en estado Pendiente. Estado actual: {horasExtras.Estados?.nombre}";
                return RedirectToAction("Index");
            }

            CargarListasDesplegables(horasExtras);
            return View(horasExtras);
        }

        // POST: HorasExtras/Edit/5 - VERSIÓN CORREGIDA SIN TOTAL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id)
        {
            try
            {
                // 📊 VERIFICAR QUE EL REGISTRO EXISTE Y ESTÁ EN ESTADO PENDIENTE
                var registroActual = db.Database.SqlQuery<dynamic>(
                    "SELECT id_hora_extra, id_estado, fecha_creacion FROM HorasExtras WHERE id_hora_extra = @p0 AND id_estado = 10",
                    id).FirstOrDefault();

                if (registroActual == null)
                {
                    TempData["Error"] = "Registro no encontrado o ya no está en estado Pendiente.";
                    return RedirectToAction("Index");
                }

                // 🔄 OBTENER VALORES DEL FORMULARIO
                if (!int.TryParse(Request.Form["id_empleado"], out int idEmpleado))
                {
                    ModelState.AddModelError("id_empleado", "Empleado requerido.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                if (!int.TryParse(Request.Form["id_tipo"], out int idTipo))
                {
                    ModelState.AddModelError("id_tipo", "Tipo de hora extra requerido.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                if (!DateTime.TryParse(Request.Form["fecha"], out DateTime fecha))
                {
                    ModelState.AddModelError("fecha", "Fecha requerida.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                if (!TimeSpan.TryParse(Request.Form["hora_inicio"], out TimeSpan horaInicio))
                {
                    ModelState.AddModelError("hora_inicio", "Hora de inicio requerida.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                if (!TimeSpan.TryParse(Request.Form["hora_fin"], out TimeSpan horaFin))
                {
                    ModelState.AddModelError("hora_fin", "Hora de fin requerida.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                string motivo = Request.Form["motivo"] ?? "";

                // 🔍 VALIDACIONES BÁSICAS (igual que antes)
                if (horaInicio >= horaFin)
                {
                    ModelState.AddModelError("hora_fin", "La hora de fin debe ser posterior a la hora de inicio.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                if (fecha > DateTime.Today)
                {
                    ModelState.AddModelError("fecha", "La fecha no puede ser futura.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                if (fecha < DateTime.Today.AddDays(-30))
                {
                    ModelState.AddModelError("fecha", "No se pueden registrar horas extras de más de 30 días de antigüedad.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                var duracion = horaFin - horaInicio;
                if (duracion.TotalHours > 12)
                {
                    ModelState.AddModelError("hora_fin", "No se pueden registrar más de 12 horas extras por día.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                // Validar duplicados
                var yaExiste = db.HorasExtras.Any(h => h.id_empleado == idEmpleado
                    && h.fecha == fecha
                    && h.id_hora_extra != id);

                if (yaExiste)
                {
                    ModelState.AddModelError("fecha", "Ya existe un registro de horas extras para este empleado en esta fecha.");
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }

                // 📊 CALCULAR HORAS Y MONTOS
                decimal horas = (decimal)duracion.TotalHours;
                decimal valorHora = 0;
                decimal recargo = 50; // Default

                // Obtener salario del empleado
                var empleado = db.Empleados.Include(e => e.Puestos)
                    .FirstOrDefault(e => e.id_empleado == idEmpleado);

                if (empleado?.Puestos != null)
                {
                    valorHora = empleado.Puestos.salario_base / 30 / 8;

                    var tipoHoraExtra = db.TiposHoraExtra.Find(idTipo);
                    if (tipoHoraExtra != null)
                    {
                        recargo = tipoHoraExtra.recargo == 0 ? 50m : tipoHoraExtra.recargo;
                    }
                }

                // 🔧 ACTUALIZACIÓN CON SQL DIRECTO - SIN MODIFICAR "total"
                var sqlUpdate = @"
            UPDATE HorasExtras 
            SET id_empleado = @p0,
                id_tipo = @p1,
                fecha = @p2,
                hora_inicio = @p3,
                hora_fin = @p4,
                horas = @p5,
                valor_hora = @p6,
                recargo = @p7,
                motivo = @p8,
                fecha_actualizacion = @p9
            WHERE id_hora_extra = @p10 AND id_estado = 10";

                var parametros = new object[] {
            idEmpleado,         // @p0
            idTipo,             // @p1
            fecha,              // @p2
            horaInicio,         // @p3
            horaFin,            // @p4
            horas,              // @p5
            valorHora,          // @p6
            recargo,            // @p7
            motivo,             // @p8
            DateTime.Now,       // @p9
            id                  // @p10
        };

                var filasAfectadas = db.Database.ExecuteSqlCommand(sqlUpdate, parametros);

                if (filasAfectadas > 0)
                {
                    // 📋 LOG AUTOMÁTICO
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId!= null)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_HORAS_EXTRAS",
                            $"Editadas horas extras para {empleado?.nombre1} {empleado?.apellido1} (ID: {id})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = "Horas extras actualizadas exitosamente.";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["Error"] = "No se pudo actualizar el registro. Verifique que aún esté en estado Pendiente.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al actualizar: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error en Edit: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                // En caso de error, mostrar el formulario nuevamente
                try
                {
                    var errorModel = db.HorasExtras.Find(id);
                    CargarListasDesplegables(errorModel);
                    return View(errorModel);
                }
                catch
                {
                    return RedirectToAction("Index");
                }
            }
        }

        // GET: HorasExtras/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            HorasExtras horasExtras = db.HorasExtras
                .Include(h => h.Empleados)
                .Include(h => h.Estados)
                .Include(h => h.TiposHoraExtra)
                .Include(h => h.Usuarios)
                .Include(h => h.Usuarios.Empleados)
                .FirstOrDefault(h => h.id_hora_extra == id);

            if (horasExtras == null)
                return HttpNotFound();

            // 🔍 VALIDACIÓN: Verificar si ya fue aprobada (ID 11 = Aprobado)
            ViewBag.EsAprobada = horasExtras.id_estado == 11;

            return View(horasExtras);
        }

        // POST: HorasExtras/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                HorasExtras horasExtras = db.HorasExtras.Find(id);
                if (horasExtras == null)
                    return HttpNotFound();

                // 🛡️ VALIDACIÓN: No eliminar si ya fue aprobada (ID 11 = Aprobado)
                if (horasExtras.id_estado == 11)
                {
                    TempData["Error"] = "No se pueden eliminar horas extras ya aprobadas.";
                    return RedirectToAction("Index");
                }

                // 📋 LOG AUTOMÁTICO
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId!= null)
                {
                    var empleado = db.Empleados.Find(horasExtras.id_empleado);
                    BitacoraHelper.RegistrarAccion("ELIMINAR_HORAS_EXTRAS",
                        $"Eliminadas horas extras de {empleado?.nombre1} {empleado?.apellido1} del {horasExtras.fecha:dd/MM/yyyy} (ID: {horasExtras.id_hora_extra})",
                        currentUserId.Value);
                }

                db.HorasExtras.Remove(horasExtras);
                db.SaveChanges();

                TempData["Success"] = "Horas extras eliminadas exitosamente.";
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
                TempData["Error"] = "Error al eliminar las horas extras.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error inesperado al eliminar: {ex.Message}";
                LogExceptionDetails(ex);
            }

            return RedirectToAction("Index");
        }

        // 🎯 MÉTODO PARA APROBAR HORAS EXTRAS
        [HttpPost]
        public ActionResult Aprobar(int id)
        {
            try
            {
                var horasExtras = db.HorasExtras.Find(id);
                if (horasExtras == null)
                    return Json(new { success = false, message = "Registro no encontrado." });

                // Cambiar de estado Pendiente (10) a Aprobado (11)
                horasExtras.id_estado = 11; // ID 11 = Aprobado
                horasExtras.aprobado_por = (int?)Session["UserId"];
                horasExtras.fecha_aprobacion = DateTime.Now;
                horasExtras.fecha_actualizacion = DateTime.Now;

                db.SaveChanges();

                // 📋 LOG
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId!= null)
                {
                    var empleado = db.Empleados.Find(horasExtras.id_empleado);
                    BitacoraHelper.RegistrarAccion("APROBAR_HORAS_EXTRAS",
                        $"Aprobadas {horasExtras.horas:F2} horas extras de {empleado?.nombre1} {empleado?.apellido1}",
                        currentUserId.Value);
                }

                return Json(new { success = true, message = "Horas extras aprobadas exitosamente." });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error al aprobar las horas extras." });
            }
        }

        // 🎯 MÉTODO PARA RECHAZAR HORAS EXTRAS
        [HttpPost]
        public ActionResult Rechazar(int id)
        {
            try
            {
                var horasExtras = db.HorasExtras.Find(id);
                if (horasExtras == null)
                    return Json(new { success = false, message = "Registro no encontrado." });

                // Cambiar de estado Pendiente (10) a Rechazado (12)
                horasExtras.id_estado = 12; // ID 12 = Rechazado
                horasExtras.aprobado_por = (int?)Session["UserId"];
                horasExtras.fecha_aprobacion = DateTime.Now;
                horasExtras.fecha_actualizacion = DateTime.Now;

                db.SaveChanges();

                // 📋 LOG
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId!= null)
                {
                    var empleado = db.Empleados.Find(horasExtras.id_empleado);
                    BitacoraHelper.RegistrarAccion("RECHAZAR_HORAS_EXTRAS",
                        $"Rechazadas horas extras de {empleado?.nombre1} {empleado?.apellido1}",
                        currentUserId.Value);
                }

                return Json(new { success = true, message = "Horas extras rechazadas." });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error al rechazar las horas extras." });
            }
        }

        #region Métodos auxiliares

        private void CargarListasDesplegables(HorasExtras horasExtras = null)
        {
            ViewBag.id_empleado = new SelectList(
                db.Empleados.Where(e => e.estado == "Activo")
                    .Select(e => new {
                        e.id_empleado,
                        NombreCompleto = e.nombre1 + " " + e.apellido1 + " - " + e.cedula
                    })
                    .OrderBy(e => e.NombreCompleto),
                "id_empleado", "NombreCompleto", horasExtras?.id_empleado);

            // 🔧 SOLO CARGAR ESTADOS DE HORASEXTRAS
            ViewBag.id_estado = new SelectList(
                db.Estados.Where(e => e.modulo == "HorasExtras"),
                "id_estado", "nombre", horasExtras?.id_estado);

            ViewBag.aprobado_por = new SelectList(
                db.Usuarios.Include(u => u.Empleados)
                    .Where(u => u.Empleados != null)
                    .Select(u => new {
                        u.id_usuario,
                        NombreCompleto = u.Empleados.nombre1 + " " + u.Empleados.apellido1
                    })
                    .OrderBy(x => x.NombreCompleto),
                "id_usuario", "NombreCompleto", horasExtras?.aprobado_por);

            ViewBag.id_tipo = new SelectList(
                db.TiposHoraExtra.OrderBy(t => t.nombre),
                "id_tipo", "nombre", horasExtras?.id_tipo);
        }

        private bool ValidarHorasExtras(HorasExtras horasExtras)
        {
            bool esValido = true;

            // 🔍 Validar fecha no sea futura
            if (horasExtras.fecha > DateTime.Today)
            {
                ModelState.AddModelError("fecha", "La fecha no puede ser futura.");
                esValido = false;
            }

            // 🔍 Validar fecha no sea muy antigua (máximo 30 días)
            if (horasExtras.fecha < DateTime.Today.AddDays(-30))
            {
                ModelState.AddModelError("fecha", "No se pueden registrar horas extras de más de 30 días de antigüedad.");
                esValido = false;
            }

            // 🔍 Validar hora inicio < hora fin
            if (horasExtras.hora_inicio >= horasExtras.hora_fin)
            {
                ModelState.AddModelError("hora_fin", "La hora de fin debe ser posterior a la hora de inicio.");
                esValido = false;
            }

            // 🔍 Validar que no exceda 12 horas por día
            var duracion = horasExtras.hora_fin - horasExtras.hora_inicio;
            if (duracion.TotalHours > 12)
            {
                ModelState.AddModelError("hora_fin", "No se pueden registrar más de 12 horas extras por día.");
                esValido = false;
            }

            // 🔍 Validar duplicados en la misma fecha
            var yaExiste = db.HorasExtras.Any(h => h.id_empleado == horasExtras.id_empleado
                && h.fecha == horasExtras.fecha
                && h.id_hora_extra != horasExtras.id_hora_extra);

            if (yaExiste)
            {
                ModelState.AddModelError("fecha", "Ya existe un registro de horas extras para este empleado en esta fecha.");
                esValido = false;
            }

            return esValido;
        }

        private void CalcularHorasYMonto(HorasExtras horasExtras)
        {
            // 📊 Calcular horas trabajadas
            var duracion = horasExtras.hora_fin - horasExtras.hora_inicio;
            horasExtras.horas = (decimal)duracion.TotalHours;

            // 📊 Obtener salario del empleado
            var empleado = db.Empleados.Include(e => e.Puestos).FirstOrDefault(e => e.id_empleado == horasExtras.id_empleado);
            if (empleado?.Puestos != null)
            {
                // 💰 Calcular valor hora ordinaria: Salario Mensual / 30 / 8
                var valorHoraOrdinaria = empleado.Puestos.salario_base / 30 / 8;
                horasExtras.valor_hora = valorHoraOrdinaria;

                // 📈 Aplicar recargo según tipo
                var tipoHoraExtra = db.TiposHoraExtra.Find(horasExtras.id_tipo);
                if (tipoHoraExtra != null)
                {
                    // Solo aplicar un valor por defecto si recargo == 0 (por precaución)
                    horasExtras.recargo = tipoHoraExtra.recargo == 0 ? 50m : tipoHoraExtra.recargo;

                    // 💵 Calcular total: (Valor Hora * (1 + Recargo/100)) * Horas
                    var factorRecargo = 1 + (horasExtras.recargo / 100);
                    horasExtras.total = horasExtras.valor_hora * factorRecargo * horasExtras.horas;
                }
            }
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
            System.Diagnostics.Debug.WriteLine($"Error en HorasExtrasController: {ex.Message}");
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