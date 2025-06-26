using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using SistemaNomina.Models;

namespace SistemaNomina.Controllers
{
    public class TipoIncapacidadesController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de todos los tipos de incapacidades
        public ActionResult Index()
        {
            return View(db.TipoIncapacidades.ToList());
        }

        // Muestra los detalles de un tipo de incapacidad específico
        public ActionResult Details(int? id)
        {
            // Verifica si el id es nulo, o sea, no se agregó 
            if (id == null)
            {
                // Retorna un error de solicitud incorrecta
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el tipo de incapacidad por su id
            TipoIncapacidades tipoIncapacidades = db.TipoIncapacidades.Find(id);

            // Si no se encuentra, retorna error 
            if (tipoIncapacidades == null)
            {
                return HttpNotFound();
            }

            // Muestra la vista con los datos encontrados
            return View(tipoIncapacidades);
        }

        // Muestra el formulario para crear un nuevo tipo de incapacidad
        public ActionResult Create()
        {
            return View();
        }

        // Recibe los datos del formulario para crear un nuevo tipo de incapacidad
        [HttpPost]
        [ValidateAntiForgeryToken] // Seguridad 
        public ActionResult Create([Bind(Include = "id_tipo,nombre,dias_maximos,pago_planilla,descripcion,fecha_creacion,fecha_actualizacion")] TipoIncapacidades tipoIncapacidades)
        {
            // Verifica que los datos del formulario sean válidos
            if (ModelState.IsValid)
            {
                // Agrega el nuevo registro a la bd
                db.TipoIncapacidades.Add(tipoIncapacidades);
                db.SaveChanges(); // Guarda los cambios
                return RedirectToAction("Index"); // Redirige a la lista
            }

            // Si los datos no son válidos, vuelve a mostrar el formulario
            return View(tipoIncapacidades);
        }

        // Muestra el formulario para editar un tipo de incapacidad existente
        public ActionResult Edit(int? id)
        {
            // Verifica si el id es nulo
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el tipo de incapacidad por su id
            TipoIncapacidades tipoIncapacidades = db.TipoIncapacidades.Find(id);

            // Si no lo encuentra, muestra error
            if (tipoIncapacidades == null)
            {
                return HttpNotFound();
            }

            // Muestra la vista con los datos actuales para editar
            return View(tipoIncapacidades);
        }

        // Recibe los datos editados del formulario
        [HttpPost]
        [ValidateAntiForgeryToken] 
        public ActionResult Edit([Bind(Include = "id_tipo,nombre,dias_maximos,pago_planilla,descripcion,fecha_creacion,fecha_actualizacion")] TipoIncapacidades tipoIncapacidades)
        {
            // Verifica que los datos sean válidos
            if (ModelState.IsValid)
            {
                // Marca el objeto como modificado
                db.Entry(tipoIncapacidades).State = EntityState.Modified;
                db.SaveChanges(); // Guarda los cambios
                return RedirectToAction("Index"); // Redirige a la lista
            }

            // Si hay errores, vuelve a mostrar el formulario
            return View(tipoIncapacidades);
        }

        // Muestra la vista para confirmar la eliminación de un tipo de incapacidad
        public ActionResult Delete(int? id)
        {
            // Verifica si el id es nulo
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el registro por su id
            TipoIncapacidades tipoIncapacidades = db.TipoIncapacidades.Find(id);

            // Si no lo encuentra, muestra error
            if (tipoIncapacidades == null)
            {
                return HttpNotFound();
            }

            // Muestra la vista de confirmación con los datos
            return View(tipoIncapacidades);
        }

        // Elimina el registro de forma definitiva después de confirmar
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            // Busca el registro por su id
            TipoIncapacidades tipoIncapacidades = db.TipoIncapacidades.Find(id);

            // Elimina el registro de la bd
            db.TipoIncapacidades.Remove(tipoIncapacidades);
            db.SaveChanges(); // Guarda los cambios
            return RedirectToAction("Index"); // Redirige a la lista
        }

        // Libera los recursos 
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose(); // Cierra la conexión con la bd
            }
            base.Dispose(disposing); 
        }
    }
}
