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
    public class NominasController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de todas las nóminas registradas
        public ActionResult Index()
        {
            var nomina = db.Nomina.Include(n => n.Empleados).Include(n => n.ISR1);
            return View(nomina.ToList());
        }



        // Muestra los detalles de una nómina en sí
        public ActionResult Details(int? id)
        {
            // Verifica si no se recibió un ID
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca la nómina con el ID indicado
            Nomina nomina = db.Nomina.Find(id);

            // Si no se encuentra, muestra error
            if (nomina == null)
            {
                return HttpNotFound();
            }

            // Muestra la información de la nómina
            return View(nomina);
        }

        // Muestra el formulario para crear una nueva nómina
        public ActionResult Create()
        {
            // Llena de empleados
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula");

            // Llena de tipos de ISR
            ViewBag.id_isr = new SelectList(db.ISR, "id_isr", "descripcion");

            return View();
        }

        // Procesa los datos del formulario para crear una nueva nómina
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_nomina,id_empleado,id_isr,mes,anio,salario_bruto,horas_extras,salario_dias_feriados,ccss,ivm,isr,credito_hijos,credito_conyuge,otros_descuentos,base_imponible,salario_neto,fecha_creacion,fecha_actualizacion")] Nomina nomina)
        {
            // Verifica si los datos son válidos
            if (ModelState.IsValid)
            {
                // Agrega la nueva nómina a la bd
                db.Nomina.Add(nomina);
                db.SaveChanges();

                // Redirige a la lista de nóminas
                return RedirectToAction("Index");
            }

            // Si hubo error, vuelve a llenar los combos y muestra el formulario
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", nomina.id_empleado);
            ViewBag.id_isr = new SelectList(db.ISR, "id_isr", "descripcion", nomina.id_isr);
            return View(nomina);
        }

        // Muestra el formulario para editar una nómina existente
        public ActionResult Edit(int? id)
        {
            // Verifica si no se recibió un ID
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca la nómina con el ID indicado
            Nomina nomina = db.Nomina.Find(id);

            // Si no se encuentra, muestra error
            if (nomina == null)
            {
                return HttpNotFound();
            }

            // Llena los combos con la información actual
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", nomina.id_empleado);
            ViewBag.id_isr = new SelectList(db.ISR, "id_isr", "descripcion", nomina.id_isr);
            return View(nomina);
        }

        // Procesa los datos del formulario para guardar los cambios en la nómina
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_nomina,id_empleado,id_isr,mes,anio,salario_bruto,horas_extras,salario_dias_feriados,ccss,ivm,isr,credito_hijos,credito_conyuge,otros_descuentos,base_imponible,salario_neto,fecha_creacion,fecha_actualizacion")] Nomina nomina)
        {
            // Verifica si los datos son válidos
            if (ModelState.IsValid)
            {
                // Marca la nómina como modificada
                db.Entry(nomina).State = EntityState.Modified;
                db.SaveChanges();

                // Redirige a la lista de nóminas
                return RedirectToAction("Index");
            }

            // Si hubo error, vuelve a llenar los combos y muestra el formulario
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", nomina.id_empleado);
            ViewBag.id_isr = new SelectList(db.ISR, "id_isr", "descripcion", nomina.id_isr);
            return View(nomina);
        }

        // Muestra la confirmación para eliminar una nómina
        public ActionResult Delete(int? id)
        {
            // Verifica si no se recibió un ID
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca la nómina con el ID indicado
            Nomina nomina = db.Nomina.Find(id);

            // Si no se encuentra, muestra error
            if (nomina == null)
            {
                return HttpNotFound();
            }

            // Muestra la confirmación para eliminar
            return View(nomina);
        }

        // Procesa la eliminación de la nómina
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            // Busca y elimina la nómina
            Nomina nomina = db.Nomina.Find(id);
            db.Nomina.Remove(nomina);
            db.SaveChanges();

            // Redirige a la lista de nóminas
            return RedirectToAction("Index");
        }

        // Libera los recursos 
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        // AGREGAR ESTAS ACCIONES AL NominasController.cs EXISTENTE
        // (después de las acciones que ya tienes)

        // ============================================
        // NUEVAS ACCIONES PARA CASOS DE USO COMPLETOS
        // ============================================

        /// <summary>
        /// CASO DE USO: Generar Nómina - Paso 1
        /// Permite seleccionar el período y empleados para calcular
        /// </summary>
        public ActionResult SeleccionarPeriodo()
        {
            ViewBag.Empleados = db.Empleados.Where(e => e.estado == "Activo").ToList();
            ViewBag.Meses = new SelectList(new[]
            {
        new { Value = 1, Text = "Enero" },
        new { Value = 2, Text = "Febrero" },
        new { Value = 3, Text = "Marzo" },
        new { Value = 4, Text = "Abril" },
        new { Value = 5, Text = "Mayo" },
        new { Value = 6, Text = "Junio" },
        new { Value = 7, Text = "Julio" },
        new { Value = 8, Text = "Agosto" },
        new { Value = 9, Text = "Septiembre" },
        new { Value = 10, Text = "Octubre" },
        new { Value = 11, Text = "Noviembre" },
        new { Value = 12, Text = "Diciembre" }
    }, "Value", "Text");

            return View();
        }

        /// <summary>
        /// CASO DE USO: Generar Nómina - Paso 2  
        /// Procesa la selección y calcula la nómina automáticamente
        /// </summary>
        [HttpPost]
        // REEMPLAZAR el método CalcularNomina en NominasController.cs

        
        public ActionResult CalcularNomina(int mes, int anio, List<int> empleadosSeleccionados)
        {
            // Crear una lista estructurada en lugar de objetos dinámicos
            var resultados = new List<CalculoNominaViewModel>();

            foreach (int idEmpleado in empleadosSeleccionados)
            {
                var empleado = db.Empleados.Find(idEmpleado);
                if (empleado == null) continue;

                // 1. CALCULAR SALARIO BASE
                decimal salarioBase = empleado.Puestos.salario_base;

                // 2. CALCULAR HORAS EXTRAS DEL MES
                var horasExtras = db.HorasExtras
                    .Where(he => he.id_empleado == idEmpleado &&
                                he.fecha.Month == mes &&
                                he.fecha.Year == anio &&
                                he.Estados.nombre == "Aprobado")
                    .Sum(he => he.total) ?? 0;

                // 3. CALCULAR DÍAS FERIADOS TRABAJADOS
                var diasFeriados = CalcularPagoFeriados(idEmpleado, mes, anio);

                // 4. CALCULAR SALARIO BRUTO
                decimal salarioBruto = salarioBase + horasExtras + diasFeriados;

                // 5. APLICAR DEDUCCIONES LEGALES
                var deducciones = CalcularDeduccionesLegales(salarioBruto, empleado.cantidad_hijos ?? 0, anio);

                // 6. CALCULAR SALARIO NETO
                decimal salarioNeto = salarioBruto - deducciones.CCSS - deducciones.IVM - deducciones.ISR;

                // Crear ViewModel estructurado
                var resultado = new CalculoNominaViewModel
                {
                    IdEmpleado = empleado.id_empleado,
                    NombreCompleto = $"{empleado.nombre1} {empleado.apellido1}",
                    Cedula = empleado.cedula,
                    SalarioBase = salarioBase,
                    HorasExtras = horasExtras,
                    DiasFeriados = diasFeriados,
                    SalarioBruto = salarioBruto,
                    CCSS = deducciones.CCSS,
                    IVM = deducciones.IVM,
                    ISR = deducciones.ISR,
                    SalarioNeto = salarioNeto,
                    Mes = mes,
                    Anio = anio
                };

                resultados.Add(resultado);
            }

            // Pasar lista estructurada en lugar de ViewBag
            var viewModel = new VistaPreviaViewModel
            {
                Resultados = resultados,
                Mes = mes,
                Anio = anio
            };

            return View("VistaPrevia", viewModel);
        }

        /// <summary>
        /// CASO DE USO: Aplicar deducciones legales
        /// Calcula CCSS, IVM e ISR según normativa costarricense
        /// </summary>
        // REEMPLAZAR este método en NominasController.cs
        private dynamic CalcularDeduccionesLegales(decimal salarioBruto, int cantidadHijos, int anio)
        {
            // CCSS: 9.34%
            decimal ccss = salarioBruto * 0.0934m;

            // IVM: 2.75%  
            decimal ivm = salarioBruto * 0.0275m;

            // ISR: Según tabla progresiva
            decimal isr = CalcularISR(salarioBruto, cantidadHijos, anio);

            return new
            {
                CCSS = Math.Round(ccss, 2),
                IVM = Math.Round(ivm, 2),
                ISR = Math.Round(isr, 2)
            };
        }

        /// <summary>
        /// Calcula el Impuesto Sobre la Renta según tabla del MTSS
        /// </summary>
        private decimal CalcularISR(decimal salarioBruto, int cantidadHijos, int anio)
        {
            var tablaISR = db.ISR.Where(i => i.anio == anio).OrderBy(i => i.limite_inferior).ToList();

            if (!tablaISR.Any()) return 0;

            decimal isrCalculado = 0;
            decimal salarioParaISR = salarioBruto;

            foreach (var tramo in tablaISR)
            {
                if (salarioParaISR <= tramo.limite_inferior) break;

                decimal salarioEnTramo = Math.Min(salarioParaISR, tramo.limite_superior) - tramo.limite_inferior;
                if (salarioEnTramo > 0)
                {
                    isrCalculado += (salarioEnTramo * tramo.porcentaje / 100) + tramo.exceso;
                }
            }

            // Aplicar créditos por hijos
            if (tablaISR.Any() && cantidadHijos > 0)
            {
                decimal creditoHijos = (tablaISR.First().credito_hijo ?? 0) * cantidadHijos;
                isrCalculado = Math.Max(0, isrCalculado - creditoHijos);
            }

            return isrCalculado;
        }

        /// <summary>
        /// Calcula el pago adicional por días feriados trabajados
        /// </summary>
        private decimal CalcularPagoFeriados(int idEmpleado, int mes, int anio)
        {
            var feriadosDelMes = db.Feriados
                .Where(f => f.fecha.Month == mes && f.fecha.Year == anio)
                .ToList();

            decimal totalFeriados = 0;

            foreach (var feriado in feriadosDelMes)
            {
                var asistencia = db.Asistencia
                    .FirstOrDefault(a => a.id_empleado == idEmpleado &&
                                       a.fecha.Date == feriado.fecha.Date &&
                                       a.hora_entrada.HasValue && a.hora_salida.HasValue);

                if (asistencia != null && feriado.pago_obligatorio == true)
                {
                    var empleado = db.Empleados.Find(idEmpleado);
                    decimal salarioDiario = empleado.Puestos.salario_base / 30;

                    // Pago normal + recargo por feriado
                    totalFeriados += salarioDiario * (1 + (feriado.recargo ?? 0) / 100);
                }
            }

            return totalFeriados;
        }

        /// <summary>
        /// Guarda la nómina calculada en la base de datos
        /// </summary>
        [HttpPost]
        public ActionResult GuardarNomina(FormCollection form)
        {
            try
            {
                var nominasGuardadas = 0;

                // Procesar cada empleado del formulario
                for (int i = 0; i < Request.Form.AllKeys.Count(k => k.StartsWith("empleado_")); i++)
                {
                    var nomina = new Nomina
                    {
                        id_empleado = int.Parse(form[$"empleado_{i}"]),
                        mes = int.Parse(form["mes"]),
                        anio = int.Parse(form["anio"]),
                        salario_bruto = decimal.Parse(form[$"salario_bruto_{i}"]),
                        horas_extras = decimal.Parse(form[$"horas_extras_{i}"]),
                        salario_dias_feriados = decimal.Parse(form[$"dias_feriados_{i}"]),
                        ccss = decimal.Parse(form[$"ccss_{i}"]),
                        ivm = decimal.Parse(form[$"ivm_{i}"]),
                        isr = decimal.Parse(form[$"isr_{i}"]),
                        salario_neto = decimal.Parse(form[$"salario_neto_{i}"]),
                        id_isr = 1, // Tomar el primer registro de ISR disponible
                        fecha_creacion = DateTime.Now
                    };

                    db.Nomina.Add(nomina);
                    nominasGuardadas++;
                }

                db.SaveChanges();

                TempData["Mensaje"] = $"✅ Nómina generada exitosamente para {nominasGuardadas} empleados.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Error al guardar la nómina: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

    }
}
