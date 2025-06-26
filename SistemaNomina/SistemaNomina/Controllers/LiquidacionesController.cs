using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using SistemaNomina.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using System.Diagnostics;

namespace SistemaNomina.Controllers
{
    public class LiquidacionesController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Liquidaciones
        public ActionResult Index()
        {
            try
            {
                Debug.WriteLine("=== INICIO Index Liquidaciones ===");

                var liquidaciones = db.Liquidaciones
                    .Include(l => l.Empleados)
                    .Include(l => l.TipoLiquidacion)
                    .OrderByDescending(l => l.fecha_creacion)
                    .ToList();

                Debug.WriteLine($"Liquidaciones encontradas: {liquidaciones.Count}");

                return View(liquidaciones);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR en Index: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                TempData["Error"] = "Error al cargar liquidaciones: " + ex.Message;
                return View(new List<Liquidaciones>());
            }
        }

        // GET: Liquidaciones/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                TempData["Error"] = "ID de liquidación no válido";
                return RedirectToAction("Index");
            }

            try
            {
                Liquidaciones liquidacion = db.Liquidaciones
                    .Include(l => l.Empleados)
                    .Include(l => l.TipoLiquidacion)
                    .FirstOrDefault(l => l.id_liquidacion == id);

                if (liquidacion == null)
                {
                    TempData["Error"] = "Liquidación no encontrada";
                    return RedirectToAction("Index");
                }

                return View(liquidacion);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar liquidación: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: Liquidaciones/CalcularLiquidacion
        public ActionResult CalcularLiquidacion()
        {
            try
            {
                Debug.WriteLine("=== INICIO CalcularLiquidacion GET ===");

                var empleadosActivos = db.Empleados
                    .Where(e => e.estado == "Activo")
                    .Select(e => new {
                        id_empleado = e.id_empleado,
                        display = e.cedula + " - " + e.nombre1 + " " + e.apellido1
                    })
                    .ToList();

                Debug.WriteLine($"Empleados activos encontrados: {empleadosActivos.Count}");

                if (!empleadosActivos.Any())
                {
                    ViewBag.Error = "No hay empleados activos disponibles para liquidar";
                    ViewBag.IdEmpleado = new SelectList(new List<object>(), "id_empleado", "display");
                }
                else
                {
                    ViewBag.IdEmpleado = new SelectList(empleadosActivos, "id_empleado", "display");
                }

                var tiposLiquidacion = db.TipoLiquidacion.ToList();
                Debug.WriteLine($"Tipos de liquidación encontrados: {tiposLiquidacion.Count}");

                if (!tiposLiquidacion.Any())
                {
                    ViewBag.Error = "No hay tipos de liquidación configurados. Contacte al administrador.";
                    ViewBag.IdTipo = new SelectList(new List<object>(), "id_tipo", "nombre");
                }
                else
                {
                    ViewBag.IdTipo = new SelectList(tiposLiquidacion, "id_tipo", "nombre");
                }

                return View(new CalculoLiquidacionViewModel());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR en CalcularLiquidacion GET: {ex.Message}");
                ViewBag.Error = "Error al cargar datos: " + ex.Message;
                ViewBag.IdEmpleado = new SelectList(new List<object>(), "id_empleado", "display");
                ViewBag.IdTipo = new SelectList(new List<object>(), "id_tipo", "nombre");
                return View(new CalculoLiquidacionViewModel());
            }
        }

        // POST: Liquidaciones/CalcularLiquidacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CalcularLiquidacion(int? idEmpleado, int? idTipo, DateTime? fechaSalida)
        {
            Debug.WriteLine("=== INICIO CalcularLiquidacion POST ===");
            Debug.WriteLine($"Parámetros recibidos - IdEmpleado: {idEmpleado}, IdTipo: {idTipo}, FechaSalida: {fechaSalida}");

            try
            {
                // Validar parámetros de entrada
                if (!idEmpleado.HasValue || idEmpleado.Value <= 0)
                {
                    Debug.WriteLine("ERROR: ID Empleado inválido");
                    ViewBag.Error = "Por favor seleccione un empleado válido";
                    return CargarDatosCalculadora(idEmpleado, idTipo);
                }

                if (!idTipo.HasValue || idTipo.Value <= 0)
                {
                    Debug.WriteLine("ERROR: ID Tipo inválido");
                    ViewBag.Error = "Por favor seleccione un tipo de liquidación válido";
                    return CargarDatosCalculadora(idEmpleado, idTipo);
                }

                if (!fechaSalida.HasValue)
                {
                    Debug.WriteLine("ERROR: Fecha salida inválida");
                    ViewBag.Error = "Por favor ingrese una fecha de salida válida";
                    return CargarDatosCalculadora(idEmpleado, idTipo);
                }

                if (fechaSalida.Value > DateTime.Now)
                {
                    Debug.WriteLine("ERROR: Fecha salida futura");
                    ViewBag.Error = "La fecha de salida no puede ser mayor a la fecha actual";
                    return CargarDatosCalculadora(idEmpleado, idTipo);
                }

                // Buscar empleado
                var empleado = db.Empleados
                    .Include(e => e.Puestos)
                    .FirstOrDefault(e => e.id_empleado == idEmpleado.Value);

                Debug.WriteLine($"Empleado encontrado: {empleado?.nombre1} {empleado?.apellido1}");

                if (empleado == null)
                {
                    Debug.WriteLine("ERROR: Empleado no encontrado");
                    ViewBag.Error = "Empleado no encontrado";
                    return CargarDatosCalculadora(idEmpleado, idTipo);
                }

                if (empleado.estado != "Activo")
                {
                    Debug.WriteLine("ERROR: Empleado no activo");
                    ViewBag.Error = "El empleado seleccionado no está activo";
                    return CargarDatosCalculadora(idEmpleado, idTipo);
                }

                // Validar fecha de salida vs fecha de ingreso
                if (fechaSalida.Value < empleado.fecha_ingreso)
                {
                    Debug.WriteLine("ERROR: Fecha salida anterior a ingreso");
                    ViewBag.Error = "La fecha de salida no puede ser anterior a la fecha de ingreso del empleado";
                    return CargarDatosCalculadora(idEmpleado, idTipo);
                }

                // Buscar tipo de liquidación
                var tipoLiquidacion = db.TipoLiquidacion.Find(idTipo.Value);
                Debug.WriteLine($"Tipo liquidación encontrado: {tipoLiquidacion?.nombre}");

                if (tipoLiquidacion == null)
                {
                    Debug.WriteLine("ERROR: Tipo liquidación no encontrado");
                    ViewBag.Error = "Tipo de liquidación no encontrado";
                    return CargarDatosCalculadora(idEmpleado, idTipo);
                }

                // Calcular liquidación
                Debug.WriteLine("Iniciando cálculo de liquidación...");
                var calculo = CalcularLiquidacionEmpleado(empleado, tipoLiquidacion, fechaSalida.Value);
                Debug.WriteLine($"Cálculo completado - Total: {calculo.TotalLiquidacion}");

                var viewModel = new CalculoLiquidacionViewModel
                {
                    Calculo = calculo,
                    EsVistaPrevia = true
                };

                return CargarDatosCalculadora(idEmpleado, idTipo, viewModel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR en CalcularLiquidacion POST: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                ViewBag.Error = "Error al calcular liquidación: " + ex.Message;
                return CargarDatosCalculadora(idEmpleado, idTipo);
            }
        }

        // Método auxiliar para cargar datos de la calculadora
        private ActionResult CargarDatosCalculadora(int? idEmpleado = null, int? idTipo = null, CalculoLiquidacionViewModel viewModel = null)
        {
            Debug.WriteLine("=== CargarDatosCalculadora ===");

            try
            {
                var empleadosActivos = db.Empleados
                    .Where(e => e.estado == "Activo")
                    .Select(e => new {
                        id_empleado = e.id_empleado,
                        display = e.cedula + " - " + e.nombre1 + " " + e.apellido1
                    })
                    .ToList();

                ViewBag.IdEmpleado = new SelectList(empleadosActivos, "id_empleado", "display", idEmpleado);
                ViewBag.IdTipo = new SelectList(db.TipoLiquidacion, "id_tipo", "nombre", idTipo);

                return View(viewModel ?? new CalculoLiquidacionViewModel());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR en CargarDatosCalculadora: {ex.Message}");
                ViewBag.Error = "Error al cargar datos: " + ex.Message;
                ViewBag.IdEmpleado = new SelectList(new List<object>(), "id_empleado", "display");
                ViewBag.IdTipo = new SelectList(new List<object>(), "id_tipo", "nombre");
                return View(new CalculoLiquidacionViewModel());
            }
        }

        // POST: Liquidaciones/GuardarLiquidacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuardarLiquidacion(CalculoLiquidacionViewModel modelo)
        {
            Debug.WriteLine("=== INICIO GuardarLiquidacion ===");

            try
            {
                Debug.WriteLine($"Modelo recibido - Es null: {modelo == null}");
                Debug.WriteLine($"Calculo es null: {modelo?.Calculo == null}");

                if (modelo?.Calculo == null)
                {
                    Debug.WriteLine("ERROR: Modelo o Calculo es null");
                    TempData["Error"] = "Datos de liquidación no válidos";
                    return RedirectToAction("CalcularLiquidacion");
                }

                Debug.WriteLine($"IdEmpleado: {modelo.Calculo.IdEmpleado}");
                Debug.WriteLine($"TotalLiquidacion: {modelo.Calculo.TotalLiquidacion}");

                if (modelo.Calculo.IdEmpleado <= 0)
                {
                    Debug.WriteLine("ERROR: ID empleado inválido");
                    TempData["Error"] = "ID de empleado no válido";
                    return RedirectToAction("CalcularLiquidacion");
                }

                if (modelo.Calculo.TotalLiquidacion <= 0)
                {
                    Debug.WriteLine("ERROR: Total liquidación inválido");
                    TempData["Error"] = "El total de liquidación debe ser mayor a cero";
                    return RedirectToAction("CalcularLiquidacion");
                }

                // Verificar que el empleado existe y está activo
                var empleado = db.Empleados.Find(modelo.Calculo.IdEmpleado);
                Debug.WriteLine($"Empleado encontrado: {empleado?.nombre1} - Estado: {empleado?.estado}");

                if (empleado == null)
                {
                    Debug.WriteLine("ERROR: Empleado no encontrado");
                    TempData["Error"] = "Empleado no encontrado";
                    return RedirectToAction("CalcularLiquidacion");
                }

                if (empleado.estado != "Activo")
                {
                    Debug.WriteLine("ERROR: Empleado no activo");
                    TempData["Error"] = "El empleado ya fue liquidado anteriormente";
                    return RedirectToAction("Index");
                }

                // Verificar que no exista ya una liquidación para este empleado
                var liquidacionExistente = db.Liquidaciones
                    .FirstOrDefault(l => l.id_empleado == modelo.Calculo.IdEmpleado);

                if (liquidacionExistente != null)
                {
                    Debug.WriteLine("ERROR: Liquidación ya existe");
                    TempData["Error"] = "Ya existe una liquidación para este empleado";
                    return RedirectToAction("Index");
                }

                // Crear nueva liquidación
                Debug.WriteLine("Creando nueva liquidación...");
                var liquidacion = new Liquidaciones
                {
                    id_empleado = modelo.Calculo.IdEmpleado,
                    id_tipo = modelo.Calculo.IdTipo,
                    fecha_salida = modelo.Calculo.FechaSalida,
                    preaviso = modelo.Calculo.Preaviso,
                    cesantia = modelo.Calculo.Cesantia,
                    vacaciones_pendientes = modelo.Calculo.VacacionesPendientes,
                    dias_vacaciones_pendientes = modelo.Calculo.DiasVacacionesPendientes,
                    aguinaldo_proporcional = modelo.Calculo.AguinaldoProporcional,
                    total_liquidacion = modelo.Calculo.TotalLiquidacion,
                    isr_liquidacion = modelo.Calculo.ISR,
                    css_liquidacion = modelo.Calculo.CCSS,
                    ivm_liquidacion = modelo.Calculo.IVM,
                    fecha_creacion = DateTime.Now,
                    fecha_actualizacion = DateTime.Now
                };

                db.Liquidaciones.Add(liquidacion);

                // Cambiar estado del empleado a "Inactivo"
                empleado.estado = "Inactivo";
                empleado.fecha_actualizacion = DateTime.Now;

                // Guardar cambios en una transacción
                Debug.WriteLine("Guardando cambios en base de datos...");
                db.SaveChanges();
                Debug.WriteLine("Cambios guardados exitosamente");

                TempData["Success"] = $"Liquidación guardada exitosamente para {empleado.nombre1} {empleado.apellido1}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR en GuardarLiquidacion: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                TempData["Error"] = "Error al guardar liquidación: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // Método para calcular liquidación
        private LiquidacionViewModel CalcularLiquidacionEmpleado(Empleados empleado, TipoLiquidacion tipo, DateTime fechaSalida)
        {
            Debug.WriteLine("=== CalcularLiquidacionEmpleado ===");

            try
            {
                var fechaIngreso = empleado.fecha_ingreso;
                var aniosLaborados = CalcularAniosLaborados(fechaIngreso, fechaSalida);
                var salarioBase = empleado.Puestos?.salario_base ?? 0;

                Debug.WriteLine($"Salario base: {salarioBase}");

                if (salarioBase <= 0)
                {
                    throw new Exception("El empleado no tiene un salario base configurado");
                }

                // Obtener datos de vacaciones pendientes
                var vacaciones = db.Vacaciones
                    .Where(v => v.id_empleado == empleado.id_empleado)
                    .OrderByDescending(v => v.periodo)
                    .FirstOrDefault();

                int diasVacacionesPendientes = 0;
                if (vacaciones != null)
                {
                    diasVacacionesPendientes = Math.Max(0, vacaciones.dias_disponibles - (vacaciones.dias_disfrutados ?? 0));
                }

                // Calcular componentes según tipo de liquidación
                decimal preaviso = 0;
                decimal cesantia = 0;
                decimal vacacionesPendientes = 0;
                decimal aguinaldoProporcional = 0;

                // Calcular vacaciones pendientes
                if (diasVacacionesPendientes > 0)
                {
                    vacacionesPendientes = CalcularVacacionesPendientes(empleado.id_empleado, diasVacacionesPendientes);
                }

                // Calcular aguinaldo proporcional
                aguinaldoProporcional = CalcularAguinaldoProporcional(empleado.id_empleado, fechaIngreso, fechaSalida);

                // Según el tipo de liquidación
                var nombreTipo = tipo.nombre?.ToLower() ?? "";
                switch (nombreTipo)
                {
                    case "con responsabilidad":
                    case "despido con responsabilidad":
                        preaviso = salarioBase; // 1 mes de salario
                        cesantia = salarioBase * (decimal)Math.Floor(aniosLaborados); // Salario x años trabajados
                        break;

                    case "sin responsabilidad":
                    case "despido sin responsabilidad":
                    case "renuncia voluntaria":
                        // Solo aguinaldo y vacaciones
                        preaviso = 0;
                        cesantia = 0;
                        break;

                    default:
                        // Tipo desconocido, solo aguinaldo y vacaciones
                        preaviso = 0;
                        cesantia = 0;
                        break;
                }

                // Calcular totales
                var salarioBruto = preaviso + cesantia + vacacionesPendientes + aguinaldoProporcional;

                // Calcular deducciones
                var ccss = salarioBruto * 0.0934m; // 9.34%
                var ivm = salarioBruto * 0.0275m;  // 2.75%
                var isr = CalcularISR(salarioBruto, empleado.cantidad_hijos ?? 0); // ISR según tabla

                var totalDeducciones = ccss + ivm + isr;
                var totalLiquidacion = salarioBruto - totalDeducciones;

                Debug.WriteLine($"Totales calculados - Bruto: {salarioBruto}, Deducciones: {totalDeducciones}, Neto: {totalLiquidacion}");

                return new LiquidacionViewModel
                {
                    IdEmpleado = empleado.id_empleado,
                    NombreCompleto = $"{empleado.nombre1} {empleado.nombre2} {empleado.apellido1} {empleado.apellido2}".Trim(),
                    Cedula = empleado.cedula,
                    FechaIngreso = fechaIngreso,
                    FechaSalida = fechaSalida,
                    IdTipo = tipo.id_tipo,
                    NombreTipoLiquidacion = tipo.nombre,
                    AniosLaborados = (decimal)aniosLaborados,
                    SalarioBase = salarioBase,
                    DiasVacacionesPendientes = diasVacacionesPendientes,
                    VacacionesPendientes = vacacionesPendientes,
                    AguinaldoProporcional = aguinaldoProporcional,
                    Preaviso = preaviso,
                    Cesantia = cesantia,
                    SalarioBruto = salarioBruto,
                    CCSS = ccss,
                    IVM = ivm,
                    ISR = isr,
                    TotalDeducciones = totalDeducciones,
                    TotalLiquidacion = totalLiquidacion
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR en CalcularLiquidacionEmpleado: {ex.Message}");
                throw new Exception($"Error en cálculo de liquidación: {ex.Message}");
            }
        }

        private double CalcularAniosLaborados(DateTime fechaIngreso, DateTime fechaSalida)
        {
            var timeSpan = fechaSalida - fechaIngreso;
            return Math.Max(0, timeSpan.TotalDays / 365.25); // Considerar años bisiestos
        }

        private decimal CalcularVacacionesPendientes(int idEmpleado, int diasPendientes)
        {
            try
            {
                // Obtener los últimos 12 salarios mensuales (aproximación de 50 semanas)
                var ultimosSalarios = db.Nomina
                    .Where(n => n.id_empleado == idEmpleado)
                    .OrderByDescending(n => n.anio)
                    .ThenByDescending(n => n.mes)
                    .Take(12)
                    .Select(n => n.salario_bruto)
                    .ToList();

                decimal salarioDiario;

                if (!ultimosSalarios.Any())
                {
                    // Si no hay historial, usar salario base del puesto
                    var empleado = db.Empleados.Include(e => e.Puestos).FirstOrDefault(e => e.id_empleado == idEmpleado);
                    if (empleado?.Puestos?.salario_base > 0)
                    {
                        salarioDiario = empleado.Puestos.salario_base / 30;
                        return salarioDiario * diasPendientes;
                    }
                    return 0;
                }

                var promedioMensual = ultimosSalarios.Average();
                salarioDiario = promedioMensual / 30;

                return salarioDiario * diasPendientes;
            }
            catch (Exception)
            {
                return 0; // Si hay error, retornar 0
            }
        }

        private decimal CalcularAguinaldoProporcional(int idEmpleado, DateTime fechaIngreso, DateTime fechaSalida)
        {
            try
            {
                var anioActual = fechaSalida.Year;

                // Obtener salarios del período del aguinaldo
                var salariosAguinaldo = db.Nomina
                    .Where(n => n.id_empleado == idEmpleado &&
                               ((n.anio == anioActual - 1 && n.mes == 12) ||
                                (n.anio == anioActual && n.mes <= 11)))
                    .Select(n => n.salario_bruto)
                    .ToList();

                if (!salariosAguinaldo.Any())
                {
                    // Si no hay historial, calcular proporcional con salario base
                    var empleado = db.Empleados.Include(e => e.Puestos).FirstOrDefault(e => e.id_empleado == idEmpleado);
                    if (empleado?.Puestos?.salario_base > 0)
                    {
                        var mesesTrabajados = Math.Min(12, (fechaSalida - fechaIngreso).Days / 30.44);
                        return (empleado.Puestos.salario_base * (decimal)mesesTrabajados) / 12;
                    }
                    return 0;
                }

                return salariosAguinaldo.Sum() / 12;
            }
            catch (Exception)
            {
                return 0; // Si hay error, retornar 0
            }
        }

        private decimal CalcularISR(decimal salarioBruto, int cantidadHijos)
        {
            try
            {
                var tablaISR = db.ISR
                    .Where(i => i.anio == DateTime.Now.Year &&
                               salarioBruto >= i.limite_inferior &&
                               salarioBruto <= i.limite_superior)
                    .FirstOrDefault();

                if (tablaISR == null) return 0;

                var baseImponible = salarioBruto - tablaISR.limite_inferior;
                var impuesto = tablaISR.exceso + (baseImponible * (tablaISR.porcentaje / 100));

                // Aplicar créditos
                var creditos = (cantidadHijos * (tablaISR.credito_hijo ?? 0));

                return Math.Max(0, impuesto - creditos);
            }
            catch (Exception)
            {
                return 0; // Si hay error, retornar 0
            }
        }

        // GET: Liquidaciones/GenerarPDF/5
        [HttpGet]
        public ActionResult GenerarPDF(int? id)
        {
            Debug.WriteLine($"=== INICIO GenerarPDF - ID: {id} ===");

            if (id == null)
            {
                Debug.WriteLine("ERROR: ID de liquidación es null");
                TempData["Error"] = "ID de liquidación no válido";
                return RedirectToAction("Index");
            }

            try
            {
                var liquidacion = db.Liquidaciones
                    .Include(l => l.Empleados)
                    .Include(l => l.TipoLiquidacion)
                    .FirstOrDefault(l => l.id_liquidacion == id);

                if (liquidacion == null)
                {
                    Debug.WriteLine("ERROR: Liquidación no encontrada");
                    TempData["Error"] = "Liquidación no encontrada";
                    return RedirectToAction("Index");
                }

                Debug.WriteLine($"Generando PDF para: {liquidacion.Empleados.nombre1} {liquidacion.Empleados.apellido1}");

                var pdfBytes = GenerarPDFLiquidacion(liquidacion);
                var fileName = $"Liquidacion_{liquidacion.Empleados.cedula}_{DateTime.Now:yyyyMMdd}.pdf";

                Debug.WriteLine($"PDF generado exitosamente: {fileName}");

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR al generar PDF: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                TempData["Error"] = "Error al generar PDF: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        private byte[] GenerarPDFLiquidacion(Liquidaciones liquidacion)
        {
            Debug.WriteLine("=== INICIO GenerarPDFLiquidacion ===");

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    var document = new Document(PageSize.A4, 50, 50, 50, 50);
                    var writer = PdfWriter.GetInstance(document, memoryStream);

                    document.Open();

                    // Fonts
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                    var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

                    // Título
                    var title = new Paragraph("LIQUIDACIÓN LABORAL", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 20;
                    document.Add(title);

                    // Información de la empresa
                    document.Add(new Paragraph("Smart Building Solutions", headerFont));
                    document.Add(new Paragraph("Santa Ana, Costa Rica", normalFont));
                    document.Add(new Paragraph($"Fecha: {DateTime.Now:dd/MM/yyyy}", normalFont));
                    document.Add(new Paragraph(" "));

                    // Información del empleado
                    document.Add(new Paragraph("DATOS DEL EMPLEADO", headerFont));
                    document.Add(new Paragraph($"Nombre: {liquidacion.Empleados.nombre1} {liquidacion.Empleados.nombre2} {liquidacion.Empleados.apellido1} {liquidacion.Empleados.apellido2}".Trim(), normalFont));
                    document.Add(new Paragraph($"Cédula: {liquidacion.Empleados.cedula}", normalFont));
                    document.Add(new Paragraph($"Fecha de Ingreso: {liquidacion.Empleados.fecha_ingreso:dd/MM/yyyy}", normalFont));
                    document.Add(new Paragraph($"Fecha de Salida: {liquidacion.fecha_salida:dd/MM/yyyy}", normalFont));
                    document.Add(new Paragraph($"Tipo de Liquidación: {liquidacion.TipoLiquidacion.nombre}", normalFont));
                    document.Add(new Paragraph(" "));

                    // Tabla de conceptos
                    var table = new PdfPTable(2);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 70f, 30f });

                    // Encabezados
                    table.AddCell(new PdfPCell(new Phrase("CONCEPTO", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });
                    table.AddCell(new PdfPCell(new Phrase("MONTO", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });

                    // Conceptos
                    if (liquidacion.preaviso > 0)
                    {
                        table.AddCell(new Phrase("Preaviso", normalFont));
                        table.AddCell(new Phrase($"₡{liquidacion.preaviso:N2}", normalFont));
                    }

                    if (liquidacion.cesantia > 0)
                    {
                        table.AddCell(new Phrase("Cesantía", normalFont));
                        table.AddCell(new Phrase($"₡{liquidacion.cesantia:N2}", normalFont));
                    }

                    if (liquidacion.vacaciones_pendientes > 0)
                    {
                        table.AddCell(new Phrase($"Vacaciones ({liquidacion.dias_vacaciones_pendientes} días)", normalFont));
                        table.AddCell(new Phrase($"₡{liquidacion.vacaciones_pendientes:N2}", normalFont));
                    }

                    if (liquidacion.aguinaldo_proporcional > 0)
                    {
                        table.AddCell(new Phrase("Aguinaldo Proporcional", normalFont));
                        table.AddCell(new Phrase($"₡{liquidacion.aguinaldo_proporcional:N2}", normalFont));
                    }

                    // Subtotal
                    var subtotal = (liquidacion.preaviso ?? 0) + (liquidacion.cesantia ?? 0) +
                                  (liquidacion.vacaciones_pendientes ?? 0) + (liquidacion.aguinaldo_proporcional ?? 0);

                    table.AddCell(new PdfPCell(new Phrase("SUBTOTAL", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });
                    table.AddCell(new PdfPCell(new Phrase($"₡{subtotal:N2}", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });

                    // Deducciones
                    if (liquidacion.css_liquidacion > 0)
                    {
                        table.AddCell(new Phrase("CCSS (9.34%)", normalFont));
                        table.AddCell(new Phrase($"-₡{liquidacion.css_liquidacion:N2}", normalFont));
                    }

                    if (liquidacion.ivm_liquidacion > 0)
                    {
                        table.AddCell(new Phrase("IVM (2.75%)", normalFont));
                        table.AddCell(new Phrase($"-₡{liquidacion.ivm_liquidacion:N2}", normalFont));
                    }

                    if (liquidacion.isr_liquidacion > 0)
                    {
                        table.AddCell(new Phrase("Impuesto sobre la Renta", normalFont));
                        table.AddCell(new Phrase($"-₡{liquidacion.isr_liquidacion:N2}", normalFont));
                    }

                    // Total
                    table.AddCell(new PdfPCell(new Phrase("TOTAL A PAGAR", titleFont)) { BackgroundColor = BaseColor.DARK_GRAY });
                    table.AddCell(new PdfPCell(new Phrase($"₡{liquidacion.total_liquidacion:N2}", titleFont)) { BackgroundColor = BaseColor.DARK_GRAY });

                    document.Add(table);

                    // Firmas
                    document.Add(new Paragraph(" "));
                    document.Add(new Paragraph("_________________________", normalFont));
                    document.Add(new Paragraph("Firma del Empleado", normalFont));
                    document.Add(new Paragraph(" "));
                    document.Add(new Paragraph("_________________________", normalFont));
                    document.Add(new Paragraph("Firma del Empleador", normalFont));

                    document.Close();
                    Debug.WriteLine("PDF generado correctamente");
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR en GenerarPDFLiquidacion: {ex.Message}");
                throw new Exception($"Error generando PDF: {ex.Message}", ex);
            }
        }

        // GET: Liquidaciones/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                TempData["Error"] = "ID de liquidación no válido";
                return RedirectToAction("Index");
            }

            try
            {
                Liquidaciones liquidaciones = db.Liquidaciones.Find(id);
                if (liquidaciones == null)
                {
                    TempData["Error"] = "Liquidación no encontrada";
                    return RedirectToAction("Index");
                }

                ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", liquidaciones.id_empleado);
                ViewBag.id_tipo = new SelectList(db.TipoLiquidacion, "id_tipo", "nombre", liquidaciones.id_tipo);
                return View(liquidaciones);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar liquidación: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Liquidaciones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_liquidacion,id_empleado,id_tipo,fecha_salida,preaviso,cesantia,vacaciones_pendientes,dias_vacaciones_pendientes,aguinaldo_proporcional,total_liquidacion,isr_liquidacion,css_liquidacion,ivm_liquidacion,fecha_creacion,fecha_actualizacion")] Liquidaciones liquidaciones)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    liquidaciones.fecha_actualizacion = DateTime.Now;
                    db.Entry(liquidaciones).State = EntityState.Modified;
                    db.SaveChanges();
                    TempData["Success"] = "Liquidación actualizada exitosamente";
                    return RedirectToAction("Index");
                }

                ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", liquidaciones.id_empleado);
                ViewBag.id_tipo = new SelectList(db.TipoLiquidacion, "id_tipo", "nombre", liquidaciones.id_tipo);
                return View(liquidaciones);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar liquidación: " + ex.Message;
                ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", liquidaciones.id_empleado);
                ViewBag.id_tipo = new SelectList(db.TipoLiquidacion, "id_tipo", "nombre", liquidaciones.id_tipo);
                return View(liquidaciones);
            }
        }

        // GET: Liquidaciones/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                TempData["Error"] = "ID de liquidación no válido";
                return RedirectToAction("Index");
            }

            try
            {
                Liquidaciones liquidaciones = db.Liquidaciones
                    .Include(l => l.Empleados)
                    .Include(l => l.TipoLiquidacion)
                    .FirstOrDefault(l => l.id_liquidacion == id);

                if (liquidaciones == null)
                {
                    TempData["Error"] = "Liquidación no encontrada";
                    return RedirectToAction("Index");
                }
                return View(liquidaciones);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar liquidación: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Liquidaciones/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                Liquidaciones liquidaciones = db.Liquidaciones.Find(id);
                if (liquidaciones == null)
                {
                    TempData["Error"] = "Liquidación no encontrada";
                    return RedirectToAction("Index");
                }

                // Opcional: Reactivar empleado al eliminar liquidación
                var empleado = db.Empleados.Find(liquidaciones.id_empleado);
                if (empleado != null)
                {
                    empleado.estado = "Activo";
                    empleado.fecha_actualizacion = DateTime.Now;
                }

                db.Liquidaciones.Remove(liquidaciones);
                db.SaveChanges();

                TempData["Success"] = "Liquidación eliminada exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar liquidación: " + ex.Message;
                return RedirectToAction("Index");
            }
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