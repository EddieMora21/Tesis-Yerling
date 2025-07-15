// Services/NotificacionService.cs
using SistemaNomina.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaNomina.Services
{
    public class NotificacionService : IDisposable
    {
        private smartbuilding_rhEntities _context;
        private bool _disposed = false;

        public NotificacionService()
        {
            _context = new smartbuilding_rhEntities();
        }

        public async Task<bool> CrearNotificacion(int? remitenteId, int destinatarioId, string titulo,
            string mensaje, string tipo, int? referenciaId = null, string referenciaTipo = null, string prioridad = "normal")
        {
            try
            {
                var notificacion = new Notificaciones
                {
                    id_remitente = remitenteId,
                    id_destinatario = destinatarioId,
                    titulo = titulo,
                    mensaje = mensaje,
                    tipo = tipo,
                    referencia_id = referenciaId,
                    referencia_tipo = referenciaTipo,
                    prioridad = prioridad ?? "normal",
                    icono = ObtenerIconoPorTipo(tipo, referenciaTipo),
                    color = ObtenerColorPorTipo(tipo),
                    fecha_creacion = DateTime.Now,
                    leido = false
                };

                _context.Set<Notificaciones>().Add(notificacion);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al crear notificación: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Notificaciones>> ObtenerNotificacionesUsuario(int empleadoId, bool soloNoLeidas = false, int limite = 50)
        {
            var query = _context.Set<Notificaciones>()
                .Include("Empleados") // Remitente
                .Include("Empleados1") // Destinatario
                .Where(n => n.id_destinatario == empleadoId);

            if (soloNoLeidas)
            {
                query = query.Where(n => n.leido.HasValue && !n.leido.Value);
            }

            return await query
                .OrderByDescending(n => n.fecha_creacion)
                .Take(limite)
                .ToListAsync();
        }

        public async Task<int> ContarNotificacionesNoLeidas(int empleadoId)
        {
            return await _context.Set<Notificaciones>()
                .CountAsync(n => n.id_destinatario == empleadoId && (n.leido.HasValue && !n.leido.Value));
        }

        public async Task<bool> MarcarComoLeida(int notificacionId, int usuarioId)
        {
            try
            {
                var notificacion = await _context.Set<Notificaciones>()
                    .FirstOrDefaultAsync(n => n.id_notificacion == notificacionId && n.id_destinatario == usuarioId);

                if (notificacion != null && notificacion.leido.HasValue && !notificacion.leido.Value)
                {
                    notificacion.leido = true;
                    notificacion.fecha_leido = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> MarcarTodasComoLeidas(int empleadoId)
        {
            try
            {
                var notificaciones = await _context.Set<Notificaciones>()
     .Where(n => n.id_destinatario == empleadoId && n.leido.HasValue && !n.leido.Value)
     .ToListAsync();

                foreach (var notificacion in notificaciones)
                {
                    notificacion.leido = true;
                    notificacion.fecha_leido = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EliminarNotificacion(int notificacionId, int usuarioId)
        {
            try
            {
                var notificacion = await _context.Set<Notificaciones>()
                    .FirstOrDefaultAsync(n => n.id_notificacion == notificacionId && n.id_destinatario == usuarioId);

                if (notificacion != null)
                {
                    _context.Set<Notificaciones>().Remove(notificacion);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Métodos específicos para diferentes tipos de notificaciones
        public async Task NotificarSolicitudVacaciones(int solicitanteId, int supervisorId, int solicitudId)
        {
            var solicitante = await _context.Empleados.FindAsync(solicitanteId);
            if (solicitante != null)
            {
                await CrearNotificacion(
                    solicitanteId,
                    supervisorId,
                    "Nueva Solicitud de Vacaciones",
                    $"{solicitante.nombre1} {solicitante.apellido1} ha solicitado vacaciones y requiere su aprobación.",
                    "solicitud",
                    solicitudId,
                    "vacacion",
                    "alta"
                );
            }
        }

        public async Task NotificarAprobacionVacaciones(int solicitanteId, int aprobadorId, int solicitudId, bool aprobada, string comentario = "")
        {
            var aprobador = await _context.Empleados.FindAsync(aprobadorId);
            var estado = aprobada ? "aprobada" : "rechazada";
            var titulo = aprobada ? "Vacaciones Aprobadas" : "Vacaciones Rechazadas";
            var mensaje = $"Su solicitud de vacaciones ha sido {estado}";

            if (aprobador != null)
            {
                mensaje += $" por {aprobador.nombre1} {aprobador.apellido1}";
            }

            if (!string.IsNullOrEmpty(comentario))
            {
                mensaje += $". Comentario: {comentario}";
            }

            await CrearNotificacion(
                aprobadorId,
                solicitanteId,
                titulo,
                mensaje,
                aprobada ? "aprobacion" : "rechazo",
                solicitudId,
                "vacacion",
                "alta"
            );
        }

        public async Task NotificarNominaGenerada(int empleadoId, int mes, int anio)
        {
            var nombreMes = ObtenerNombreMes(mes);
            await CrearNotificacion(
                null,
                empleadoId,
                "Nómina Generada",
                $"Su planilla correspondiente al mes de {nombreMes} {anio} ha sido generada y está disponible para consulta.",
                "sistema",
                null,
                "nomina",
                "normal"
            );
        }

        // Métodos auxiliares
        private string ObtenerIconoPorTipo(string tipo, string referenciaTipo = null)
        {
            switch (tipo)
            {
                case "solicitud":
                    switch (referenciaTipo)
                    {
                        case "vacacion": return "fa-paper-plane";
                        case "permiso": return "fa-stamp";
                        case "hora_extra": return "fa-clock";
                        default: return "fa-paper-plane";
                    }
                case "aprobacion": return "fa-check-circle";
                case "rechazo": return "fa-times-circle";
                case "sistema":
                    switch (referenciaTipo)
                    {
                        case "nomina": return "fa-money-check-alt";
                        case "aguinaldo": return "fa-gift";
                        case "liquidacion": return "fa-calculator";
                        default: return "fa-cog";
                    }
                case "informativa":
                    switch (referenciaTipo)
                    {
                        case "incapacidad": return "fa-notes-medical";
                        default: return "fa-info-circle";
                    }
                default: return "fa-bell";
            }
        }

        private string ObtenerColorPorTipo(string tipo)
        {
            switch (tipo)
            {
                case "solicitud": return "warning";
                case "aprobacion": return "success";
                case "rechazo": return "danger";
                case "sistema": return "info";
                case "informativa": return "secondary";
                default: return "primary";
            }
        }

        private string ObtenerNombreMes(int mes)
        {
            string[] meses = {
                "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
            };
            return mes >= 1 && mes <= 12 ? meses[mes] : "Mes inválido";
        }

        // Implementación de IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}