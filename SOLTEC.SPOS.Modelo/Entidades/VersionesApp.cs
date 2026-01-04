using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOLTEC.SPOS.Modelo.Entidades
{
    public class VersionesApp
    {
        public int Id { get; set; }

        public string NombreSistema { get; set; }

        public string VersionSistema { get; set; }

        public string PathArchivoConfig { get; set; }
        public string PathArchivoEXE { get; set; }

        public string NombrePaquete { get; set; }

        public string PathDestinoPaquete { get; set; }
    }

    public class MonitorDeApps
    {
        public int IdMonitor { get; set; }

        public string Ruta { get; set; }

        public string NombreEXE { get; set; }

        public string NombreProceso { get; set; }
        public bool Activo { get; set; }

    }
}
