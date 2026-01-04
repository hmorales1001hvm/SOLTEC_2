using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;

namespace SOLTEC.SPOS.Negocio.Sincronizacion
{
    public class ControlEnvio
    {
        public HashSet<string> RegistrosEnviados { get; set; } = new HashSet<string>();
    }

    public class ControlEnvioManager
    {
        private readonly string _path;
        private ControlEnvio _control;
        private const int DIAS_ACTIVOS = 8;
        private const string CARPETA_HISTORICO = "Historico";

        // Diccionario global de locks por archivo
        private static readonly Dictionary<string, object> _locksGlobales = new Dictionary<string, object>();

        public ControlEnvioManager(string path)
        {
            _path = path;
            _control = Cargar();
        }

        private void RotarYDepurar()
        {
            DateTime fechaLimite = DateTime.Today.AddDays(-DIAS_ACTIVOS);

            var activos = new HashSet<string>();
            var historicos = new List<string>();

            foreach (var r in _control.RegistrosEnviados)
            {
                var fecha = ExtraerFecha(r);
                if (!fecha.HasValue)
                    continue;

                if (fecha.Value.Date >= fechaLimite)
                    activos.Add(r);
                else
                    historicos.Add(r);
            }

            // Guardar históricos
            GuardarHistorico(historicos);

            // Reemplazar activos
            _control.RegistrosEnviados = activos;
        }


        private void GuardarHistorico(List<string> historicos)
        {
            if (!historicos.Any())
                return;

            string basePath = Path.GetDirectoryName(_path);
            string carpeta = Path.Combine(basePath, CARPETA_HISTORICO);

            Directory.CreateDirectory(carpeta);

            var grupos = historicos
                .Select(r => new { Registro = r, Fecha = ExtraerFecha(r) })
                .Where(x => x.Fecha.HasValue)
                .GroupBy(x => x.Fecha.Value.Date);

            foreach (var grupo in grupos)
            {
                string archivo = Path.Combine(
                    carpeta,
                    $"control_envio_{grupo.Key:yyyy-MM-dd}.json");

                HashSet<string> existentes = new HashSet<string>();

                if (File.Exists(archivo))
                {
                    var json = File.ReadAllText(archivo);
                    var ce = JsonConvert.DeserializeObject<ControlEnvio>(json);
                    if (ce != null)
                        existentes = ce.RegistrosEnviados;
                }

                foreach (var r in grupo.Select(g => g.Registro))
                    existentes.Add(r);

                var salida = new ControlEnvio { RegistrosEnviados = existentes };
                File.WriteAllText(archivo, JsonConvert.SerializeObject(salida, Formatting.Indented));
            }
        }

        private DateTime? ExtraerFecha(string registro)
        {
            foreach (var parte in registro.Split('|'))
            {
                if (parte.StartsWith("FechaOperacion=", StringComparison.OrdinalIgnoreCase) ||
                    parte.StartsWith("fechaOperacion=", StringComparison.OrdinalIgnoreCase))
                {
                    var valor = parte.Split('=')[1];

                    if (DateTime.TryParse(valor, out DateTime fecha))
                        return fecha;
                }
            }
            return null;
        }



        /// <summary>
        /// Obtiene un lock único por archivo
        /// </summary>
        private object GetLockObject()
        {
            lock (_locksGlobales)
            {
                if (!_locksGlobales.ContainsKey(_path))
                    _locksGlobales[_path] = new object();

                return _locksGlobales[_path];
            }
        }

        /// <summary>
        /// Expone registros de forma segura
        /// </summary>
        public IEnumerable<string> RegistrosEnviados => _control.RegistrosEnviados.ToList();

        /// <summary>
        /// Cargar archivo JSON
        /// </summary>
        public ControlEnvio Cargar()
        {
            lock (GetLockObject())
            {
                if (!File.Exists(_path))
                {
                    var nuevo = new ControlEnvio();
                    Guardar(nuevo);
                    return nuevo;
                }

                using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<ControlEnvio>(json) ?? new ControlEnvio();
                }
            }
        }

        /// <summary>
        /// Guardar JSON en disco
        /// </summary>
        public void Guardar(ControlEnvio control = null)
        {
            lock (GetLockObject())
            {
                if (control != null)
                    _control = control;

                RotarYDepurar();

                var json = JsonConvert.SerializeObject(_control, Formatting.Indented);

                using (var fs = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write(json);
                }
            }
        }


        public void GuardarCambios()
        {
            Guardar(_control);
        }

        public bool ExisteRegistro(string clave)
        {
            lock (GetLockObject())
            {
                return _control.RegistrosEnviados.Contains(clave);
            }
        }

        public void Registrar(string clave)
        {
            lock (GetLockObject())
            {
                _control.RegistrosEnviados.Add(clave);
            }
        }

        public void Eliminar(string clave)
        {
            lock (GetLockObject())
            {
                if (_control.RegistrosEnviados.Contains(clave))
                    _control.RegistrosEnviados.Remove(clave);
            }
        }

        /// <summary>
        /// Construir clave incremental, usando un objeto dinámico
        /// </summary>
        public string ConstruirClave(string tabla, dynamic fila, string[] columnas)
        {
            var partes = new List<string> { tabla };
            var dict = (IDictionary<string, object>)fila;

            foreach (var col in columnas)
            {
                var valor = dict.ContainsKey(col) ? dict[col] : null;
                partes.Add($"{col}={valor}");
            }

            return string.Join("|", partes);
        }
    }
}
