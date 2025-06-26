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
    public class FeriadosController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de feriados
        public ActionResult Index()
        {
            return View(db.Feriados.ToList());
        }

        // Muestra detalles de un feriado específico
        public ActionResult Details(int? id)
        {
            if (id == null) // Si no se recibe un ID válido
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Feriados feriados = db.Feriados.Find(id); // Busca el feriado por ID
            if (feriados == null) // Si no existe, muestra error 
            {
                return HttpNotFound();
            }
            return View(feriados); // Muestra los datos del feriado
        }

        // Muestra el formulario para crear un feriado
        public ActionResult Create()
        {
            return View();
        }

        // Procesa los datos del formulario de creación
        [HttpPost]
        [ValidateAntiForgeryToken] // Seguridad contra ataques 
        public ActionResult Create([Bind(Include = "id_feriado,nombre,fecha,pago_obligatorio,recargo,descripcion,fecha_creacion,fecha_actualizacion")] Feriados feriados)
        {
            if (ModelState.IsValid) // Verifica que los datos sean válidos
            {
                db.Feriados.Add(feriados); // Agrega el feriado a la bd
                db.SaveChanges(); // Guarda los cambios
                return RedirectToAction("Index"); // Vuelve a la lista
            }

            return View(feriados); // Si hay error, vuelve al formulario
        }

        // Muestra el formulario para editar un feriado
        public ActionResult Edit(int? id)
        {
            if (id == null) // Si no hay ID válido
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Feriados feriados = db.Feriados.Find(id); // Busca el feriado
            if (feriados == null) // Si no existe, error 
            {
                return HttpNotFound();
            }
            return View(feriados); // Muestra el formulario con datos
        }

        // Procesa los datos del formulario de edición
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_feriado,nombre,fecha,pago_obligatorio,recargo,descripcion,fecha_creacion,fecha_actualizacion")] Feriados feriados)
        {
            if (ModelState.IsValid) // Si los datos son válidos
            {
                db.Entry(feriados).State = EntityState.Modified; // Marca como modificado
                db.SaveChanges(); // Guarda cambios
                return RedirectToAction("Index"); // Vuelve a la lista
            }
            return View(feriados); // Si hay error, vuelve al formulario
        }

        // Muestra la confirmación para eliminar un feriado
        public ActionResult Delete(int? id)
        {
            if (id == null) // Si no hay ID
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Feriados feriados = db.Feriados.Find(id); // Busca el feriado
            if (feriados == null) // Si no existe, error 
            {
                return HttpNotFound();
            }
            return View(feriados); // Muestra datos para confirmar eliminación
        }

        // Confirma la eliminación de un feriado
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Feriados feriados = db.Feriados.Find(id); // Busca el feriado
            db.Feriados.Remove(feriados); // Lo elimina
            db.SaveChanges(); // Guarda cambios
            return RedirectToAction("Index"); // Vuelve a la lista
        }

        // Libera recursos del sistema
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose(); // Cierra conexión a la bd
            }
            base.Dispose(disposing); // Llama al método base
        }
    }
}
