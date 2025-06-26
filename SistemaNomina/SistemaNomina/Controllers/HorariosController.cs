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
    public class HorariosController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Mostrar la lista de horarios
        public ActionResult Index()
        {
            var horarios = db.Horarios.OrderBy(h => h.nombre).ToList();
            return View(horarios);
        }

        // Mostrar detalles de un horario específico
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Horarios horario = db.Horarios.Find(id);
            if (horario == null)
                return HttpNotFound();

            return View(horario);
        }

        // Mostrar formulario para crear nuevo horario
        public ActionResult Create()
        {
            var model = new Horarios();
            return View(model);
        }

        // Guardar nuevo horario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "nombre,hora_entrada,hora_salida")] Horarios horario)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔍 VALIDACIÓN DE NEGOCIO - Horario único
                    var existeHorario = db.Horarios.Any(h => h.nombre.ToLower() == horario.nombre.ToLower());
                    if (existeHorario)
                    {
                        ModelState.AddModelError("nombre", "Ya existe un horario con este nombre.");
                        return View(horario);
                    }

                    // 🔍 VALIDACIÓN DE NEGOCIO - Hora entrada < hora salida
                    if (horario.hora_entrada >= horario.hora_salida)
                    {
                        ModelState.AddModelError("hora_salida", "La hora de salida debe ser posterior a la hora de entrada.");
                        return View(horario);
                    }

                    // 📅 Fechas automáticas
                    horario.fecha_creacion = DateTime.Now;
                    horario.fecha_actualizacion = DateTime.Now;

                    db.Horarios.Add(horario);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Horario creado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("CREAR_HORARIO",
                            $"Creado horario: {horario.nombre} ({horario.hora_entrada:HH\\:mm} - {horario.hora_salida:HH\\:mm})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = $"Horario '{horario.nombre}' creado exitosamente.";
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

            return View(horario);
        }

        // Mostrar formulario para editar horario
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Horarios horario = db.Horarios.Find(id);
            if (horario == null)
                return HttpNotFound();

            return View(horario);
        }

        // Guardar cambios en horario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_horario,nombre,hora_entrada,hora_salida,fecha_creacion")] Horarios horario)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔍 VALIDACIÓN DE NEGOCIO - Horario único (excluyendo el actual)
                    var existeHorario = db.Horarios.Any(h => h.nombre.ToLower() == horario.nombre.ToLower()
                                                          && h.id_horario != horario.id_horario);
                    if (existeHorario)
                    {
                        ModelState.AddModelError("nombre", "Ya existe otro horario con este nombre.");
                        return View(horario);
                    }

                    // 🔍 VALIDACIÓN DE NEGOCIO - Hora entrada < hora salida
                    if (horario.hora_entrada >= horario.hora_salida)
                    {
                        ModelState.AddModelError("hora_salida", "La hora de salida debe ser posterior a la hora de entrada.");
                        return View(horario);
                    }

                    // 📅 Actualizar fecha de modificación
                    horario.fecha_actualizacion = DateTime.Now;

                    db.Entry(horario).State = EntityState.Modified;
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Horario editado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_HORARIO",
                            $"Editado horario: {horario.nombre} ({horario.hora_entrada:HH\\:mm} - {horario.hora_salida:HH\\:mm}) (ID: {horario.id_horario})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = $"Horario '{horario.nombre}' actualizado exitosamente.";
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

            return View(horario);
        }

        // Mostrar confirmación para eliminar horario
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Horarios horario = db.Horarios.Find(id);
            if (horario == null)
                return HttpNotFound();

            // 🔍 VALIDACIÓN DE NEGOCIO - Verificar si hay empleados asociados
            var empleadosAsociados = db.Empleados.Count(e => e.id_horario == id);
            ViewBag.EmpleadosAsociados = empleadosAsociados;

            // 🔍 VALIDACIÓN DE NEGOCIO - Verificar si hay puestos asociados
            var puestosAsociados = db.Puestos.Count(p => p.id_horario == id);
            ViewBag.PuestosAsociados = puestosAsociados;

            return View(horario);
        }

        // Eliminar horario (con validaciones de negocio)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                Horarios horario = db.Horarios.Find(id);
                if (horario == null)
                    return HttpNotFound();

                // 🛡️ VALIDACIÓN DE NEGOCIO - No eliminar si hay empleados asociados
                var empleadosAsociados = db.Empleados.Count(e => e.id_horario == id);
                if (empleadosAsociados > 0)
                {
                    TempData["Error"] = $"No se puede eliminar el horario '{horario.nombre}' porque está asignado a {empleadosAsociados} empleado(s).";
                    return RedirectToAction("Index");
                }

                // 🛡️ VALIDACIÓN DE NEGOCIO - No eliminar si hay puestos asociados
                var puestosAsociados = db.Puestos.Count(p => p.id_horario == id);
                if (puestosAsociados > 0)
                {
                    TempData["Error"] = $"No se puede eliminar el horario '{horario.nombre}' porque está asignado a {puestosAsociados} puesto(s).";
                    return RedirectToAction("Index");
                }

                // 📋 LOG AUTOMÁTICO - Horario eliminado
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("ELIMINAR_HORARIO",
                        $"Eliminado horario: {horario.nombre} ({horario.hora_entrada:HH\\:mm} - {horario.hora_salida:HH\\:mm}) (ID: {horario.id_horario})",
                        currentUserId.Value);
                }

                db.Horarios.Remove(horario);
                db.SaveChanges();

                TempData["Success"] = $"Horario '{horario.nombre}' eliminado exitosamente.";
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
                TempData["Error"] = "Error al eliminar el horario. Verifique que no esté siendo utilizado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error inesperado al eliminar: {ex.Message}";
                LogExceptionDetails(ex);
            }

            return RedirectToAction("Index");
        }

        // Limpiar recursos
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }

        #region Métodos auxiliares para manejo de errores

        // Método para manejar errores específicos de base de datos
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
                    ModelState.AddModelError("", "Ya existe un registro con estos valores (violación de restricción única).");
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

        // Método para registrar detalles del error
        private void LogExceptionDetails(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en HorariosController: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        #endregion
    }
}