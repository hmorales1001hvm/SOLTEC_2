using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Soltec.Common.LoggerFramework;
using SOLTEC.SPOS.Modelo.Entidades;
using SOLTEC.SPOS.Negocio.Sincronizacion;
using SOLTEC.SPOS.Orquestador.Sincronizacion;

namespace SOLTEC.SPOS.Orquestador.Sincronizacion
{
    public class BackGroundService
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly List<Task> _backgroundTasks = new List<Task>();
        private readonly List<CancellationTokenSource> _cancellationTokens = new List<CancellationTokenSource>();

        // Control de tareas activas por nombre
        private readonly Dictionary<string, TareaActiva> _tareasActivas = new Dictionary<string, TareaActiva>();

        private Sincronizador _sincronizador;
        private string _urls = string.Empty;
        private string _rutaSucursalObtenida = string.Empty;
        private string _rutaActualizador = string.Empty;

        public void Start()
        {
            Task.Run(() => RunAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts.Cancel();

            foreach (var cts in _cancellationTokens)
            {
                cts.Cancel();
            }

            try
            {
                Task.WaitAll(_backgroundTasks.ToArray(), TimeSpan.FromSeconds(20));
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                {
                    Logger.Error(inner);
                }
            }
        }

        private async Task RunAsync(CancellationToken token)
        {
            _urls = Settings1.Default.Urls;
            _rutaSucursalObtenida = Settings1.Default.RutaSucursalObtenida;
            _rutaActualizador = Settings1.Default.RutaActualizador;

            //_urls = "https://localhost:7273/api";
            //_urls = "http://trasmision.itsoltec.com:8083/api";
            _sincronizador = new Sincronizador(_urls, _rutaSucursalObtenida, _rutaActualizador);

            // Tarea principal programada cada X minutos
            await TareaLanzamientoAsync(IniciaProcesoDeSincronizacion,
                                        "Scripts en Linea",
                                        Settings1.Default.TiempoEjecutaAplicacionDefault,
                                        token);
        }

        private async Task TareaLanzamientoAsync(Func<CancellationToken, Task> taskFunc, string taskName, int intervaloMinutos, CancellationToken parentToken)
        {
            if (_tareasActivas.ContainsKey(taskName))
            {
                var tareaExistente = _tareasActivas[taskName];
                var tiempoMaximo = TimeSpan.FromMinutes(20); // 20 min máximo para esperar 

                if (DateTime.Now - tareaExistente.FechaInicio > tiempoMaximo)
                {
                    Logger.Important($"La tarea '{taskName}' lleva demasiado tiempo activa, se forzará su cancelación.");
                    tareaExistente.TokenSource.Cancel();
                    _tareasActivas.Remove(taskName);
                }
                else if (tareaExistente.IntervaloMinutos == intervaloMinutos)
                {
                    Logger.Info($"La tarea '{taskName}' ya está en ejecución con el mismo intervalo ({intervaloMinutos} min).");
                    return;
                }
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            _cancellationTokens.Add(cts);
            var token = cts.Token;

            var task = Task.Run(async () =>
            {
                try
                {
                    Logger.Important($"Iniciando tarea: {taskName}");

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            //if (HorarioPermitido())
                            await taskFunc(token);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error en tarea '{taskName}': {ex.Message}");
                        }

                        try
                        {
                            await Task.Delay(TimeSpan.FromMinutes(intervaloMinutos), token);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    Logger.Important($"Tarea '{taskName}' finalizada.");
                    _tareasActivas.Remove(taskName);
                }

            }, token);

            _tareasActivas[taskName] = new TareaActiva
            {
                Task = task,
                TokenSource = cts,
                IntervaloMinutos = intervaloMinutos,
                FechaInicio = DateTime.Now
            };

            _backgroundTasks.Add(task);
            await Task.CompletedTask;
        }

        private async Task IniciaProcesoDeSincronizacion(CancellationToken token)
        {
            int contador = 1;
            while (contador <= 50)
            {
                var listaDeScripts = await _sincronizador.ObtieneScripts(contador);
                if (listaDeScripts != null)
                {
                    _sincronizador.HistoricoEnviado = false;
                    if (listaDeScripts.Success)
                    {
                        Logger.Info($"Se encontraron {listaDeScripts.Count} scripts");
                        if (listaDeScripts?.List == null || listaDeScripts.List.Count == 0)
                            return;

                        if (listaDeScripts.List.FirstOrDefault().TicketsFaltantes == "SI")
                        {
                            try
                            {
                                string pathControl = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\control_envio.json";
                                if (System.IO.File.Exists(pathControl))
                                    System.IO.File.Delete(pathControl);
                            }
                            catch ( Exception ex) { Logger.Error("No se pudo eliminar el archivo de control."); }
                        }

                        foreach (var script in listaDeScripts.List)
                        {
                            if (script == null) continue;

                            int intervalo = script.TiempoTransmision > 0
                                ? script.TiempoTransmision
                                : Settings1.Default.TiempoEjecutaAplicacionDefault;
                            Logger.Info($"Ejecutando script: {script.Nombre} cada {script.TiempoTransmision} minuto(s)");
                            // Controla que no se ejecute más de una vez el mismo script
                            await TareaLanzamientoAsync(
                                ct => ProcesarScriptAsync(ct, script),
                                script.Nombre+"_"+script.TipoCarga,
                                intervalo,
                                token
                            );
                        }
                    }
                    contador = 51;
                    break;
                }
                else
                {
                    Logger.Error(listaDeScripts.Message);
                }
                contador++;
            }
        }

        private async Task ProcesarScriptAsync(CancellationToken token, SPOS_SQLScripts script)
        {
            try
            {
                string accessKey = Settings1.Default.AccessKey.ToString();
                string secretKey = Settings1.Default.SecretKey.ToString();
                string region = Settings1.Default.Region.ToString();
                string queueUrl = Settings1.Default.QueueUrl.ToString();

                Logger.Important($"Ejecutando script: {script.Nombre}");
                if (script != null && string.IsNullOrEmpty(script.ScriptTable) && script.MultiplesTablas)
                {
                    await _sincronizador.ProcesaScriptAsync(script);
                }
                else if (script.Tipo.Split('|')[0] == "SQS")
                {
                    if (!string.IsNullOrEmpty(script.UrlSQS))
                        await _sincronizador.ProcesaSQS_AWS(script, accessKey, secretKey, region, (string.IsNullOrEmpty(script.UrlSQS) ? queueUrl : script.UrlSQS));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error al procesar script '{script.Nombre}': {ex.Message}");
            }
        }

        private bool HorarioPermitido()
        {
            var ahora = DateTime.Now.TimeOfDay;
            var inicio = new TimeSpan(6, 0, 0); // 6:00 AM
            var fin = new TimeSpan(23, 59, 0);  // 11:59 PM
            return ahora >= inicio && ahora <= fin;
        }
    }

    public class TareaActiva
    {
        public Task Task { get; set; }
        public CancellationTokenSource TokenSource { get; set; }
        public int IntervaloMinutos { get; set; }
        public DateTime FechaInicio { get; set; }
    }
}
