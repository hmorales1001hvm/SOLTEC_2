using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOLTEC.SPOS.Negocio.Sincronizacion
{
    public class ControlOnDemandManager
    {
        private readonly string _path;
        private ControlOnDemand _control;
        private static readonly object _lock = new object();

        public ControlOnDemandManager(string path)
        {
            _path = path;
            _control = Cargar();
        }

        private ControlOnDemand Cargar()
        {
            lock (_lock)
            {
                if (!File.Exists(_path))
                    return new ControlOnDemand();

                try
                {
                    var json = File.ReadAllText(_path);
                    return JsonConvert.DeserializeObject<ControlOnDemand>(json) ?? new ControlOnDemand();
                }
                catch
                {
                    File.Delete(_path);
                    return new ControlOnDemand();
                }
            }
        }

        public bool YaEjecutadoHoy()
            => _control.UltimaEjecucion?.Date == DateTime.Today;

        public void RegistrarEjecucion()
        {
            lock (_lock)
            {
                _control.UltimaEjecucion = DateTime.Today;
                _control.UltimaHora = DateTime.Now.TimeOfDay;

                File.WriteAllText(_path,
                    JsonConvert.SerializeObject(_control, Formatting.Indented));
            }
        }
    }

}
