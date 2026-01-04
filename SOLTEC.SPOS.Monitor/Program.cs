using Soltec.Common.LoggerFramework;
using SOLTEC.SPOS.Modelo.Entidades;
using SOLTEC.SPOS.Negocio.Sincronizacion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace SOLTEC.SPOS.Monitor
{
    class Program
    {
        private static Sincronizador _sincronizador;
        private static string _urls = string.Empty;
        private static string _rutaSucursalObtenida = string.Empty;
        private static string _rutaActualizador = string.Empty;

        static async Task Main(string[] args)
        {
            FileUtil.localLogPath = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\Logs\";
            try
            {
                Logger.Info($"Iniciando ejecución del MONITOR");
                _urls = Settings1.Default.Urls;
                _rutaSucursalObtenida = Settings1.Default.RutaSucursalObtenida;
                _rutaActualizador = Settings1.Default.RutaActualizador;

                //_urls = "http://trasmision.itsoltec.com:8083/api";

                try
                {
                    EliminarServicio("Soltec_OnLineSales");
                    EliminarServicio("Soltec_ServiceSQLite");
                    EliminarServicio("Soltec_AppTransmision");

                    if (Directory.Exists(@"C:\Sfspos\AppOnLineSales"))
                        Directory.Delete(@"C:\Sfspos\AppOnLineSales", recursive: true);
                    if (Directory.Exists(@"C:\Sfspos\serviceApp"))
                        Directory.Delete(@"C:\Sfspos\serviceApp", recursive: true);
                    if (Directory.Exists(@"C:\Sfspos\serviceUpdate"))
                        Directory.Delete(@"C:\Sfspos\serviceUpdate", recursive: true);
                    if (Directory.Exists(@"C:\Sfspos\Soltec.WindowsServiceApp"))
                        Directory.Delete(@"C:\Sfspos\Soltec.WindowsServiceApp", recursive: true);
                    if (Directory.Exists(@"C:\Sfspos\Soltec.WindowsServiceOnLine"))
                        Directory.Delete(@"C:\Sfspos\Soltec.WindowsServiceOnLine", recursive: true);
                    if (Directory.Exists(@"C:\Sfspos\Soltec.WindowsServiceSQLite"))
                        Directory.Delete(@"C:\Sfspos\Soltec.WindowsServiceSQLite", recursive: true);
                   
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al eliminar los servicios de windows: {ex.Message}");
                }


                _sincronizador = new Sincronizador(_urls, _rutaSucursalObtenida, _rutaActualizador);
                var versionesApp = await _sincronizador.IniciaEjecucionMonitor();
                if (versionesApp != null)
                {
                    if (versionesApp.Count > 0)
                        await _sincronizador.ActualizaVersiones(versionesApp);
                }


                try
                {
                    await _sincronizador.EjecutaOrquestador();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al abrir su archivo: C:\\Sfspos\\Orquestador\\SOLTEC.SPOS.Orquestador\\SOLTEC.SPOS.Orquestador.exe, error: {ex.Message}");
                }

                try
                {
                    await CreaServicioMonitor(@"C:\Sfspos\Orquestador\SOLTEC.SPOS.ServicioMonitor\SOLTEC.SPOS.ServicioMonitor.exe");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error al crear y/o actualizar el servicio MONITOR, error: {ex.Message}");
                }

                try
                {
                    await _sincronizador.EjecutaSFS();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al abrir SFS");
                }

                // Se auto actualiza en caso de existir una nueva versión
                try
                {
                    Logger.Info("Iniciando proceso de auto actualización.");
                    await AutoActualizar();
                    Logger.Info("Termina proceso de auto actualización.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al auto actualizarse el Monitor. Error: {ex.Message}");
                }

                Logger.Info($"Termina ejecución del MONITOR");
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error en la aplicacion {ex.Message}");
            }
        }


        public async static Task AutoActualizar()
        {
            string zip = @"C:\Sfspos\Orquestador\Actualizador\SOLTEC.SPOS.Monitor.zip";
            if (File.Exists(zip))
            {
                // Detiene servicio para que no abra el ejecutable actual.
                await DetenerServicio("Soltec_ServicioMonitor");

                string rutaUpdater = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\SOLTEC.SPOS.Updater.exe"; 
                // Lanzar el updater
                string exeActual = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\SOLTEC.SPOS.ServicioMonitor.exe"; 
                string carpetaDestino = @"C:\Sfspos\Orquestador";

                Process.Start(new ProcessStartInfo
                {
                    FileName = rutaUpdater,
                    Arguments = $"\"{zip}\" \"{carpetaDestino}\" \"{exeActual}\"",
                    UseShellExecute = false
                });

                // Salir para permitir reemplazo
                Environment.Exit(0);
            }
        }

        public async static Task DetenerServicio(string nombre)
        {
            try
            {
                ServiceController servicio = new ServiceController(nombre);

                if (servicio.Status != ServiceControllerStatus.Stopped &&
                    servicio.Status != ServiceControllerStatus.StopPending)
                {
                    Logger.Info($"Deteniendo servicio: {nombre}...");
                    servicio.Stop();
                    servicio.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                    Logger.Info("Servicio detenido.");
                }
            }
            catch (InvalidOperationException ex)
            {
                Logger.Info($"Servicio '{nombre}' no encontrado o no se puede acceder: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al detener el servicio: {ex.Message}");
            }
        }


        /// <summary>
        /// Servicio para crear o reiniciar el Servicio Monitor.
        /// </summary>
        /// <param name="rutaServicioMonitor"></param>
        /// <returns></returns>
        public async static Task<bool> CreaServicioMonitor(string rutaServicioMonitor)
        {
            var serviceName = "Soltec_ServicioMonitor";

            if (!File.Exists(rutaServicioMonitor))
            {
                Logger.Error($"El archivo del servicio no existe: {rutaServicioMonitor}");
                return false;
            }

            try
            {
                string exeName = Path.GetFileNameWithoutExtension(rutaServicioMonitor);
                var procesos = Process.GetProcessesByName(exeName);

                var servicio = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == serviceName);
                if (servicio == null)
                {
                    Logger.Warning($"El servicio {serviceName} no existe. Creándolo...");
                    await EjecutarComandoAsync($"create {serviceName} binPath= \"{rutaServicioMonitor}\" start= auto");
                    Logger.Important($"✅ Servicio {serviceName} creado correctamente.");

                    await EjecutarComandoAsync($"start {serviceName}");
                    Logger.Important($"✅ Servicio {serviceName} iniciado correctamente.");
                    return true;
                }

                servicio.Refresh();
                Logger.Info($"Estado actual del servicio {serviceName}: {servicio.Status}");

                if (servicio.Status == ServiceControllerStatus.Stopped)
                {
                    Logger.Info($"El servicio {serviceName} está detenido. Iniciando...");
                    servicio.Start();
                    servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    Logger.Important($"Servicio {serviceName} iniciado correctamente.");
                }
                else if (servicio.Status == ServiceControllerStatus.StartPending || servicio.Status == ServiceControllerStatus.StopPending)
                {
                    Logger.Warning($"El servicio {serviceName} está en estado {servicio.Status}. Esperando 10 segundos...");
                    await Task.Delay(10000);
                    servicio.Refresh();

                    if (servicio.Status != ServiceControllerStatus.Running)
                    {
                        Logger.Warning($"El servicio no respondió. Intentando reinicio forzado...");
                        await EjecutarComandoAsync($"stop {serviceName}");
                        await Task.Delay(3000);
                        await EjecutarComandoAsync($"start {serviceName}");
                        Logger.Important($"Reinicio forzado del servicio {serviceName} ejecutado.");
                    }
                }
                else
                {
                    Logger.Warning($"Estado inesperado del servicio: {servicio.Status}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"❌ Error al gestionar el servicio {serviceName}: {ex.Message}");
                return false;
            }

            return true;
        }


        static async Task EjecutarComandoAsync(string argumentos)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "sc",
                Arguments = argumentos,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ejecutando '{argumentos}': {ex.Message}");
            }
        }




        public static void EliminarServicio(string nombreServicio)
        {
            try
            {
                ServiceController servicio = new ServiceController(nombreServicio);
                try
                {
                    // Detener el servicio si está ejecutándose
                    if (servicio.Status != ServiceControllerStatus.Stopped &&
                        servicio.Status != ServiceControllerStatus.StopPending)
                    {
                        Logger.Info($"Deteniendo servicio: {nombreServicio}...");
                        servicio.Stop();
                        servicio.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                    }
                }
                catch (InvalidOperationException)
                {
                    //Logger.Info($"Servicio '{nombreServicio}' no existe o no puede ser accedido.");
                    return;
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"delete \"{nombreServicio}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Verb = "runas" // Ejecutar como administrador
                };

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    Logger.Info($"Resultado de eliminar '{nombreServicio}':\n{output}");
                }
            }
            catch (Exception ex)
            {
                Logger.Info($"Error eliminando servicio '{nombreServicio}': {ex.Message}");
            }
        }
    }
}

