// Models/SolicitudVacacionesViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SistemaNomina.Models
{
    public class SolicitudVacacionesViewModel
    {
        public int id_solicitud { get; set; }

        [Required(ErrorMessage = "El ID de vacaciones es requerido")]
        public int id_vacacion { get; set; }

        [Required(ErrorMessage = "La fecha de inicio es requerida")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de inicio")]
        public DateTime fecha_inicio { get; set; }

        [Required(ErrorMessage = "La fecha de fin es requerida")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de fin")]
        public DateTime fecha_fin { get; set; }

        [StringLength(500, ErrorMessage = "El comentario no puede exceder los 500 caracteres")]
        [Display(Name = "Comentario de la solicitud")]
        public string comentario_solicitud { get; set; }

        [Display(Name = "Comentario de respuesta")]
        public string comentario_respuesta { get; set; }

        public int id_estado { get; set; }
        public int? aprobado_por { get; set; }
        public DateTime? fecha_aprobacion { get; set; }
    }
}