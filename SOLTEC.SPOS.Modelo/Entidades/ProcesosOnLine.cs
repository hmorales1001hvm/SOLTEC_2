using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOLTEC.SPOS.Modelo.Entidades
{
    public class ProcesosOnLine
    {
        public string Sucursal { get; set; }
        public string Json { get; set; }
        public string NombreProceso { get; set; }
        public int IdSucursal { get; set; }
        public string Ver1 { get; set; }
        public string Ver2 { get; set; }
        public string Ver3 { get; set; }
        public string Ver4 { get; set; }
        public string Ver5 { get; set; }
        public string Ver6 { get; set; }
        public bool ConDatos { get; set; }
        public int MultiFra { get; set; }
        public string HostName { get; set; }
        public string DatabaseName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool ConTransmisionInicial { get; set; }
        public string TicketsFaltantes { get; set; }
        public string TipoCarga { get; set; }

    }
}
