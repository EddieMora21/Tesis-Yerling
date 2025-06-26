using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using SistemaNomina;
using SistemaNomina.Models;
using SistemaNomina.Helpers;
using SistemaNomina.Filters;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "IT")]
    public class RolesController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Roles
        public ActionResult Index()
        {
            return View(db.Roles.OrderBy(r => r.nombre).ToList());
        }

        // GET: Roles/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Roles roles = db.Roles.Find(id);
            if (roles == null)
                return HttpNotFound();

            return View(roles);
        }

        // GET: Roles/Create
        public ActionResult Create()
        {
            // Guardar la URL de retorno si viene de otro lugar
            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
            return View();
        }

        // POST: Roles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_rol,nombre,descripcion,fecha_creacion,fecha_actualizacion")] Roles roles)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar que no exista un rol con el mismo nombre
                    var existeRol = db.Roles.Any(r => r.nombre.ToLower() == roles.nombre.ToLower());
                    if (existeRol)
                    {
                        ModelState.AddModelError("nombre", "Ya existe un rol con este nombre.");
                        ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
                        return View(roles);
                    }

                    roles.fecha_creacion = DateTime.Now;
                    roles.fecha_actualizacion = DateTime.Now;

                    db.Roles.Add(roles);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Rol creado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("CREAR_ROL",
                            $"Creado rol: {roles.nombre} - {roles.descripcion}",
                            currentUserId.Value);
                    }

                    // 🔄 VERIFICAR SI VIENE DE CREAR USUARIO
                    string returnUrl = Request.QueryString["returnUrl"];
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        // Regresar a crear usuario con parámetro de actualización
                        return Redirect(returnUrl + "?refresh=true");
                    }

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error al crear rol: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error al crear rol: {ex.Message}");
            }

            ViewBag.ReturnUrl = Request.QueryString["returnUrl"];
            return View(roles);
        }

        // GET: Roles/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Roles roles = db.Roles.Find(id);
            if (roles == null)
                return HttpNotFound();

            return View(roles);
        }

        // POST: Roles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_rol,nombre,descripcion,fecha_creacion,fecha_actualizacion")] Roles roles)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar que no exista otro rol con el mismo nombre
                    var existeRol = db.Roles.Any(r => r.nombre.ToLower() == roles.nombre.ToLower() && r.id_rol != roles.id_rol);
                    if (existeRol)
                    {
                        ModelState.AddModelError("nombre", "Ya existe otro rol con este nombre.");
                        return View(roles);
                    }

                    roles.fecha_actualizacion = DateTime.Now;

                    db.Entry(roles).State = EntityState.Modified;
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Rol editado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_ROL",
                            $"Editado rol: {roles.nombre} (ID: {roles.id_rol})",
                            currentUserId.Value);
                    }

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error al actualizar rol: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error al actualizar rol: {ex.Message}");
            }

            return View(roles);
        }

        // GET: Roles/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Roles roles = db.Roles.Find(id);
            if (roles == null)
                return HttpNotFound();

            // Verificar si el rol está siendo usado por algún usuario
            var usuariosConEsteRol = db.Usuarios.Count(u => u.id_rol == id);
            ViewBag.UsuariosAsociados = usuariosConEsteRol;

            return View(roles);
        }

        // POST: Roles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                Roles roles = db.Roles.Find(id);
                if (roles == null)
                    return HttpNotFound();

                // Verificar si el rol está siendo usado
                var usuariosConEsteRol = db.Usuarios.Count(u => u.id_rol == id);
                if (usuariosConEsteRol > 0)
                {
                    TempData["Error"] = $"No se puede eliminar el rol '{roles.nombre}' porque está asignado a {usuariosConEsteRol} usuario(s).";
                    return RedirectToAction("Index");
                }

                // 📋 LOG AUTOMÁTICO - Rol eliminado
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("ELIMINAR_ROL",
                        $"Eliminado rol: {roles.nombre} (ID: {roles.id_rol})",
                        currentUserId.Value);
                }

                db.Roles.Remove(roles);
                db.SaveChanges();

                TempData["Success"] = $"Rol '{roles.nombre}' eliminado exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar rol: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error al eliminar rol: {ex.Message}");
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}