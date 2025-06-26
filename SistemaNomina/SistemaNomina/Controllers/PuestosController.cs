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
    public class PuestosController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra todos los puestos
        public ActionResult Index()
        {
            try
            {
                // Incluye información de departamentos y horarios asociados a cada puesto
                var puestos = db.Puestos.Include(p => p.Departamentos)
                                       .Include(p => p.Horarios)
                                       .OrderBy(p => p.nombre_puesto)
                                       .ToList();
                return View(puestos);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar puestos.";
                LogExceptionDetails(ex);
                return View(new List<Puestos>());
            }
        }

        // Muestra los detalles de un puesto específico
        public ActionResult Details(int? id)
        {
            try
            {
                if (id == null)
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

                Puestos puestos = db.Puestos.Find(id);
                if (puestos == null)
                    return HttpNotFound();

                // Contar empleados asociados a este puesto
                ViewBag.CantidadEmpleados = db.Empleados.Count(e => e.id_puesto == id);

                return View(puestos);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el puesto.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index");
            }
        }

        // Muestra el formulario para crear un nuevo puesto
        public ActionResult Create()
        {
            // Guardar la URL de retorno si viene de otro lugar
            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];

            var model = new Puestos
            {
                fecha_creacion = DateTime.Now,
                fecha_actualizacion = DateTime.Now
            };

            CargarListas();
            return View(model);
        }

        // Procesa el formulario de creación
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "nombre_puesto,id_departamento,salario_base,descripcion,horas_jornada,es_jefe,id_horario")] Puestos puestos)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar que no exista un puesto con el mismo nombre en el mismo departamento
                    var existePuesto = db.Puestos.Any(p => p.nombre_puesto.ToLower() == puestos.nombre_puesto.ToLower()
                                                        && p.id_departamento == puestos.id_departamento);
                    if (existePuesto)
                    {
                        ModelState.AddModelError("nombre_puesto", "Ya existe un puesto con este nombre en el departamento seleccionado.");
                        CargarListas(puestos);
                        ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                        return View(puestos);
                    }

                    // Asigna fecha de creación y actualización automática
                    puestos.fecha_creacion = DateTime.Now;
                    puestos.fecha_actualizacion = DateTime.Now;

                    // Limpiar campos
                    puestos.nombre_puesto = puestos.nombre_puesto?.Trim();
                    puestos.descripcion = puestos.descripcion?.Trim();

                    db.Puestos.Add(puestos);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Puesto creado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        var departamento = db.Departamentos.Find(puestos.id_departamento);
                        BitacoraHelper.RegistrarAccion("CREAR_PUESTO",
                            $"Creado puesto: {puestos.nombre_puesto} (Departamento: {departamento?.nombre})",
                            currentUserId.Value);
                    }

                    // 🔄 VERIFICAR SI VIENE DE CREAR EMPLEADO
                    string returnUrl = Request.QueryString["returnUrl"];
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        return Redirect(returnUrl + "?refresh=true");
                    }

                    TempData["Success"] = $"Puesto '{puestos.nombre_puesto}' creado exitosamente.";
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

            // Si algo falla, se vuelve a cargar el formulario con los valores
            CargarListas(puestos);
            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
            return View(puestos);
        }

        // Muestra el formulario para editar un puesto existente
        public ActionResult Edit(int? id)
        {
            try
            {
                if (id == null)
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

                Puestos puestos = db.Puestos.Find(id);
                if (puestos == null)
                    return HttpNotFound();

                CargarListas(puestos);
                return View(puestos);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el puesto para edición.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index");
            }
        }

        // Procesa el formulario de edición
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_puesto,nombre_puesto,id_departamento,salario_base,descripcion,horas_jornada,es_jefe,id_horario,fecha_creacion")] Puestos puestos)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar que no exista otro puesto con el mismo nombre en el mismo departamento
                    var existePuesto = db.Puestos.Any(p => p.nombre_puesto.ToLower() == puestos.nombre_puesto.ToLower()
                                                        && p.id_departamento == puestos.id_departamento
                                                        && p.id_puesto != puestos.id_puesto);
                    if (existePuesto)
                    {
                        ModelState.AddModelError("nombre_puesto", "Ya existe otro puesto con este nombre en el departamento seleccionado.");
                        CargarListas(puestos);
                        return View(puestos);
                    }

                    // Actualiza la fecha de modificación
                    puestos.fecha_actualizacion = DateTime.Now;
                    puestos.nombre_puesto = puestos.nombre_puesto?.Trim();
                    puestos.descripcion = puestos.descripcion?.Trim();

                    db.Entry(puestos).State = EntityState.Modified;
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Puesto editado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_PUESTO",
                            $"Editado puesto: {puestos.nombre_puesto} (ID: {puestos.id_puesto})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = $"Puesto '{puestos.nombre_puesto}' actualizado exitosamente.";
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

            // Si hay errores, se vuelve a cargar el formulario
            CargarListas(puestos);
            return View(puestos);
        }

        // Muestra la confirmación para eliminar un puesto
        public ActionResult Delete(int? id)
        {
            try
            {
                if (id == null)
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

                Puestos puestos = db.Puestos.Find(id);
                if (puestos == null)
                    return HttpNotFound();

                // 🔍 VALIDACIÓN DE NEGOCIO - Verificar si hay empleados asociados
                var empleadosAsociados = db.Empleados.Count(e => e.id_puesto == id);
                ViewBag.EmpleadosAsociados = empleadosAsociados;

                return View(puestos);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el puesto para eliminación.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index");
            }
        }

        // Procesa la eliminación del puesto
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                Puestos puestos = db.Puestos.Find(id);
                if (puestos == null)
                    return HttpNotFound();

                // 🛡️ VALIDACIÓN DE NEGOCIO - No eliminar si hay empleados asociados
                var empleadosAsociados = db.Empleados.Count(e => e.id_puesto == id);
                if (empleadosAsociados > 0)
                {
                    TempData["Error"] = $"No se puede eliminar el puesto '{puestos.nombre_puesto}' porque está asignado a {empleadosAsociados} empleado(s).";
                    return RedirectToAction("Index");
                }

                // 📋 LOG AUTOMÁTICO - Puesto eliminado
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("ELIMINAR_PUESTO",
                        $"Eliminado puesto: {puestos.nombre_puesto} (ID: {puestos.id_puesto})",
                        currentUserId.Value);
                }

                db.Puestos.Remove(puestos);
                db.SaveChanges();

                TempData["Success"] = $"Puesto '{puestos.nombre_puesto}' eliminado exitosamente.";
            }
            catch (DbUpdateException dbEx)
            {
                HandleDbUpdateException(dbEx);
                TempData["Error"] = "Error al eliminar el puesto. Verifique que no esté siendo utilizado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error inesperado al eliminar: {ex.Message}";
                LogExceptionDetails(ex);
            }

            return RedirectToAction("Index");
        }

        // Libera los recursos cuando el controlador ya no se usa
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }

        #region Métodos auxiliares

        // Método para cargar las listas desplegables
        private void CargarListas(Puestos puesto = null)
        {
            ViewBag.id_departamento = new SelectList(db.Departamentos.OrderBy(d => d.nombre),
                                                    "id_departamento", "nombre",
                                                    puesto?.id_departamento);

            ViewBag.id_horario = new SelectList(db.Horarios.OrderBy(h => h.nombre),
                                               "id_horario", "nombre",
                                               puesto?.id_horario);
        }

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
            System.Diagnostics.Debug.WriteLine($"Error en PuestosController: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        #endregion
    }
}