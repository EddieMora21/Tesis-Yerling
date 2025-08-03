using System;

namespace SistemaNomina.Models
{
    /// <summary>
    /// ViewModel para manejar las deducciones legales de nómina
    /// </summary>
    public class DeduccionesViewModel
    {
        public decimal CCSS { get; set; }
        public decimal IVM { get; set; }
        public decimal ISR { get; set; }

        public DeduccionesViewModel(decimal ccss, decimal ivm, decimal isr)
        {
            CCSS = Math.Round(ccss, 2);
            IVM = Math.Round(ivm, 2);
            ISR = Math.Round(isr, 2);
        }

        /// <summary>
        /// Calcula el total de todas las deducciones
        /// </summary>
        public decimal TotalDeducciones => CCSS + IVM + ISR;

        /// <summary>
        /// Obtiene el porcentaje de CCSS aplicado
        /// </summary>
        public decimal PorcentajeCCSS => 10.67m;

        /// <summary>
        /// Obtiene el porcentaje de IVM aplicado
        /// </summary>
        public decimal PorcentajeIVM => 4.17m;
    }
}