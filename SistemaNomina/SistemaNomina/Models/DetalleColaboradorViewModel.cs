using System.Collections.Generic;

namespace SistemaNomina.Models
{
    public class DetalleColaboradorViewModel
    {
        public Empleados Empleado { get; set; }
        public List<Nomina> UltimasNominas { get; set; }
        public Vacaciones VacacionesActuales { get; set; }
        public List<SolicitudesVacaciones> SolicitudesVacaciones { get; set; }
        public List<HorasExtras> HorasExtrasAnual { get; set; }
        public List<Incapacidades> Incapacidades { get; set; }
        public List<Asistencia> AsistenciaReciente { get; set; }
    }
}