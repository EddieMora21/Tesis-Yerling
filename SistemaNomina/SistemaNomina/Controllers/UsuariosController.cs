using System;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using SistemaNomina;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;
using SistemaNomina.Models;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH", "Supervisor", "Empleado", "IT")]
    public class UsuariosController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Usuarios/Login
        [AllowAnonymous]
        public ActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public ActionResult Login(string usuario, string contrasena)
        {
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena))
            {
                ViewBag.Error = "Debe ingresar el usuario y la contraseña.";
                return View();
            }

            var usuarioDB = db.Usuarios
                .Include(u => u.Roles)
                .Include(u => u.Empleados)
                .FirstOrDefault(u => u.usuario != null && u.usuario.ToLower() == usuario.ToLower());

            if (usuarioDB != null)
            {
                // 🔐 NUEVA VALIDACIÓN CON ENCRIPTACIÓN
                bool passwordValida = false;

                // Verificar si la contraseña está encriptada o en texto plano (migración gradual)
                if (usuarioDB.contrasena.Length == 64) // SHA-256 produce 64 caracteres
                {
                    passwordValida = SecurityHelper.VerifyPassword(contrasena, usuarioDB.contrasena);
                }
                else
                {
                    // Compatibilidad con contraseñas en texto plano (temporal)
                    passwordValida = usuarioDB.contrasena == contrasena;

                    // Si es válida, encriptar y guardar
                    if (passwordValida)
                    {
                        usuarioDB.contrasena = SecurityHelper.EncryptPassword(contrasena);
                        usuarioDB.fecha_actualizacion = DateTime.Now;
                        db.Entry(usuarioDB).State = EntityState.Modified;
                        db.SaveChanges();
                    }
                }

                if (passwordValida)
                {
                    // Guardar información en sesión
                    Session["Usuario"] = usuarioDB.usuario;
                    Session["RolUsuario"] = usuarioDB.Roles?.nombre ?? "Sin Rol";
                    Session["UserId"] = usuarioDB.id_usuario;

                    FormsAuthentication.SetAuthCookie(usuarioDB.usuario, false);

                    // 📋 LOG AUTOMÁTICO - Login exitoso
                    BitacoraHelper.RegistrarLogin(usuarioDB.id_usuario, usuarioDB.usuario);

                    if (usuarioDB.primer_ingreso == true)
                    {
                        return RedirectToAction("CambiarContrasena", new { id = usuarioDB.id_usuario });
                    }

                    switch (usuarioDB.Roles?.nombre?.ToLower())
                    {
                        case "admin":
                        case "rrhh":
                        case "supervisor":
                        case "empleado":
                        default:
                            return RedirectToAction("Index", "Home");
                    }
                }
                else
                {
                    // 📋 LOG AUTOMÁTICO - Login fallido
                    BitacoraHelper.RegistrarLoginFallido(usuario);
                    ViewBag.Error = "Contraseña incorrecta.";
                }
            }
            else
            {
                // 📋 LOG AUTOMÁTICO - Usuario no existe
                BitacoraHelper.RegistrarLoginFallido(usuario);
                ViewBag.Error = "El usuario no existe.";
            }

            return View();
        }

        // GET: Usuarios
        [RoleAuthorize("Admin")]
        public ActionResult Index()
        {
            var usuarios = db.Usuarios
                .Include(u => u.Empleados)
                .Include(u => u.Roles)
                .ToList();

            return View(usuarios);
        }

        // GET: Usuarios/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var currentUserId = (int?)Session["UserId"];
            var currentUserRole = Session["RolUsuario"] as string;

            // Validar acceso
            if (currentUserRole != "Admin" && id != currentUserId)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            Usuarios usuario = db.Usuarios
                .Include(u => u.Empleados)
                .Include(u => u.Roles)
                .FirstOrDefault(u => u.id_usuario == id);

            if (usuario == null)
            {
                return HttpNotFound();
            }
            return View(usuario);
        }

        // GET: Usuarios/Create
        [RoleAuthorize("Admin")]
        public ActionResult Create()
        {
            var roles = db.Roles.ToList();
            var currentUserRole = Session["RolUsuario"] as string;

            if (currentUserRole != "Admin")
            {
                roles = roles.Where(r => !r.nombre.Equals("Admin", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula");
            ViewBag.id_rol = new SelectList(roles, "id_rol", "nombre");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin")]
        public ActionResult Create([Bind(Include = "id_usuario, id_empleado,usuario,contrasena,id_rol,primer_ingreso,fecha_creacion,fecha_actualizacion")] Usuarios usuarios)
        {
            if (ModelState.IsValid)
            {
                var currentUserRole = Session["RolUsuario"] as string;

                if (currentUserRole != "Admin")
                {
                    var rol = db.Roles.Find(usuarios.id_rol);
                    if (rol != null && rol.nombre.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("id_rol", "No tiene permisos para asignar este rol");
                        ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", usuarios.id_empleado);
                        ViewBag.id_rol = new SelectList(db.Roles, "id_rol", "nombre", usuarios.id_rol);
                        return View(usuarios);
                    }
                }

                // 🔐 ENCRIPTAR CONTRASEÑA
                usuarios.contrasena = SecurityHelper.EncryptPassword(usuarios.contrasena);
                usuarios.fecha_creacion = DateTime.Now;
                usuarios.fecha_actualizacion = DateTime.Now;
                usuarios.primer_ingreso = true;

                db.Usuarios.Add(usuarios);
                db.SaveChanges();

                // 📋 LOG AUTOMÁTICO - Usuario creado
                var currentUserId = (int?)Session["UserId"];
                BitacoraHelper.RegistrarCreacionUsuario(currentUserId.Value, usuarios.usuario);

                return RedirectToAction("Index");
            }

            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", usuarios.id_empleado);
            ViewBag.id_rol = new SelectList(db.Roles, "id_rol", "nombre", usuarios.id_rol);
            return View(usuarios);
        }

        // GET: Usuarios/Edit/5
        [RoleAuthorize("Admin")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Usuarios usuario = db.Usuarios.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }

            var currentUserRole = Session["RolUsuario"] as string;
            var roles = db.Roles.ToList();

            if (currentUserRole != "Admin")
            {
                roles = roles.Where(r => !r.nombre.Equals("Admin", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", usuario.id_empleado);
            ViewBag.id_rol = new SelectList(roles, "id_rol", "nombre", usuario.id_rol);
            return View(usuario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin")]
        public ActionResult Edit([Bind(Include = "id_usuario,id_empleado,usuario,contrasena,id_rol,primer_ingreso,fecha_creacion,fecha_actualizacion")] Usuarios usuarios)
        {
            if (ModelState.IsValid)
            {
                var currentUserRole = Session["RolUsuario"] as string;

                if (currentUserRole != "Admin")
                {
                    var rol = db.Roles.Find(usuarios.id_rol);
                    if (rol != null && rol.nombre.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("id_rol", "No tiene permisos para asignar este rol");
                        ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", usuarios.id_empleado);
                        ViewBag.id_rol = new SelectList(db.Roles, "id_rol", "nombre", usuarios.id_rol);
                        return View(usuarios);
                    }
                }

                // 🔐 Si se cambió la contraseña, encriptarla
                var usuarioOriginal = db.Usuarios.AsNoTracking().FirstOrDefault(u => u.id_usuario == usuarios.id_usuario);
                if (usuarioOriginal != null && usuarioOriginal.contrasena != usuarios.contrasena)
                {
                    usuarios.contrasena = SecurityHelper.EncryptPassword(usuarios.contrasena);
                }

                usuarios.fecha_actualizacion = DateTime.Now;
                db.Entry(usuarios).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", usuarios.id_empleado);
            ViewBag.id_rol = new SelectList(db.Roles, "id_rol", "nombre", usuarios.id_rol);
            return View(usuarios);
        }

        // GET: Usuarios/Delete/5
        [RoleAuthorize("Admin")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Usuarios usuario = db.Usuarios
                .Include(u => u.Empleados)
                .Include(u => u.Roles)
                .FirstOrDefault(u => u.id_usuario == id);

            if (usuario == null)
            {
                return HttpNotFound();
            }

            return View(usuario);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin")]
        public ActionResult DeleteConfirmed(int id)
        {
            Usuarios usuario = db.Usuarios.Find(id);

            // 📋 LOG AUTOMÁTICO - Usuario eliminado
            var currentUserId = (int?)Session["UserId"];
            BitacoraHelper.RegistrarEliminacionUsuario(currentUserId.Value, usuario.usuario);

            db.Usuarios.Remove(usuario);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        [AllowAnonymous]
        public ActionResult Logout()
        {
            // 📋 LOG AUTOMÁTICO - Logout
            var currentUserId = (int?)Session["UserId"];
            var currentUsername = Session["Usuario"] as string;

            if (currentUserId.HasValue && !string.IsNullOrEmpty(currentUsername))
            {
                BitacoraHelper.RegistrarLogout(currentUserId.Value, currentUsername);
            }

            FormsAuthentication.SignOut();
            Session.Clear();
            return RedirectToAction("Login");
        }

        [Authorize]
        public ActionResult CambiarContrasena(int id)
        {
            var currentUserId = (int?)Session["UserId"];
            var currentUserRole = Session["RolUsuario"] as string;

            if (id != currentUserId && currentUserRole != "Admin" && currentUserRole != "IT")
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var usuario = db.Usuarios.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }

            ViewBag.id_usuario = usuario.id_usuario;
            ViewBag.usuario = usuario.usuario;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult CambiarContrasena(int id, string nuevaContrasena, string confirmarContrasena)
        {
            var currentUserId = (int?)Session["UserId"];
            var currentUserRole = Session["RolUsuario"] as string;

            if (id != currentUserId && currentUserRole != "Admin" && currentUserRole != "IT")
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            if (nuevaContrasena != confirmarContrasena)
            {
                ViewBag.Error = "Las contraseñas no coinciden.";
                ViewBag.id_usuario = id;
                return View();
            }

            // 🔐 VALIDAR POLÍTICA DE CONTRASEÑAS
            if (!SecurityHelper.IsValidPassword(nuevaContrasena))
            {
                ViewBag.Error = "La contraseña debe tener al menos 6 caracteres, incluyendo letras y números.";
                ViewBag.id_usuario = id;
                return View();
            }

            var usuario = db.Usuarios.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }

            // 🔐 ENCRIPTAR NUEVA CONTRASEÑA
            usuario.contrasena = SecurityHelper.EncryptPassword(nuevaContrasena);
            usuario.primer_ingreso = false;
            usuario.fecha_actualizacion = DateTime.Now;

            db.Entry(usuario).State = EntityState.Modified;
            db.SaveChanges();

            // 📋 LOG AUTOMÁTICO - Cambio de contraseña
            BitacoraHelper.RegistrarCambioContrasena(usuario.id_usuario, usuario.usuario);

            if (id == currentUserId)
            {
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}