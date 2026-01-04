using Soltec.Common.LoggerFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SOLTEC.SPOS.Updater
{
    internal class Program
    {

        static async Task Main(string[] args)
        {
            FileUtil.localLogPath = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\UpdaterLogs\";
            if (args.Length < 3)
            {
                Logger.Info("Uso: Updater.exe <Zip> <Destino> <ExePrincipal>");
                return;
            }

            string archivoZip = args[0];
            string destino = args[1];
            string exePrincipal = args[2];

            Logger.Info("Esperando que la app principal cierre...");
            Thread.Sleep(1500);

            // Esperar que el ejecutable esté liberado
            while (File.Exists(exePrincipal))
            {
                try
                {
                    using (File.Open(exePrincipal, FileMode.Open, FileAccess.ReadWrite)) { }
                    break;
                }
                catch
                {
                    Thread.Sleep(500);
                }
            }

            try
            {
                // Extraer ZIP temporal
                string tempExtraer = Path.Combine(Path.GetTempPath(), "ActualizacionTMP");
                if (Directory.Exists(tempExtraer))
                    Directory.Delete(tempExtraer, true);

                ZipFile.ExtractToDirectory(archivoZip, tempExtraer);

                foreach (var archivo in Directory.GetFiles(tempExtraer, "*", SearchOption.AllDirectories))
                {
                    string subruta = archivo.Substring(tempExtraer.Length + 1);
                    string destinoFinal = Path.Combine(destino, subruta);

                    string carpeta = Path.GetDirectoryName(destinoFinal);
                    if (Directory.Exists(carpeta))
                    {
                        if (!archivo.Contains("Soltec.Common.LoggerFramework") && !archivo.Contains("SOLTEC.SPOS.Updater"))
                        {
                            File.Copy(archivo, destinoFinal, true);
                            Logger.Info($"Archivo actualizado: {destinoFinal}");
                        }
                    }
                    else
                    {
                        Logger.Info($"Omitido: carpeta no existe para '{destinoFinal}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info($"Error durante la actualización: {ex.Message}");
            }

            try
            {
                // Lanzar app actualizada
                //Process.Start(exePrincipal);
                // Eliminar ZIP usado
                if (File.Exists(archivoZip))
                {
                    try
                    {
                        File.Delete(archivoZip);
                        Logger.Info("ZIP eliminado correctamente.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Info($"Error al eliminar ZIP: {ex.Message}");
                    }
                }

                try
                {
                    await CreaServicioMonitor(@"C:\Sfspos\Orquestador\SOLTEC.SPOS.ServicioMonitor\SOLTEC.SPOS.ServicioMonitor.exe");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error al crear y/o actualizar el servicio MONITOR, error: {ex.Message}");
                }

                Logger.Info("Actualización completada.");

            } catch (Exception ex)
            {
                Logger.Error($"Error al eliminar el archivo .zip");
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

    }
}
