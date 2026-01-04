using Microsoft.Extensions.Configuration;
using Soltec.Common.LoggerFramework;
using System.Diagnostics;
using System.Reflection;

namespace Soltec.WindowsServiceApp
{
    public class Worker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private static int _tiempoEjecutaAplicacionDefault;

        public Worker(IConfiguration configuration)
        {
            _configuration = configuration;
            _tiempoEjecutaAplicacionDefault = Convert.ToInt32(_configuration["AppConfig:TiempoEjecutaAplicacionDefault"]);
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            FileUtil.localLogPath = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.ServicioMonitor\Logs\";
            Logger.Important($"Iniciando Servicio Monitor - {Assembly.GetExecutingAssembly().GetName().Version}");

            while (!cancellationToken.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;
                try
                {
                    Logger.Important($"Ejecutando aplicación: SOLTEC.SPOS.Monitor.exe ");
                    await AbrirAplicacion(@"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\SOLTEC.SPOS.Monitor.exe", "SOLTEC.SPOS.Monitor");
                    await Task.Delay(TimeSpan.FromMinutes(_tiempoEjecutaAplicacionDefault), cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al ejecutar el servicio de windows: {ex.Message}");
                    continue;
                }
            }
        }

    
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }
        static bool ProcesoEnEjecucion(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }
        
        public async static Task<bool> AbrirAplicacion(string rutaAplicacion, string nombreDelProceso)
        {
            Logger.Info($"Ejecutando aplicación {rutaAplicacion}.");

            if (ProcesoEnEjecucion(nombreDelProceso))
            {
                Logger.Info($"La aplicación {rutaAplicacion} ya está en ejecución.");
                return true;
            }
            if (!System.IO.File.Exists(rutaAplicacion))
            {
                Logger.Warning($"No se encontró la aplicación {rutaAplicacion}");
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = rutaAplicacion,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };

                Process.Start(psi);
                Logger.Info($"Aplicación iniciada en modo administrador {rutaAplicacion}.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error al iniciar la aplicación: {rutaAplicacion} \n{ex.Message}");
            }

            return true;
        }

    }
}
