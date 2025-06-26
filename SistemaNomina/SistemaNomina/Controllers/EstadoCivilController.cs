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
    public class EstadoCivilController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Mostrar la lista de estados civiles
        public ActionResult Index()
        {
            var estadosCiviles = db.EstadoCivil.OrderBy(e => e.nombre).ToList();
            return View(estadosCiviles);
        }

        // Mostrar detalles de un estado civil específico
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            EstadoCivil estadoCivil = db.EstadoCivil.Find(id);
            if (estadoCivil == null)
                return HttpNotFound();

            return View(estadoCivil);
        }

        // Mostrar formulario para crear nuevo estado civil
        public ActionResult Create()
        {
            var model = new EstadoCivil();
            return View(model);
        }

        // Guardar nuevo estado civil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "nombre")] EstadoCivil estadoCivil)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar que no exista otro estado civil con el mismo nombre
                    var existeEstado = db.EstadoCivil.Any(e => e.nombre.ToLower() == estadoCivil.nombre.ToLower());
                    if (existeEstado)
                    {
                        ModelState.AddModelError("nombre", "Ya existe un estado civil con este nombre.");
                        return View(estadoCivil);
                    }

                    // Asignar fechas automáticamente
                    estadoCivil.fecha_creacion = DateTime.Now;
                    estadoCivil.fecha_actualizacion = DateTime.Now;

                    db.EstadoCivil.Add(estadoCivil);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Estado civil creado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("CREAR_ESTADO_CIVIL",
                            $"Creado estado civil: {estadoCivil.nombre}",
                            currentUserId.Value);
                    }

                    TempData["Success"] = $"Estado civil '{estadoCivil.nombre}' creado exitosamente.";
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

            return View(estadoCivil);
        }

        // Mostrar formulario para editar estado civil
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            EstadoCivil estadoCivil = db.EstadoCivil.Find(id);
            if (estadoCivil == null)
                return HttpNotFound();

            return View(estadoCivil);
        }

        // Guardar cambios en estado civil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_estado_civil,nombre,fecha_creacion")] EstadoCivil estadoCivil)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar que no exista otro estado civil con el mismo nombre
                    var existeEstado = db.EstadoCivil.Any(e => e.nombre.ToLower() == estadoCivil.nombre.ToLower()
                                                            && e.id_estado_civil != estadoCivil.id_estado_civil);
                    if (existeEstado)
                    {
                        ModelState.AddModelError("nombre", "Ya existe otro estado civil con este nombre.");
                        return View(estadoCivil);
                    }

                    // Actualizar fecha de modificación
                    estadoCivil.fecha_actualizacion = DateTime.Now;

                    db.Entry(estadoCivil).State = EntityState.Modified;
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Estado civil editado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_ESTADO_CIVIL",
                            $"Editado estado civil: {estadoCivil.nombre} (ID: {estadoCivil.id_estado_civil})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = $"Estado civil '{estadoCivil.nombre}' actualizado exitosamente.";
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

            return View(estadoCivil);
        }

        // Mostrar confirmación para eliminar estado civil
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            EstadoCivil estadoCivil = db.EstadoCivil.Find(id);
            if (estadoCivil == null)
                return HttpNotFound();

            // 🔍 VALIDACIÓN DE NEGOCIO - Verificar si hay empleados asociados
            var empleadosAsociados = db.Empleados.Count(e => e.id_estado_civil == id);
            ViewBag.EmpleadosAsociados = empleadosAsociados;

            return View(estadoCivil);
        }

        // Eliminar estado civil (lógica de validación)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                EstadoCivil estadoCivil = db.EstadoCivil.Find(id);
                if (estadoCivil == null)
                    return HttpNotFound();

                // 🛡️ VALIDACIÓN DE NEGOCIO - No eliminar si hay empleados asociados
                var empleadosAsociados = db.Empleados.Count(e => e.id_estado_civil == id);
                if (empleadosAsociados > 0)
                {
                    TempData["Error"] = $"No se puede eliminar el estado civil '{estadoCivil.nombre}' porque está asignado a {empleadosAsociados} empleado(s).";
                    return RedirectToAction("Index");
                }

                // 📋 LOG AUTOMÁTICO - Estado civil eliminado
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("ELIMINAR_ESTADO_CIVIL",
                        $"Eliminado estado civil: {estadoCivil.nombre} (ID: {estadoCivil.id_estado_civil})",
                        currentUserId.Value);
                }

                db.EstadoCivil.Remove(estadoCivil);
                db.SaveChanges();

                TempData["Success"] = $"Estado civil '{estadoCivil.nombre}' eliminado exitosamente.";
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
                TempData["Error"] = "Error al eliminar el estado civil. Verifique que no esté siendo utilizado.";
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
            System.Diagnostics.Debug.WriteLine($"Error en EstadoCivilController: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        #endregion
    }
}