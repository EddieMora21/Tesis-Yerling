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
        // ✅ MÉTODO INDEX CORREGIDO CON FILTROS
        public ActionResult Index(int? anio, int? mes, string empleado)
        {
            try
            {
                // ✅ QUERY BASE: Incluir relaciones necesarias
                var query = db.Nomina
                    .Include(n => n.Empleados)
                    .Include(n => n.Empleados.Puestos)
                    .Include(n => n.Empleados.Puestos.Departamentos)
                    .Include(n => n.ISR1)
                    .AsQueryable();

                // ✅ FILTRO POR AÑO
                if (anio.HasValue)
                {
                    query = query.Where(n => n.anio == anio.Value);
                }

                // ✅ FILTRO POR MES
                if (mes.HasValue)
                {
                    query = query.Where(n => n.mes == mes.Value);
                }

                // ✅ FILTRO POR EMPLEADO (cédula o nombre)
                if (!string.IsNullOrEmpty(empleado))
                {
                    string filtroEmpleado = empleado.Trim().ToLower();
                    query = query.Where(n =>
                        n.Empleados.cedula.Contains(filtroEmpleado) ||
                        n.Empleados.nombre1.ToLower().Contains(filtroEmpleado) ||
                        n.Empleados.apellido1.ToLower().Contains(filtroEmpleado) ||
                        (n.Empleados.nombre1 + " " + n.Empleados.apellido1).ToLower().Contains(filtroEmpleado)
                    );
                }

                // ✅ ORDENAR RESULTADOS: Más recientes primero
                var nominas = query
                    .OrderByDescending(n => n.anio)
                    .ThenByDescending(n => n.mes)
                    .ThenBy(n => n.Empleados.apellido1)
                    .ThenBy(n => n.Empleados.nombre1)
                    .ToList();

                // ✅ MANTENER VALORES DE FILTROS EN ViewBag
                ViewBag.FiltroAnio = anio?.ToString();
                ViewBag.FiltroMes = mes?.ToString();
                ViewBag.FiltroEmpleado = empleado;

                // ✅ INFORMACIÓN ADICIONAL
                ViewBag.TotalRegistros = nominas.Count;
                ViewBag.TotalEmpleados = nominas.Select(n => n.id_empleado).Distinct().Count();

                // ✅ MENSAJE INFORMATIVO
                if (anio.HasValue || mes.HasValue || !string.IsNullOrEmpty(empleado))
                {
                    ViewBag.MensajeFiltro = $"Mostrando {nominas.Count} registros filtrados";
                }

                return View(nominas);
            }
            catch (Exception ex)
            {
                // ✅ MANEJO DE ERRORES
                TempData["Error"] = "Error al cargar las nóminas: " + ex.Message;
                return View(new List<Nomina>());
            }
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
        /// <summary>
        /// ✅ BÚSQUEDA AJAX: Para filtros en tiempo real
        /// </summary>
        [HttpPost]
        public JsonResult BuscarNominas(int? anio, int? mes, string empleado)
        {
            try
            {
                var query = db.Nomina
                    .Include(n => n.Empleados)
                    .Include(n => n.ISR1)
                    .AsQueryable();

                // Aplicar filtros
                if (anio.HasValue)
                    query = query.Where(n => n.anio == anio.Value);

                if (mes.HasValue)
                    query = query.Where(n => n.mes == mes.Value);

                if (!string.IsNullOrEmpty(empleado))
                {
                    string filtro = empleado.Trim().ToLower();
                    query = query.Where(n =>
                        n.Empleados.cedula.Contains(filtro) ||
                        n.Empleados.nombre1.ToLower().Contains(filtro) ||
                        n.Empleados.apellido1.ToLower().Contains(filtro)
                    );
                }

                var resultados = query
                    .OrderByDescending(n => n.anio)
                    .ThenByDescending(n => n.mes)
                    .Select(n => new
                    {
                        id_nomina = n.id_nomina,
                        empleado = n.Empleados.nombre1 + " " + n.Empleados.apellido1,
                        cedula = n.Empleados.cedula,
                        periodo = n.mes + "/" + n.anio,
                        salario_bruto = n.salario_bruto,
                        salario_neto = n.salario_neto,
                        horas_extras = n.horas_extras ?? 0,
                        dias_feriados = n.salario_dias_feriados ?? 0,
                        deducciones = (n.ccss ?? 0) + (n.ivm ?? 0) + (n.isr ?? 0)
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    data = resultados,
                    total = resultados.Count
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult MiHistorial()
        {
            // Obtener el usuario autenticado
            var usuarioActual = User.Identity.Name;

            // Buscar el empleado basado en el usuario autenticado
            var empleado = db.Usuarios
                .Where(u => u.usuario == usuarioActual)
                .Select(u => u.Empleados)
                .FirstOrDefault();

            if (empleado == null)
            {
                TempData["Error"] = "No se pudo encontrar información del empleado.";
                return RedirectToAction("Index", "Home");
            }

            // Obtener todas las nóminas del empleado
            var misNominas = db.Nomina
                .Where(n => n.id_empleado == empleado.id_empleado)
                .Include(n => n.Empleados)
                .Include(n => n.ISR1)
                .OrderByDescending(n => n.anio)
                .ThenByDescending(n => n.mes)
                .ToList();

            ViewBag.NombreEmpleado = $"{empleado.nombre1} {empleado.apellido1}";
            ViewBag.CedulaEmpleado = empleado.cedula;

            return View(misNominas);
        }

        /// <summary>
        /// CASO DE USO: Detalle de mi planilla específica
        /// Permite al empleado ver el detalle de una nómina específica (solo suya)
        /// </summary>
        public ActionResult MiDetalle(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Obtener el usuario autenticado
            var usuarioActual = User.Identity.Name;

            // Buscar el empleado basado en el usuario autenticado
            var empleado = db.Usuarios
                .Where(u => u.usuario == usuarioActual)
                .Select(u => u.Empleados)
                .FirstOrDefault();

            if (empleado == null)
            {
                TempData["Error"] = "No se pudo encontrar información del empleado.";
                return RedirectToAction("Index", "Home");
            }

            // Buscar la nómina solo si pertenece al empleado autenticado
            Nomina nomina = db.Nomina
                .Include(n => n.Empleados)
                .Include(n => n.ISR1)
                .FirstOrDefault(n => n.id_nomina == id && n.id_empleado == empleado.id_empleado);

            if (nomina == null)
            {
                TempData["Error"] = "No tiene permisos para ver esta planilla o no existe.";
                return RedirectToAction("MiHistorial");
            }

            return View(nomina);
        }

        // Muestra el formulario para crear una nueva nómina
        // Muestra el formulario para crear una nueva nómina
        public ActionResult Create()
        {
            // ✅ CORREGIR: Llenar dropdown con cédula + nombre
            var empleados = db.Empleados
                .Where(e => e.estado == "Activo")
                .Select(e => new {
                    id_empleado = e.id_empleado,
                    display = e.cedula + " - " + e.nombre1 + " " + e.apellido1
                })
                .ToList();

            ViewBag.id_empleado = new SelectList(empleados, "id_empleado", "display");

            // ✅ CORREGIR: Llenar dropdown ISR con descripción más clara
            var categoriaISR = db.ISR
                .Select(i => new {
                    id_isr = i.id_isr,
                    display = i.descripcion + " (" + i.anio + ")"
                })
                .ToList();

            ViewBag.id_isr = new SelectList(categoriaISR, "id_isr", "display");

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
                // Establecer fechas automáticamente
                nomina.fecha_creacion = DateTime.Now;
                nomina.fecha_actualizacion = DateTime.Now;

                // Agrega la nueva nómina a la bd
                db.Nomina.Add(nomina);
                db.SaveChanges();

                TempData["Mensaje"] = "✅ Nómina creada exitosamente.";
                return RedirectToAction("Index");
            }

            // Si hubo error, vuelve a llenar los combos y muestra el formulario
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", nomina.id_empleado);
            ViewBag.id_isr = new SelectList(db.ISR, "id_isr", "descripcion", nomina.id_isr);
            return View(nomina);
        }

        // Muestra el formulario para editar una nómina existente
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

            // ✅ CORREGIR: Llenar los combos con información mejorada
            var empleados = db.Empleados
                .Where(e => e.estado == "Activo")
                .Select(e => new {
                    id_empleado = e.id_empleado,
                    display = e.cedula + " - " + e.nombre1 + " " + e.apellido1
                })
                .ToList();

            ViewBag.id_empleado = new SelectList(empleados, "id_empleado", "display", nomina.id_empleado);

            var categoriaISR = db.ISR
                .Select(i => new {
                    id_isr = i.id_isr,
                    display = i.descripcion + " (" + i.anio + ")"
                })
                .ToList();

            ViewBag.id_isr = new SelectList(categoriaISR, "id_isr", "display", nomina.id_isr);

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
                // Actualizar fecha de modificación
                nomina.fecha_actualizacion = DateTime.Now;

                // Marca la nómina como modificada
                db.Entry(nomina).State = EntityState.Modified;
                db.SaveChanges();

                TempData["Mensaje"] = "✅ Nómina actualizada exitosamente.";
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

            TempData["Mensaje"] = "✅ Nómina eliminada exitosamente.";
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
        public ActionResult CalcularNomina(int mes, int anio, List<int> empleadosSeleccionados)
        {
            if (empleadosSeleccionados == null || !empleadosSeleccionados.Any())
            {
                TempData["Error"] = "❌ Debe seleccionar al menos un empleado.";
                return RedirectToAction("SeleccionarPeriodo");
            }

            // Crear una lista estructurada en lugar de objetos dinámicos
            var resultados = new List<CalculoNominaViewModel>();

            foreach (int idEmpleado in empleadosSeleccionados)
            {
                try
                {
                    var empleado = db.Empleados.Find(idEmpleado);
                    if (empleado == null || empleado.Puestos == null) continue;

                    // 1. CALCULAR SALARIO BASE
                    decimal salarioBase = empleado.Puestos.salario_base;

                    // 2. CALCULAR HORAS EXTRAS DEL MES (solo si existen registros)
                    var horasExtras = db.HorasExtras
                        .Where(he => he.id_empleado == idEmpleado &&
                                    he.fecha.Month == mes &&
                                    he.fecha.Year == anio &&
                                    he.Estados.nombre == "Aprobado")
                        .Sum(he => (decimal?)he.total) ?? 0;

                    // 3. CALCULAR DÍAS FERIADOS TRABAJADOS
                    var diasFeriados = CalcularPagoFeriados(idEmpleado, mes, anio);

                    // 4. CALCULAR SALARIO BRUTO
                    decimal salarioBruto = salarioBase + horasExtras + diasFeriados;

                    // 5. APLICAR DEDUCCIONES LEGALES
                    var deducciones = CalcularDeduccionesLegales(salarioBruto, empleado.cantidad_hijos, anio);

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
                catch (Exception ex)
                {
                    // Log del error específico del empleado
                    System.Diagnostics.Debug.WriteLine($"Error calculando nómina para empleado {idEmpleado}: {ex.Message}");
                    continue; // Continúa con el siguiente empleado
                }
            }

            if (!resultados.Any())
            {
                TempData["Error"] = "❌ No se pudo calcular la nómina para ningún empleado seleccionado.";
                return RedirectToAction("SeleccionarPeriodo");
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
        private DeduccionesViewModel CalcularDeduccionesLegales(decimal salarioBruto, int cantidadHijos, int anio)
        {
            // ✅ PORCENTAJES SEGÚN LEY COSTARRICENSE VIGENTE

            // CCSS (Enfermedad y Maternidad): 10.67%
            decimal ccss = salarioBruto * 0.1067m;

            // IVM (Invalidez, Vejez y Muerte): 4.17%  
            decimal ivm = salarioBruto * 0.0417m;

            // ISR: Según tabla progresiva actualizada
            decimal isr = CalcularISR(salarioBruto, cantidadHijos, anio);

            return new DeduccionesViewModel(ccss, ivm, isr);
        }

        /// <summary>
        /// Calcula el Impuesto Sobre la Renta según normativa costarricense vigente
        /// CORREGIDO - Cálculo directo por tramos sin usar campo 'exceso'
        /// </summary>
        /// <summary>
        /// ✅ VERSIÓN SIMPLIFICADA - Calcula ISR sin depender de la tabla ISR para créditos
        /// </summary>
        private decimal CalcularISR(decimal salarioBruto, int cantidadHijos, int anio)
        {
            decimal isrTotal = 0;

            // TRAMO 1: Hasta ₡922,000.00 - EXENTO (0%)
            if (salarioBruto <= 922000)
            {
                return 0;
            }

            // TRAMO 2: Exceso de ₡922,000.00 hasta ₡1,352,000.00 - 10%
            if (salarioBruto > 922000)
            {
                decimal baseTramo2 = Math.Min(salarioBruto, 1352000) - 922000;
                isrTotal += baseTramo2 * 0.10m;
            }

            // TRAMO 3: Exceso de ₡1,352,000.00 hasta ₡2,373,000.00 - 15%
            if (salarioBruto > 1352000)
            {
                decimal baseTramo3 = Math.Min(salarioBruto, 2373000) - 1352000;
                isrTotal += baseTramo3 * 0.15m;
            }

            // TRAMO 4: Exceso de ₡2,373,000.00 hasta ₡4,745,000.00 - 20%
            if (salarioBruto > 2373000)
            {
                decimal baseTramo4 = Math.Min(salarioBruto, 4745000) - 2373000;
                isrTotal += baseTramo4 * 0.20m;
            }

            // TRAMO 5: Exceso de ₡4,745,000.00 - 25%
            if (salarioBruto > 4745000)
            {
                decimal baseTramo5 = salarioBruto - 4745000;
                isrTotal += baseTramo5 * 0.25m;
            }

            // ✅ CRÉDITO FIJO POR HIJO (según ley costarricense 2025)
            if (cantidadHijos > 0)
            {
                decimal creditoPorHijo = 9000m; // ₡9,000 por hijo (valor aproximado vigente)
                decimal totalCreditos = creditoPorHijo * cantidadHijos;
                isrTotal = Math.Max(0, isrTotal - totalCreditos);
            }

            return Math.Round(isrTotal, 2);
        }

        /// <summary>
        /// ✅ CORREGIDO: Calcula el pago adicional por días feriados trabajados
        /// Sin usar .Date que causa problemas en Entity Framework
        /// </summary>
        private decimal CalcularPagoFeriados(int idEmpleado, int mes, int anio)
        {
            try
            {
                // ✅ CORREGIR: Obtener feriados del mes sin usar .Date
                var feriadosDelMes = db.Feriados
                    .Where(f => f.fecha.Month == mes && f.fecha.Year == anio)
                    .ToList(); // Convertir a lista para procesar en memoria

                decimal totalFeriados = 0;

                foreach (var feriado in feriadosDelMes)
                {
                    // ✅ CORREGIR: Comparar fechas sin usar .Date en LINQ to Entities
                    var fechaFeriado = feriado.fecha;

                    var asistencia = db.Asistencia
                        .Where(a => a.id_empleado == idEmpleado &&
                                   a.fecha.Year == fechaFeriado.Year &&
                                   a.fecha.Month == fechaFeriado.Month &&
                                   a.fecha.Day == fechaFeriado.Day &&
                                   a.hora_entrada.HasValue && a.hora_salida.HasValue)
                        .FirstOrDefault();

                    if (asistencia != null && feriado.pago_obligatorio == true)
                    {
                        var empleado = db.Empleados.Find(idEmpleado);
                        if (empleado?.Puestos != null)
                        {
                            decimal salarioDiario = empleado.Puestos.salario_base / 30;

                            // Pago normal + recargo por feriado
                            decimal recargo = feriado.recargo;
                            totalFeriados += salarioDiario * (1 + recargo / 100);
                        }
                    }
                }

                return Math.Round(totalFeriados, 2);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en CalcularPagoFeriados: {ex.Message}");
                return 0; // En caso de error, devolver 0
            }
        }

        /// <summary>
        /// Guarda la nómina calculada en la base de datos
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuardarNomina(FormCollection form)
        {
            try
            {
                var nominasGuardadas = 0;
                var empleadoKeys = Request.Form.AllKeys.Where(k => k.StartsWith("empleado_")).ToList();

                if (!empleadoKeys.Any())
                {
                    TempData["Error"] = "❌ No hay datos de empleados para guardar.";
                    return RedirectToAction("Index");
                }

                // Procesar cada empleado del formulario
                for (int i = 0; i < empleadoKeys.Count; i++)
                {
                    try
                    {
                        // Verificar que existen todos los campos necesarios
                        if (string.IsNullOrEmpty(form[$"empleado_{i}"]) ||
                            string.IsNullOrEmpty(form[$"salario_bruto_{i}"]))
                        {
                            continue; // Saltar este registro
                        }

                        var nomina = new Nomina
                        {
                            id_empleado = int.Parse(form[$"empleado_{i}"]),
                            mes = int.Parse(form["mes"]),
                            anio = int.Parse(form["anio"]),
                            salario_bruto = decimal.Parse(form[$"salario_bruto_{i}"]),
                            horas_extras = decimal.Parse(form[$"horas_extras_{i}"] ?? "0"),
                            salario_dias_feriados = decimal.Parse(form[$"dias_feriados_{i}"] ?? "0"),
                            ccss = decimal.Parse(form[$"ccss_{i}"] ?? "0"),
                            ivm = decimal.Parse(form[$"ivm_{i}"] ?? "0"),
                            isr = decimal.Parse(form[$"isr_{i}"] ?? "0"),
                            salario_neto = decimal.Parse(form[$"salario_neto_{i}"]),
                            id_isr = 1, // Tomar el primer registro de ISR disponible
                            fecha_creacion = DateTime.Now,
                            fecha_actualizacion = DateTime.Now
                        };

                        db.Nomina.Add(nomina);
                        nominasGuardadas++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error procesando empleado {i}: {ex.Message}");
                        continue; // Continuar con el siguiente empleado
                    }
                }

                if (nominasGuardadas > 0)
                {
                    db.SaveChanges();
                    TempData["Mensaje"] = $"✅ Nómina generada exitosamente para {nominasGuardadas} empleados.";
                }
                else
                {
                    TempData["Error"] = "❌ No se pudo guardar ninguna nómina.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Error al guardar la nómina: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// ✅ NUEVO: Obtener detalle específico de planilla para modal
        /// </summary>
        [HttpPost]
        public ActionResult ObtenerDetallePlanilla(int idNomina)
        {
            try
            {
                var nomina = db.Nomina
                    .Include(n => n.Empleados)
                    .Include(n => n.Empleados.Puestos)
                    .Include(n => n.Empleados.Puestos.Departamentos)
                    .Include(n => n.ISR1)
                    .FirstOrDefault(n => n.id_nomina == idNomina);

                if (nomina == null)
                {
                    return Json(new { success = false, message = "Planilla no encontrada." });
                }

                var detalle = new
                {
                    success = true,
                    data = new
                    {
                        empleado = new
                        {
                            cedula = nomina.Empleados.cedula,
                            nombreCompleto = $"{nomina.Empleados.nombre1} {nomina.Empleados.apellido1}",
                            departamento = nomina.Empleados.Puestos?.Departamentos?.nombre ?? "N/A",
                            puesto = nomina.Empleados.Puestos?.nombre_puesto ?? "N/A"
                        },
                        periodo = new
                        {
                            mes = nomina.mes,
                            anio = nomina.anio,
                            mesTexto = ObtenerNombreMes(nomina.mes)
                        },
                        montos = new
                        {
                            salario_bruto = nomina.salario_bruto,
                            horas_extras = nomina.horas_extras ?? 0,
                            salario_dias_feriados = nomina.salario_dias_feriados ?? 0,
                            ccss = nomina.ccss ?? 0,
                            ivm = nomina.ivm ?? 0,
                            isr = nomina.isr ?? 0,
                            otros_descuentos = nomina.otros_descuentos ?? 0,
                            salario_neto = nomina.salario_neto,
                            total_deducciones = (nomina.ccss ?? 0) + (nomina.ivm ?? 0) + (nomina.isr ?? 0) + (nomina.otros_descuentos ?? 0)
                        },
                        fechas = new
                        {
                            fecha_creacion = nomina.fecha_creacion?.ToString("dd/MM/yyyy HH:mm") ?? "N/A",
                            fecha_actualizacion = nomina.fecha_actualizacion?.ToString("dd/MM/yyyy HH:mm") ?? "N/A"
                        }
                    }
                };

                return Json(detalle);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al obtener el detalle: " + ex.Message });
            }
        }

        private string ObtenerNombreMes(int mes)
        {
            var meses = new[]
            {
                "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
            };
            return mes >= 1 && mes <= 12 ? meses[mes] : "N/A";
        }
    }
}