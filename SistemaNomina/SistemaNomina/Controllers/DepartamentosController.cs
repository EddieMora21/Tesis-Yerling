using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;
using SistemaNomina.Models;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH", "IT")]
    public class DepartamentosController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de departamentos
        public ActionResult Index()
        {
            try
            {
                var departamentos = db.Departamentos.OrderBy(d => d.nombre).ToList();
                return View(departamentos);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar departamentos.";
                LogExceptionDetails(ex);
                return View(new List<Departamentos>());
            }
        }

        // Muestra los detalles de un departamento específico
        public ActionResult Details(int? id)
        {
            try
            {
                if (id == null)
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

                var departamento = db.Departamentos.Find(id);
                if (departamento == null)
                    return HttpNotFound();

                // Contar puestos y empleados asociados
                ViewBag.CantidadPuestos = db.Puestos.Count(p => p.id_departamento == id);
                ViewBag.CantidadEmpleados = db.Empleados.Count(e => e.Puestos.id_departamento == id);

                return View(departamento);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el departamento.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index");
            }
        }

        // Muestra el formulario para crear un nuevo departamento
        public ActionResult Create()
        {
            // Guardar la URL de retorno si viene de otro lugar
            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];

            var model = new Departamentos
            {
                fecha_creacion = DateTime.Now,
                fecha_actualizacion = DateTime.Now
            };

            return View(model);
        }

        // Guarda el nuevo departamento enviado desde el formulario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "nombre")] Departamentos departamento)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar que no exista un departamento con el mismo nombre
                    var existeDepartamento = db.Departamentos.Any(d => d.nombre.ToLower() == departamento.nombre.ToLower());
                    if (existeDepartamento)
                    {
                        ModelState.AddModelError("nombre", "Ya existe un departamento con este nombre.");
                        ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                        return View(departamento);
                    }

                    // Establecer fechas automáticamente
                    departamento.fecha_creacion = DateTime.Now;
                    departamento.fecha_actualizacion = DateTime.Now;

                    // Limpiar nombre
                    departamento.nombre = departamento.nombre?.Trim();

                    db.Departamentos.Add(departamento);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Departamento creado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("CREAR_DEPARTAMENTO",
                            $"Creado departamento: {departamento.nombre}",
                            currentUserId.Value);
                    }

                    // 🔄 VERIFICAR SI VIENE DE CREAR PUESTO
                    string returnUrl = Request.QueryString["returnUrl"];
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        return Redirect(returnUrl + "?refresh=true");
                    }

                    TempData["Success"] = $"Departamento '{departamento.nombre}' creado exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        ModelState.AddModelError(validationError.PropertyName,
                            $"Error en {validationError.PropertyName}: {validationError.ErrorMessage}");
                    }
                }
            }
            catch (DbUpdateException dbEx)
            {
                HandleDbUpdateException(dbEx);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error inesperado: {ex.Message}");
                LogExceptionDetails(ex);
            }

            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
            return View(departamento);
        }

        // Muestra el formulario para editar un departamento existente
        public ActionResult Edit(int? id)
        {
            try
            {
                if (id == null)
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

                var departamento = db.Departamentos.Find(id);
                if (departamento == null)
                    return HttpNotFound();

                return View(departamento);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el departamento para edición.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index");
            }
        }

        // Guarda los cambios realizados en un departamento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_departamento,nombre,fecha_creacion")] Departamentos departamento)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar que no exista otro departamento con el mismo nombre
                    var existeDepartamento = db.Departamentos.Any(d =>
                        d.nombre.ToLower() == departamento.nombre.ToLower() &&
                        d.id_departamento != departamento.id_departamento);

                    if (existeDepartamento)
                    {
                        ModelState.AddModelError("nombre", "Ya existe otro departamento con este nombre.");
                        return View(departamento);
                    }

                    // Actualizar fecha de modificación
                    departamento.fecha_actualizacion = DateTime.Now;
                    departamento.nombre = departamento.nombre?.Trim();

                    db.Entry(departamento).State = EntityState.Modified;
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Departamento editado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_DEPARTAMENTO",
                            $"Editado departamento: {departamento.nombre} (ID: {departamento.id_departamento})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = $"Departamento '{departamento.nombre}' actualizado exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        ModelState.AddModelError(validationError.PropertyName,
                            $"Error en {validationError.PropertyName}: {validationError.ErrorMessage}");
                    }
                }
            }
            catch (DbUpdateException dbEx)
            {
                HandleDbUpdateException(dbEx);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error inesperado: {ex.Message}");
                LogExceptionDetails(ex);
            }

            return View(departamento);
        }

        // Muestra la confirmación para eliminar un departamento
        public ActionResult Delete(int? id)
        {
            try
            {
                if (id == null)
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

                var departamento = db.Departamentos.Find(id);
                if (departamento == null)
                    return HttpNotFound();

                // Verificar si el departamento está siendo usado
                var puestosAsociados = db.Puestos.Count(p => p.id_departamento == id);
                var empleadosAsociados = db.Empleados.Count(e => e.Puestos.id_departamento == id);

                ViewBag.PuestosAsociados = puestosAsociados;
                ViewBag.EmpleadosAsociados = empleadosAsociados;

                return View(departamento);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el departamento para eliminación.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index");
            }
        }

        // Elimina un departamento después de confirmación
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                var departamento = db.Departamentos.Find(id);
                if (departamento == null)
                    return HttpNotFound();

                // Verificar si el departamento está siendo usado
                var puestosAsociados = db.Puestos.Count(p => p.id_departamento == id);
                var empleadosAsociados = db.Empleados.Count(e => e.Puestos.id_departamento == id);

                if (puestosAsociados > 0 || empleadosAsociados > 0)
                {
                    TempData["Error"] = $"No se puede eliminar el departamento '{departamento.nombre}' porque tiene {puestosAsociados} puesto(s) y {empleadosAsociados} empleado(s) asociado(s).";
                    return RedirectToAction("Index");
                }

                // 📋 LOG AUTOMÁTICO - Departamento eliminado
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("ELIMINAR_DEPARTAMENTO",
                        $"Eliminado departamento: {departamento.nombre} (ID: {departamento.id_departamento})",
                        currentUserId.Value);
                }

                db.Departamentos.Remove(departamento);
                db.SaveChanges();

                TempData["Success"] = $"Departamento '{departamento.nombre}' eliminado exitosamente.";
            }
            catch (DbUpdateException dbEx)
            {
                HandleDbUpdateException(dbEx);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar departamento: {ex.Message}";
                LogExceptionDetails(ex);
            }

            return RedirectToAction("Index");
        }

        // Limpia los recursos cuando el controlador ya no se usa
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }

        #region Métodos auxiliares para manejo de errores

        // Método para mostrar mensajes de error específicos según el tipo de error en la base de datos
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

            LogExceptionDetails(dbEx);
        }

        // Método para registrar detalles del error en el log
        private void LogExceptionDetails(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en DepartamentosController: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        #endregion
    }
}