using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using Soltec.Common.LoggerFramework;
using SOLTEC.SPOS.Datos;
using SOLTEC.SPOS.Modelo.Entidades;
using SOLTEC.SPOS.Negocio.Comun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace SOLTEC.SPOS.Negocio.Sincronizacion
{

    public class Sincronizador
    {
        private static string Sucursal = string.Empty;
        private string RutaSucursalObtenida { get; set; }
        public string Urls { get; set; }
        public string RutaActualizador { get; set; }

        private bool TranmisionInicialExitosa = false;
        private readonly Random random = new Random();
        public bool HistoricoEnviado = false;
        public Sincronizador(string urls, string rutaSucursalObtenida, string rutaActualizador)
        {

            RutaSucursalObtenida = rutaSucursalObtenida;
            Urls = urls;
            GetSucursalServicio();
            RutaActualizador = rutaActualizador;
        }

        
        /// <summary>
        /// Carga la sucursal
        /// </summary>
        void GetSucursalServicio()
        {
            var existSucursalObtenida = System.IO.File.Exists(RutaSucursalObtenida);
            if (!existSucursalObtenida)
            {
                Logger.Info($"El archivo SucursalObtenida.txt NO existe en: {RutaSucursalObtenida}");
                Sucursal = new SOLTEC.SPOS.Datos.SQLDatos().ObtieneSucursal();

                if (!string.IsNullOrEmpty(Sucursal))
                {
                    Logger.Warning($"Guardando sucursal {Sucursal} dentro del archivo SucursalObtenida.txt");
                    System.IO.File.WriteAllText(RutaSucursalObtenida, Sucursal);
                }
            }
            else
            {
                Logger.Info($"El archivo SucursalObtenida.txt SI existe en el directorio {RutaSucursalObtenida} leyendo contenido...");
                Sucursal = System.IO.File.ReadAllText(RutaSucursalObtenida);
            }
        }

        /// <summary>
        /// Obtiene lista de scripts activos.
        /// </summary>
        /// <returns></returns>
        public async Task<ApiResponse<SPOS_SQLScripts>> ObtieneScripts(int contador)
        {
            
            Logger.Important($"Obteniendo scripts para la sucursal: {Sucursal}, intento No. {contador}".ToUpper());

            var apiResponse = new ApiResponse<SPOS_SQLScripts>();
            try
            {
                var url = await ObtieneUrlActiva();

                if (!string.IsNullOrEmpty(url))
                {
                    var apiUrl = $"{url}/venta/ObtieneScriptsConCargaInicial/{Sucursal}";
                    HttpResponseMessage response;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Clear();
                        response = client.GetAsync(apiUrl).Result;
                        response.EnsureSuccessStatusCode();

                        if ((int)response.StatusCode == 400)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        else if ((int)response.StatusCode == 503)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        else if ((int)response.StatusCode == 401)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }

                        apiResponse = JsonConvert.DeserializeObject<ApiResponse<SPOS_SQLScripts>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    }
                }
                else
                {
                    Logger.Warning("No se encontraron servicios disponibles por el momento.");
                }

                return apiResponse;
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrió un error: GetSPOS_SQLScripts.\n{ex.Message}");
                return apiResponse;
            }
        }

        public async Task ProcesaScriptAsync(SPOS_SQLScripts script)
        {
            try
            {
                // Ruta del control incremental
                string pathControl = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\control_envio.json";

                // Control de avance
                var controlEnvio = new ControlEnvioManager(pathControl);

                // Definición de claves principales por tabla
                var clavesPorTabla = new Dictionary<string, string[]>
                                                    {
                                                        { "Ventas", new[] { "Id_Venta", "FechaOperacion" } },
                                                        { "VentasDesgloceTotales", new[] { "Id_Venta", "FechaOperacion" } },
                                                        { "VentasImportesProductos", new[] { "Id_Venta", "Id_Producto", "FechaOperacion" } },
                                                        { "VentasImpuestos", new[] { "Id_Venta", "FechaOperacion", "Impuesto", "TipoFactor" } },
                                                        { "VentasImpuestosDetalle", new[] { "Id_Venta", "FechaOperacion", "Id_Producto", "Impuesto" } },
                                                        { "VentasProductos", new[] { "Id_Venta", "Codigo", "FechaOperacion", "Descuento", "Cantidad", "DescuentoPorciento", "TipoOperacion", "IVA_Importe" } },
                                                        { "VentasVendedorCuotas", new[] { "Fecha", "IdVendedor", "ImporteVenta", "PorcVenta", "ImporteNaturistas", "PorcNaturistas", "MontoDescuento" } }
                                                    };

                // Procesamiento de tu script SQL original
                string rutaCargaInicial = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\CargaInicial.txt";
                string rango = string.Empty, fechaInicial = string.Empty, fechaFinal = string.Empty;
                string sqlScript = script.SQLScript;
                var horaInicial = string.Empty;
                var horaFinal = string.Empty;

                if (!script.ConTransmisionInicial)
                {
                    if (!TranmisionInicialExitosa)
                    {
                        if (File.Exists(rutaCargaInicial))
                        {
                            rango = File.ReadAllText(rutaCargaInicial);
                            fechaInicial = rango.Split('|')[0];
                            fechaFinal = rango.Split('|')[1];
                            sqlScript = sqlScript.Replace("Param1", fechaInicial).Replace("Param2", fechaFinal);
                        }
                        else
                        {
                            sqlScript = sqlScript.Replace("Param1", script.fechaInicial).Replace("Param2", script.fechaFinal);
                            script.ConTransmisionInicial = true;
                        }
                    }
                    else
                    {
                        sqlScript = sqlScript.Replace("Param1", script.fechaInicial).Replace("Param2", script.fechaFinal);
                    }
                }
                else
                {
                    if (script.TipoCarga=="NORMAL" || script.TipoCarga==null)
                        sqlScript = sqlScript.Replace("Param1", script.fechaInicial).Replace("Param2", script.fechaFinal);
                    else if (script.TipoCarga=="HISTORICO")
                    {
                        horaInicial = script.fechaInicial.Split(' ')[1];
                        horaFinal = script.fechaFinal.Split(' ')[1];

                        sqlScript = sqlScript.Replace("Param1", script.fechaInicial.Split(' ')[0])
                                             .Replace("Param2", script.fechaFinal.Split(' ')[0]);

                        var hi = TimeSpan.Parse(horaInicial);
                        var hf = TimeSpan.Parse(horaFinal);

                        Logger.Info($"Validando rango {hi} - {hf} para HISTORICOS...");

                        bool puedeEjecutar = await EsperarHastaRangoAsync(hi, hf);
                        if (HistoricoEnviado)
                            puedeEjecutar = false;

                        if (!puedeEjecutar)
                        {
                            Logger.Warning($"HISTORICOS no ejecutado porque ya pasó la hora final {hf}");
                            return; 
                        }
                    }
                    else if (script.TipoCarga == "ONDEMAND")
                    {
                        horaInicial = script.fechaInicial;
                        horaFinal = script.fechaFinal;

                        sqlScript = sqlScript.Replace("Param1", script.fechaInicial)
                                             .Replace("Param2", script.fechaFinal);

                        var hi = TimeSpan.Parse(horaInicial);
                        var hf = TimeSpan.Parse(horaFinal);

                        Logger.Info($"Validando rango {hi} - {hf} para ONDEMAND...");
                        bool puedeEjecutar = await PuedeEjecutarOnDemandAsync(hi, hf);

                        if (!puedeEjecutar)
                        {
                            Logger.Warning("ONDEMAND no ejecutado: fuera de horario o ya ejecutado hoy.");
                            return;
                        }
                    }

                }

                // Ejecutar SQL
                var data = await new SQLDatos().ObtieneSetDeDatos(sqlScript);
                bool withData = false;
                if (script.TipoCarga == "NORMAL" || script.TipoCarga == null)
                {
                    // FILTRO INCREMENTAL
                    foreach (var tabla in data.Keys.ToList())
                    {
                        if (!clavesPorTabla.ContainsKey(tabla))
                            continue;

                        var columnasClave = clavesPorTabla[tabla];
                        var lista = data[tabla] as IEnumerable<dynamic>;
                        if (lista == null)
                            continue;

                        var listaFiltrada = new List<dynamic>();

                        foreach (var fila in lista)
                        {
                            string clave = controlEnvio.ConstruirClave(tabla, fila, columnasClave);

                            if (!controlEnvio.ExisteRegistro(clave))
                                listaFiltrada.Add(fila);
                        }

                        data[tabla] = listaFiltrada;
                    }

                    withData = data.Any(t => t.Value is IEnumerable<dynamic> list && list.Any());

                    if (!withData)
                    {
                        Logger.Info("No hay información nueva para enviar, solo se actualizará la última transmisión.");
                        var response = await  ActualizaSucursalTransmisionAsync(script);
                        if (response.Success)
                        {
                            Logger.Info("Se actualizó correctamente la sucursal con la ultima transmisión.");
                        }
                        return;
                    }
                } else if (script.TipoCarga == "HISTORICO")
                {
                    withData = data.Any(t => t.Value is IEnumerable<dynamic> list && list.Any());

                    if (!withData)
                    {
                        Logger.Warning("HISTORICO no contiene datos para enviar. No se ejecutará.");
                        return;
                    }
                }
                else if (script.TipoCarga == "ONDEMAND")
                {
                    withData = data.Any(t => t.Value is IEnumerable<dynamic> list && list.Any());

                    if (!withData)
                    {
                        Logger.Warning("ONDEMAND no contiene datos para enviar. No se ejecutará.");
                        return;
                    }
                }



                var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);

                
                if (script.TipoCarga == "NORMAL" || script.TipoCarga == null)
                {
                    var response = await SincronizaScriptAsync(jsonString, script, withData, script.ConTransmisionInicial);

                    if (response.Success)
                    {
                        Logger.Info($"El proceso {script.Nombre} se ejecutó correctamente: {Sucursal}");

                
                        foreach (var tabla in data.Keys)
                        {
                            if (!clavesPorTabla.ContainsKey(tabla))
                                continue;

                            var columnas = clavesPorTabla[tabla];
                            var lista = data[tabla] as IEnumerable<dynamic>;

                            if (lista == null) continue;

                            foreach (var fila in lista)
                            {
                                string clave = controlEnvio.ConstruirClave(tabla, fila, columnas);
                                controlEnvio.Registrar(clave);
                            }
                        }

                        controlEnvio.GuardarCambios();
                    }
                    else
                    {
                        Logger.Warning($"Proceso {script.Nombre} NO se ejecutó correctamente: {Sucursal}, {response.Message}");
                    }
                }
                else if (script.TipoCarga == "HISTORICO")
                {
                    if (!HistoricoEnviado)
                    {
                        var zipBytes = ComprimirJsonYInfoEnZip(jsonString, script, withData, script.ConTransmisionInicial, Sucursal);

                        // Enviar ZIP a la API HISTORICO
                        var response = await SincronizaScriptZipAsync(zipBytes, script, withData, script.ConTransmisionInicial);

                        if (response.Success)
                        {
                            HistoricoEnviado = true;
                            Logger.Info($"HISTORICO: El proceso {script.Nombre} se ejecutó correctamente y se envió ZIP: {Sucursal}");
                            // Registrar como enviado...
                        }
                        else
                        {
                            Logger.Warning($"HISTORICO: Proceso {script.Nombre} NO se ejecutó correctamente: {Sucursal}, {response.Message}");
                        }
                    }
                }
                else if (script.TipoCarga == "ONDEMAND")
                {
                    var zipBytes = ComprimirJsonYInfoEnZip(jsonString, script, withData, script.ConTransmisionInicial, Sucursal);
                    var response = await SincronizaScriptZipAsync(zipBytes, script, withData, script.ConTransmisionInicial);
                    if (response.Success)
                    {
                        var controlOnDemand = new ControlOnDemandManager(
                            @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\control_ondemand.json");

                        controlOnDemand.RegistrarEjecucion();

                        Logger.Info($"ONDEMAND: El proceso {script.Nombre} se ejecutó correctamente y se envió ZIP: {Sucursal}");
                    }
                    else
                    {
                        Logger.Warning($"ONDEMAND: El proceso {script.Nombre} no se ejecutó.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrió un error: {ex.Message}");
            }
        }


        public byte[] ComprimirJsonYInfoEnZip(string jsonData, SPOS_SQLScripts script, bool conDatos, bool conTransmisionInicial, string sucursal)
        {
            // Crear JSON con info del script
            var infoScript = new
            {
                script.Nombre,
                script.IdSucursal,
                ConTransmisionInicial = conTransmisionInicial,
                ConDatos = conDatos,
                script.MultiFra,
                script.DatabaseName,
                script.HostName,
                script.UserName,
                script.Password,
                Sucursal = sucursal
            };

            string infoScriptJson = JsonConvert.SerializeObject(infoScript);

            using (var memoriaZip = new MemoryStream())
            {
                using (var zip = new System.IO.Compression.ZipArchive(memoriaZip, ZipArchiveMode.Create, true))
                {
                    // Agregar datos
                    var dataEntry = zip.CreateEntry($"{sucursal}_data.json", CompressionLevel.Optimal);
                    using (var entryStream = dataEntry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        writer.Write(jsonData);
                    }

                    // Agregar info del script
                    var infoEntry = zip.CreateEntry($"{sucursal}_infoDB.json", CompressionLevel.Optimal);
                    using (var entryStream = infoEntry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        writer.Write(infoScriptJson);
                    }
                }

                return memoriaZip.ToArray(); // ZIP listo para enviar
            }
        }


        private async Task<bool> PuedeEjecutarOnDemandAsync(TimeSpan hi, TimeSpan hf)
        {
            var ahora = DateTime.Now.TimeOfDay;

            if (ahora < hi || ahora > hf)
                return false;

            var path = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\control_ondemand.json";
            var control = new ControlOnDemandManager(path);

            if (control.YaEjecutadoHoy())
            {
                Logger.Info("ONDEMAND ya fue ejecutado hoy. Esperando al siguiente día.");
                return false;
            }

            return true;
        }


        public async Task<bool> EsperarHastaRangoAsync(TimeSpan horaInicial, TimeSpan horaFinal)
        {
            while (true)
            {
                var ahora = DateTime.Now.TimeOfDay;

                // Si ya se pasó del rango -> no ejecutar
                if (ahora > horaFinal)
                    return false;

                // Si está dentro del rango -> ejecutar
                if (ahora >= horaInicial && ahora <= horaFinal)
                    return true;

                // Si aún no llega la hora inicial -> esperar
                var esperar = horaInicial - ahora;

                var delay = esperar < TimeSpan.FromMinutes(1)
                    ? esperar
                    : TimeSpan.FromMinutes(1);

                await Task.Delay(delay);
            }
        }

        /// <summary>
        /// Sincronización manual
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task ProcesaScriptManualAsync(SPOS_SQLScripts script)
        {
            try
            {
                var rutaCargaInicial = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor\CargaInicial.txt";
                string rango = string.Empty;
                string fechaInicial = string.Empty;
                string fechaFinal = string.Empty;
                string sqlScript = script.SQLScript;

                if (!script.ConTransmisionInicial)
                {
                    if (!TranmisionInicialExitosa)
                    {
                        if (File.Exists(rutaCargaInicial))
                        {
                            rango = System.IO.File.ReadAllText(rutaCargaInicial);
                            fechaInicial = rango.Split('|')[0];
                            fechaFinal = rango.Split('|')[1];
                            sqlScript = sqlScript.Replace("Param1", fechaInicial).Replace("Param2", fechaFinal);
                        }
                        else
                        {
                            sqlScript = sqlScript.Replace("Param1", script.fechaInicial).Replace("Param2", script.fechaFinal);
                            script.ConTransmisionInicial = true;
                        }
                    }
                    else
                    {
                        sqlScript = sqlScript.Replace("Param1", script.fechaInicial).Replace("Param2", script.fechaFinal);
                    }
                }
                else
                {
                    sqlScript = sqlScript.Replace("Param1", script.fechaInicial).Replace("Param2", script.fechaFinal);
                }

                var contador = 0;
                var data = await new SQLDatos().ObtieneSetDeDatos(sqlScript);
                var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                if (data.ToList().Count > 0)
                {
                    bool withData = false;
                    foreach (var table in data)
                    {
                        if (table.Value is IEnumerable<object> lista && lista.Any())
                        {
                            withData = true;
                            break;
                        }
                    }
                    if (script.MultiFra == 0)
                        withData = true;

                    if (withData)
                    {
                        while (contador <=10) 
                        {
                            var response = await SincronizaScriptManualAsync(jsonString, script, withData);
                            if (response)
                            {
                                contador = 100;
                                Logger.Info($"El proceso {script.Nombre} se ejecutó correctamente: {Sucursal}");
                                break;
                            }
                            else
                            {
                                Logger.Warning($"El proceso {script.Nombre} NO se ejecutó correctamente: {Sucursal}, intentando nuevamente.");
                            }
                            contador++;
                        }
                    }
                    else
                        Logger.Info($"No se encontraron registros {script.Nombre} para este script.");
                }
                else
                    Logger.Info($"No se encontraron registros {script.Nombre} para este script.");

            }
            catch (Exception ex)
            {
                throw new Exception($"Se encontró un error al procesar su script SQLite: {ex.Message}");
            }
        }

        public async Task ProcesaSQS_AWS(SPOS_SQLScripts script, string accessKey, string secretKey, string region, string queueUrl)
        {
            try
            {
                object data = null;
                bool conDatos = false;
                var formato = script.Tipo.Split('|')[1];
                Logger.Important($"Obtenido datos para SQS - AWS: {script.Nombre}");

                if (formato == "JSON")
                {
                    data = await new SQLDatos().ObtieneDatos(script.SQLScript);

                }
                else if(formato == "PLANO")
                {
                    data = await new SQLDatos().ObtieneDatosPlano(script.SQLScript);
                    if (data.ToString().Trim() != "")
                        conDatos = true;

                    if (string.IsNullOrEmpty(data.ToString().Trim()))
                        conDatos = false;

                    if (data.ToString().Trim().Length<=5)
                        conDatos = false;
                }

                var jsonString = string.Empty;   
                if (!conDatos)
                {
                    jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                    if (string.IsNullOrWhiteSpace(jsonString) || jsonString == "null" || jsonString == "[]")
                        conDatos = false;
                    else
                        conDatos = true;
                }

                if (conDatos)
                {
                    var sqsClient = new AmazonSQSClient(
                        accessKey,
                        secretKey,
                        RegionEndpoint.GetBySystemName(region)
                    );
                    int sizeInBytes = 0;

                    if (formato == "JSON")
                        sizeInBytes = Encoding.UTF8.GetByteCount(jsonString);
                    else
                        sizeInBytes = Encoding.UTF8.GetByteCount(data.ToString());

                    //262144

                    //53622 esto pesa como archivo plano.
                    //171914 en formato JSON.

                    if (sizeInBytes <= 262144)
                    {
                        string datosEnviar = string.IsNullOrEmpty(jsonString) ? data.ToString() : jsonString;
                        var sendRequest = new SendMessageRequest
                        {
                            QueueUrl = queueUrl,
                            MessageBody = datosEnviar,
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                                        {
                                            { "Sucursal", new MessageAttributeValue { DataType = "String", StringValue = Sucursal } },
                                            { "Nombre", new MessageAttributeValue { DataType = "String", StringValue = script.Nombre } },
                                            { "Formato", new MessageAttributeValue { DataType = "String", StringValue = formato } },
                                            { "Total", new MessageAttributeValue { DataType = "Number", StringValue = "1" } }
                                        }
                        };
                        var sendResponse = await sqsClient.SendMessageAsync(sendRequest);
                        if (sendResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
                            Logger.Info($"Mensaje enviado con ID: {sendResponse.MessageId}");
                        else
                            Logger.Info($"No se pudo procesar su información: {sendResponse.HttpStatusCode}");
                    }
                    else
                    {
                        if (formato == "JSON")
                        {
                            conDatos = false;
                            int count = ((ICollection)data).Count;
                            dynamic dynList = data;
                            var partes = PartirEn(dynList, 500);
                            foreach (var parte in partes)
                            {
                                sizeInBytes = 0;

                                if (formato == "JSON")
                                {
                                    jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(parte);
                                    sizeInBytes = Encoding.UTF8.GetByteCount(jsonString);
                                }
                                else
                                    sizeInBytes = Encoding.UTF8.GetByteCount(data.ToString());

                                if (!conDatos)
                                {
                                    if (string.IsNullOrWhiteSpace(jsonString) || jsonString == "null" || jsonString == "[]")
                                        conDatos = false;
                                    else
                                        conDatos = true;
                                }

                                if (sizeInBytes <= 262144)
                                {
                                    string datosEnviar = string.IsNullOrEmpty(jsonString) ? data.ToString() : jsonString;
                                    var sendRequest = new SendMessageRequest
                                    {
                                        QueueUrl = queueUrl,
                                        MessageBody = jsonString,
                                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                                        {
                                            { "Sucursal", new MessageAttributeValue { DataType = "String", StringValue = Sucursal } },
                                            { "Nombre", new MessageAttributeValue { DataType = "String", StringValue = script.Nombre } },
                                            { "Formato", new MessageAttributeValue { DataType = "String", StringValue = formato } },
                                            { "Total", new MessageAttributeValue { DataType = "Number", StringValue = Math.Ceiling((double)count / 500).ToString() } }
                                        }
                                    };
                                    var sendResponse = await sqsClient.SendMessageAsync(sendRequest);
                                    Logger.Info($"Mensaje enviado con ID: {sendResponse.MessageId}");
                                }

                            }
                        }
                        else
                        {
                            Logger.Info($"La Data excede el tamaño permitido para: {script.Nombre}.");
                        }
                    }
                }
                else
                    Logger.Info($"No se encontraron registros {script.Nombre} para este script.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Se encontró un error al procesar su script - método: ProcesaSQS_AWS.\nError: {ex.Message}");
            }
        }


        public IEnumerable<List<T>> PartirEn<T>(List<T> source, int tamano)
        {
            for (int i = 0; i < source.Count; i += tamano)
            {
                yield return source.GetRange(i, Math.Min(tamano, source.Count - i));
            }
        }



        //public async Task<ApiResponse> ActualizaSucursalTransmisionAsync(SPOS_SQLScripts script)
        //{
        //    var apiResponse = new ApiResponse { Success = false };
        //    try
        //    {
        //        var url = await ObtieneUrlActiva();
        //        if (string.IsNullOrEmpty(url))
        //        {
        //            TranmisionInicialExitosa = false;
        //            Logger.Warning("No se encontraron servicios disponibles.");
        //            return apiResponse;
        //        }

        //        var apiUrl = $"{url}/venta/ActualizaSucursalTransmision";

        //        var ventaEnLinea = new ProcesosOnLine
        //        {
        //            Sucursal = Sucursal,
        //            Json = "{}",
        //            NombreProceso = script.Nombre,
        //            IdSucursal = script.IdSucursal,
        //            Ver1 = "", 
        //            Ver2 = "", 
        //            Ver3 = "", 
        //            Ver4 = "SV",
        //            Ver5 = "SV",
        //            Ver6 = "SV",
        //            ConDatos = false,
        //            MultiFra = script.MultiFra,
        //            DatabaseName = script.DatabaseName,
        //            HostName = script.HostName,
        //            Password = script.Password,
        //            UserName = script.UserName,
        //            ConTransmisionInicial = false,
        //            TicketsFaltantes = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
        //            TipoCarga = script.TipoCarga
        //        };

        //        using (var client = new HttpClient())
        //        {
        //            client.Timeout = TimeSpan.FromMinutes(30);

        //            var content = new StringContent(
        //                JsonConvert.SerializeObject(ventaEnLinea),
        //                Encoding.UTF8,
        //                "application/json"
        //            );

        //            HttpResponseMessage response = await client.PostAsync(apiUrl, content);
        //            string jsonResult = await response.Content.ReadAsStringAsync();

        //            Logger.Info($"Respuesta bruta del API: {jsonResult}");

        //            ApiResponse apiResult = null;

        //            try
        //            {
        //                apiResult = JsonConvert.DeserializeObject<ApiResponse>(jsonResult);
        //            }
        //            catch (Exception ex)
        //            {
        //                Logger.Error($"Error parseando respuesta JSON: {ex.Message}");
        //            }

        //            // Si la API mandó una respuesta válida
        //            if (apiResult != null)
        //            {
        //                TranmisionInicialExitosa = apiResult.Success;
        //                return apiResult;
        //            }

        //            // Si hubo HTTP 500, 503, 400, timeouts o respuesta vacía
        //            TranmisionInicialExitosa = false;
        //            apiResponse.Success = false;
        //            apiResponse.Message = $"Fallo en la API o respuesta inválida. HTTP={response.StatusCode}";
        //            return apiResponse;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error($"Error en ActualizaSucursalTransmision({script.Nombre}): {ex.Message}");
        //        apiResponse.Success = false;
        //        return apiResponse;
        //    }
        //}


        public async Task<ApiResponse> ActualizaSucursalTransmisionAsync(SPOS_SQLScripts script)
        {
            var apiResponse = new ApiResponse { Success = false };

            try
            {
                var url = await ObtieneUrlActiva();
                if (string.IsNullOrEmpty(url))
                {
                    TranmisionInicialExitosa = false;
                    Logger.Warning("No se encontraron servicios disponibles.");
                    return apiResponse;
                }

                var apiUrl = $"{url}/venta/ActualizaSucursalTransmision";

                var ventaEnLinea = new ProcesosOnLine
                {
                    Sucursal = Sucursal, 
                    Json = "{}",
                    NombreProceso = script.Nombre,
                    IdSucursal = script.IdSucursal,
                    Ver1 = "",
                    Ver2 = "",
                    Ver3 = "",
                    Ver4 = "SV",
                    Ver5 = "SV",
                    Ver6 = "SV",
                    ConDatos = false,
                    MultiFra = script.MultiFra,
                    DatabaseName = script.DatabaseName,
                    HostName = script.HostName,
                    Password = script.Password,
                    UserName = script.UserName,
                    ConTransmisionInicial = false,
                    TicketsFaltantes = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    TipoCarga = script.TipoCarga
                };

                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
                {
                    // AGREGAMOS HEADER
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Sucursal", Sucursal);

                    var content = new StringContent(
                        JsonConvert.SerializeObject(ventaEnLinea),
                        Encoding.UTF8,
                        "application/json"
                    );

                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.Error($"HTTP error {response.StatusCode} al llamar a ActualizaSucursalTransmision: {script.Nombre}");
                        apiResponse.Message = $"Fallo en la API: HTTP {response.StatusCode}";
                        return apiResponse;
                    }

                    string jsonResult = await response.Content.ReadAsStringAsync();
                    Logger.Info($"Respuesta bruta del API: {jsonResult}");

                    try
                    {
                        var apiResult = JsonConvert.DeserializeObject<ApiResponse>(jsonResult);
                        if (apiResult != null)
                        {
                            TranmisionInicialExitosa = apiResult.Success;
                            return apiResult;
                        }
                        else
                        {
                            Logger.Warning("Respuesta JSON inválida o vacía.");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Logger.Error($"Error parseando respuesta JSON: {ex.Message}");
                    }

                    // Fallback si no se pudo parsear JSON
                    TranmisionInicialExitosa = false;
                    apiResponse.Message = "Fallo en la API o respuesta inválida.";
                    return apiResponse;
                }

            }
            catch (TaskCanceledException tex) when (!tex.CancellationToken.IsCancellationRequested)
            {
                Logger.Error($"Timeout en ActualizaSucursalTransmision({script.Nombre}): {tex.Message}");
                apiResponse.Message = "Timeout al comunicarse con la API.";
                TranmisionInicialExitosa = false;
                return apiResponse;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en ActualizaSucursalTransmision({script.Nombre}): {ex.Message}");
                TranmisionInicialExitosa = false;
                return apiResponse;
            }
        }




        /// <summary>
        /// Envía información a la base de datos CENTRALIZADA.
        /// </summary>
        /// <param name="json"></param>
        /// <param name="nombre"></param>
        /// <param name="IdSucursal"></param>
        /// <returns></returns>
        public async Task<ApiResponse> SincronizaScriptAsync(string json, SPOS_SQLScripts script, bool conDatos, bool conTransmisionInicial)
        {
            var apiResponse = new ApiResponse { Success = false };

            try
            {
                var url = await ObtieneUrlActiva();
                if (string.IsNullOrEmpty(url))
                {
                    TranmisionInicialExitosa = false;
                    Logger.Warning("No se encontraron servicios disponibles.");
                    return apiResponse;
                }

                var apiUrl = $"{url}/venta/SincronizaScriptUltimo";

                var ventaEnLinea = new ProcesosOnLine
                {
                    Sucursal = Sucursal,
                    Json = json,
                    NombreProceso = script.Nombre,
                    IdSucursal = script.IdSucursal,
                    Ver1 = await ObtieneNumeroDeVersion("C:\\Sfspos\\Orquestador\\SOLTEC.SPOS.Orquestador\\SOLTEC.SPOS.Orquestador.exe"),
                    Ver2 = await ObtieneNumeroDeVersion("C:\\Sfspos\\Orquestador\\SOLTEC.SPOS.Monitor\\SOLTEC.SPOS.Monitor.exe"),
                    Ver3 = await ObtieneNumeroDeVersion("C:\\Sfspos\\Orquestador\\SOLTEC.SPOS.ServicioMonitor\\SOLTEC.SPOS.ServicioMonitor.exe"),
                    Ver4= "SV",
                    Ver5= "SV",
                    Ver6= "SV",
                    ConDatos = conDatos,
                    MultiFra = script.MultiFra,
                    DatabaseName = script.DatabaseName,
                    HostName = script.HostName,
                    Password = script.Password,
                    UserName = script.UserName,
                    ConTransmisionInicial = conTransmisionInicial,
                    TicketsFaltantes = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), 
                    TipoCarga = script.TipoCarga
                };

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30);

                    var content = new StringContent(
                        JsonConvert.SerializeObject(ventaEnLinea),
                        Encoding.UTF8,
                        "application/json"
                    );

                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                    string jsonResult = await response.Content.ReadAsStringAsync();

                    Logger.Info($"Respuesta bruta del API: {jsonResult}");

                    ApiResponse apiResult = null;

                    try
                    {
                        apiResult = JsonConvert.DeserializeObject<ApiResponse>(jsonResult);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error parseando respuesta JSON: {ex.Message}");
                    }

                    // Si la API mandó una respuesta válida
                    if (apiResult != null)
                    {
                        TranmisionInicialExitosa = apiResult.Success;
                        return apiResult;
                    }

                    // Si hubo HTTP 500, 503, 400, timeouts o respuesta vacía
                    TranmisionInicialExitosa = false;
                    apiResponse.Success = false;
                    apiResponse.Message = $"Fallo en la API o respuesta inválida. HTTP={response.StatusCode}";
                    return apiResponse;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en SincronizaScriptAsync({script.Nombre}): {ex.Message}");
                apiResponse.Success = false;
                return apiResponse;
            }
        }


        public async Task<ApiResponse> SincronizaScriptZipAsync(byte[] zipFile,  SPOS_SQLScripts script, bool conDatos, bool conTransmisionInicial)
        {
            var apiResponse = new ApiResponse { Success = false };

            try
            {
                var url = await ObtieneUrlActiva();
                if (string.IsNullOrEmpty(url))
                {
                    Logger.Warning("No se encontraron servicios disponibles para HISTORICO.");
                    return apiResponse;
                }
                
                var apiUrl = $"{url}/venta/SincronizaScriptZipAsync";

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30);

                    // Multipart/form-data para enviar ZIP y Sucursal
                    var content = new MultipartFormDataContent();

                    // Archivo ZIP
                    var fileContent = new ByteArrayContent(zipFile);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                    if (script.TipoCarga== "HISTORICO")
                        content.Add(fileContent, "file", $"{Sucursal}_DatosHistoricos.zip");
                    else if (script.TipoCarga == "ONDEMAND")
                        content.Add(fileContent, "file", $"{Sucursal}_DatosOnDemand.zip");

                    // Agregar Sucursal como parámetro
                    content.Add(new StringContent(Sucursal ?? ""), "Sucursal");

                    // Enviar al endpoint
                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                    string jsonResult = await response.Content.ReadAsStringAsync();

                    Logger.Info($"Respuesta bruta del API HISTORICO: {jsonResult}");

                    ApiResponse apiResult = null;
                    try
                    {
                        apiResult = JsonConvert.DeserializeObject<ApiResponse>(jsonResult);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error parseando respuesta JSON HISTORICO: {ex.Message}");
                    }

                    if (apiResult != null)
                        return apiResult;

                    apiResponse.Success = false;
                    apiResponse.Message = $"Fallo en la API o respuesta inválida. HTTP={response.StatusCode}";
                    return apiResponse;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en SincronizaScriptZipAsync({script.Nombre} - {script.TipoCarga}): {ex.Message}");
                apiResponse.Success = false;
                return apiResponse;
            }
        }




        /// <summary>
        /// Obtiene la URL activa.
        /// </summary>
        /// <returns></returns>
        private async Task<string> ObtieneUrlActiva()
        {
            string urlActiva = string.Empty;
            
            if (Urls.Contains("|"))
            {
                var _urls = Urls.Split('|').ToList();
                _urls = _urls.OrderBy(x => random.Next()).ToList();

                foreach (var url in _urls)
                {
                    try
                    {
                        var isOnline = await ApiEnLinea(url);
                        if (isOnline.Success)
                        {
                            Logger.Important($"El API de la URL: {url} se encuentra activa.");
                            urlActiva = url;
                            break;
                        }
                        else
                        {
                            Logger.Warning($"URL inactiva: {url}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error al verificar URL {url}: {ex.Message}");
                    }
                }
            }
            else
            {
                urlActiva = Urls;
                Logger.Info($"Usando única URL configurada: {urlActiva}");
            }
            
            return urlActiva;
        }

        /// <summary>
        /// Valida si el API de la URL seleccionada está activa.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        //public static async Task<ApiResponse> ApiEnLinea(string url)
        //{
        //    var apiUrl = $"{url}/transmision/isOnline";

        //    var apiResponse = new ApiResponse();
        //    HttpResponseMessage response;

        //    using (var client = new HttpClient())
        //    {
        //        client.DefaultRequestHeaders.Clear();
        //        response = client.GetAsync(apiUrl).Result;
        //        response.EnsureSuccessStatusCode();
        //        if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
        //        {
        //            apiResponse.Success = false;
        //            apiResponse.Message = response.ReasonPhrase;

        //            return apiResponse;
        //        }

        //        apiResponse = JsonConvert.DeserializeObject<ApiResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        //    }

        //    if (!response.IsSuccessStatusCode)
        //        Logger.Error($"No se pudo procesar: IsOnline\n{response.StatusCode}");

        //    return apiResponse;
        //}

        public static async Task<ApiResponse> ApiEnLinea(string url)
        {
            var apiUrl = $"{url}/transmision/isOnline";
            var apiResponse = new ApiResponse();

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Sucursal", Sucursal);

                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                        response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;
                        return apiResponse;
                    }

                    response.EnsureSuccessStatusCode();

                    apiResponse = JsonConvert.DeserializeObject<ApiResponse>(
                        await response.Content.ReadAsStringAsync()
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error consumiendo IsOnline: {ex.Message}");
                apiResponse.Success = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }



        /// <summary>
        /// Obtiene Número de Versión
        /// </summary>
        /// <param name="rutaAplicacion"></param>
        /// <returns></returns>
        private async Task<string> ObtieneNumeroDeVersion(string rutaAplicacion)
        {
            FileVersionInfo versionInfo = null;
            if (System.IO.File.Exists(rutaAplicacion))
                versionInfo = FileVersionInfo.GetVersionInfo(rutaAplicacion);

            return (versionInfo == null) ? "" : versionInfo.FileVersion;
        }

        public async Task<List<VersionesApp>> IniciaEjecucionMonitor()
        {
            var versionesApp = new List<VersionesApp>();
            try
            {
                var url = string.Empty;
                if (!Directory.Exists(RutaActualizador))
                    Directory.CreateDirectory(RutaActualizador);

                if (Directory.Exists(RutaActualizador))
                {
                    string[] archivos = Directory.GetFiles(RutaActualizador);
                    foreach (string archivo in archivos)
                    {
                        if (archivo.ToUpper().EndsWith(".ZIP") || archivo.ToUpper().EndsWith(".BAT"))
                        {
                            System.IO.File.Delete(archivo);
                            Logger.Info($"Archivo eliminado: {archivo}");
                        }
                    }
                }

                url = await ObtieneUrlActiva();
                if (!string.IsNullOrEmpty(url))
                {
                    var apiUrl = $"{url}/transmision/ObtieneVersiones";
                    var apiResponse = new ApiResponse<VersionesApp>();
                    HttpResponseMessage response;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Clear();
                        response = client.GetAsync(apiUrl).Result;
                        response.EnsureSuccessStatusCode();

                        if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;
                        }
                        else
                        {
                            apiResponse = JsonConvert.DeserializeObject<ApiResponse<VersionesApp>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

                            Logger.Info($"Se encontro el siguiente listado de versiones {apiResponse.List.Count}:\n{JsonConvert.SerializeObject(apiResponse.List)}");
                            if (apiResponse.List == null || apiResponse.List.Count == 0)
                            {
                                Logger.Warning("No se logro obtener el listado de versiones las aplicaciones.");
                            }
                            else
                            {
                                foreach (var versionApp in apiResponse.List)
                                {
                                    var actualizarApp = false;
                                    actualizarApp = await RequiereActualizacion(versionApp);

                                    if (actualizarApp)
                                    {
                                        url = await ObtieneUrlActiva();
                                        if (!string.IsNullOrEmpty(url))
                                        {
                                            await DescargaArchivoZIP(url, versionApp.NombrePaquete, versionApp.PathDestinoPaquete);
                                        }
                                    }

                                    else
                                        Logger.Info($"No se requiere descargar version para la aplicacion: {versionApp.NombreSistema}");

                                }
                            }
                        }
                    }

                    versionesApp = apiResponse.List;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error {ex.Message}.");
            }

            return versionesApp;
        }

        public async Task EjecutaSFS()
        {
            try
            {
                var nombreProcesoEjecucion = "sfspos";
                var pathDirectory = @"C:\sfspos\";
                var nombreProcesoActualizador = "sfs.exe";
                var sfsposProceso = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(nombreProcesoEjecucion));
                AbrirAplicacionSFS(@pathDirectory + "" + nombreProcesoActualizador, nombreProcesoEjecucion);

            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrió un error al abrir el SFS.EXE {ex.Message}");
            }
        }

        public async Task EjecutaOrquestador()
        {
            try
            {
                var nombreProcesoEjecucion = "SOLTEC.SPOS.Orquestador";
                var pathDirectory = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Orquestador\";
                var nombreProcesoActualizador = "SOLTEC.SPOS.Orquestador.exe";
                var sfsposProceso = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(nombreProcesoEjecucion));
                AbrirAplicacionSFS(@pathDirectory + "" + nombreProcesoActualizador, nombreProcesoEjecucion);
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrió un error al abrir el SFS.EXE {ex.Message}");
            }
        }


        public async static Task<bool> AbrirAplicacionSFS(string appPath, string nameExe)
        {
            Logger.Info($"Inicia proceso para ejecutar aplicaciones con UI. {appPath}, nombre del proceso_ {nameExe}");

            // Verifica si ya está corriendo
            if (IsProcessRunning(nameExe))
            {
                Logger.Info($"La aplicación SFSPOS se encontraba en ejecución {appPath}.");
            }
            else
            {
                // Verifica que exista el archivo
                if (!System.IO.File.Exists(appPath))
                {
                    Logger.Warning($"❌ No se encontró la aplicación en: " + appPath);
                    return false;
                }

                try
                {
                    if (appPath.ToUpper().Contains("SFS"))
                    {
                        ProcessHandler.CreateProcessAsUser(appPath, "");
                    }
                    else
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = appPath,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            Verb = "runas"
                        };

                        Process.Start(psi);
                        Logger.Info($"✅ Aplicación iniciada en modo administrador {appPath}.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error al iniciar la aplicación SFS.exe: {appPath} \n{ex.Message}");
                }
            }

            return true;
        }

        static bool IsProcessRunning(string processName)
        {
            bool running = false;
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                foreach (var p in processes)
                {
                    if (p.ProcessName == processName)
                        running = true;
                }
                return running;
            }
            else
                return running;
        }


        public static async Task<bool> DescargaArchivoZIP(string url, string file, string pathDestino)
        {
            var result = false;
            try
            {
                Logger.Info($"Iniciando consumo de API para descarga de archivos {file}");
                var apiUrl = $"{url}/venta/DescargaArchivoZIP/{file}";
                var apiResponse = new ApiResponse();
                HttpResponseMessage response;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    response = client.GetAsync(apiUrl).Result;
                    response.EnsureSuccessStatusCode();
                    if (response != null)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            var httpContent = response.Content;

                            using (var newFile = System.IO.File.Create(@pathDestino + @"\" + file))
                            {
                                var stream = await httpContent.ReadAsStreamAsync();
                                await stream.CopyToAsync(newFile);
                            }
                            result = true;
                        }
                        else if ((int)response.StatusCode == 400)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            result = false;
                        }
                        else if ((int)response.StatusCode == 503)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            result = false;
                        }
                        else if ((int)response.StatusCode == 401)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            result = false;
                        }
                    }
                }
                Logger.Info($"Descarga del archivo del API correctamente.  {file}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                result = false;
            }
            return result;
        }

        public async Task<bool> RequiereActualizacion(VersionesApp versionApp)
        {
            Logger.Info($"Comparado número de version para el sistema: {versionApp.NombreSistema}");

            bool isUpdate = false;
            FileVersionInfo versionInfo = null;
            string path = string.Empty;

            if (versionApp.NombreSistema == "SOLTEC.SPOS.Orquestador")
            {
                path = @"C:\\Sfspos\\Orquestador\\SOLTEC.SPOS.Orquestador\\SOLTEC.SPOS.Orquestador.exe";
                if (System.IO.File.Exists(path))
                {
                    versionInfo = FileVersionInfo.GetVersionInfo(path);
                }
            }
            else if (versionApp.NombreSistema == "SOLTEC.SPOS.Monitor")
            {
                path = @"C:\\Sfspos\\Orquestador\\SOLTEC.SPOS.Monitor\\SOLTEC.SPOS.Monitor.exe";
                if (System.IO.File.Exists(path))
                {
                    versionInfo = FileVersionInfo.GetVersionInfo(path);
                }
            }

            if (!Directory.Exists(@"C:\Sfspos\Orquestador\SOLTEC.SPOS.Orquestador"))
                Directory.CreateDirectory(@"C:\Sfspos\Orquestador\SOLTEC.SPOS.Orquestador");
            if (!Directory.Exists(@"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor"))
                Directory.CreateDirectory(@"C:\Sfspos\Orquestador\SOLTEC.SPOS.Monitor");


            var version2 = new Version();
            var pathArchivoCompare = string.Empty;
            if (versionInfo != null)
            {
                if (versionApp.NombreSistema == "SOLTEC.SPOS.Orquestador")
                {
                    version2 = new Version(versionInfo.FileVersion);
                    pathArchivoCompare = versionApp.PathArchivoEXE;
                }
                else if (versionApp.NombreSistema == "SOLTEC.SPOS.Monitor")
                {
                    version2 = new Version(versionInfo.FileVersion);
                    pathArchivoCompare = versionApp.PathArchivoEXE;
                }
            }

            if (System.IO.File.Exists(pathArchivoCompare))
            {
                var version1 = new Version(versionApp.VersionSistema);
                var comparison = version2.CompareTo(version1);
                if (comparison > 0)
                {
                    Logger.Info($"La version DB {version1} es mayor que la version instalada {version2}");
                }
                else if (comparison < 0)
                {
                    Logger.Info($"La version DB {version1} es mayor que la version instalada {version2}");
                    isUpdate = true;
                }
                else
                {
                    Logger.Info($"La version DB {version1} es igual a la version instalada {version2}");
                }
            }
            else
            {
                Logger.Warning($"El archivo de configuracion para la aplicacion {versionApp.NombreSistema} no existe. Descargando actualizacion.");
                isUpdate = true;
            }

            return isUpdate;
        }


        // Actualiza versiones en caso de existir.
        public async Task<bool> ActualizaVersiones(List<VersionesApp> versionesApp)
        {
            Logger.Info("Iniciando proceso de actualizacion para paquetes zip descargados.");

            try
            {
                var existZipFiles = Directory.GetFiles(RutaActualizador, "*.zip");
                Logger.Info($"Se encontraron {existZipFiles.Count()} archivo(s) zip para descomprimir.");

                if (existZipFiles.Length > 0)
                {
                    var archivosZip = Directory.GetFiles(RutaActualizador, "*.zip");

                    foreach (var ver in versionesApp)
                    {
                        if (ver.NombreSistema.Trim() != "SOLTEC.SPOS.Monitor")
                        {
                            foreach (var archivoZip in archivosZip)
                            {
                                if (ver.NombrePaquete.Trim() == Path.GetFileName(archivoZip.Trim()))
                                {
                                    try
                                    {
                                        string nombreProceso = Path.GetFileNameWithoutExtension(archivoZip);
                                        await CerrarProcesoEnEjecucion(nombreProceso);
                                        string destinoFinal = Path.GetDirectoryName(ver.PathArchivoEXE);
                                        if (!Directory.Exists(destinoFinal))
                                            Directory.CreateDirectory(destinoFinal);

                                        using (ZipArchive archivo = ZipFile.OpenRead(archivoZip))
                                        {
                                            foreach (ZipArchiveEntry entrada in archivo.Entries)
                                            {
                                                if (string.IsNullOrEmpty(entrada.Name))
                                                    continue;

                                                string[] partes = entrada.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                                                string rutaRelativa = string.Join(Path.DirectorySeparatorChar.ToString(), partes.Skip(1));
                                                string rutaDestino = Path.Combine(destinoFinal, rutaRelativa);
                                                string dirDestino = Path.GetDirectoryName(rutaDestino);
                                                if (!string.IsNullOrWhiteSpace(dirDestino))
                                                {
                                                    Directory.CreateDirectory(dirDestino);
                                                }
                                                entrada.ExtractToFile(rutaDestino, overwrite: true);
                                            }
                                        }
                                        Logger.Info($"Descomprimido: {archivoZip} -> {destinoFinal}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Info($"Error descomprimiendo '{archivoZip}': {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    Logger.Info("Se actualizaron los Zips descargados.");
                }
                else
                {
                    Logger.Info("No se encontraron archivos zip para una actualizacion.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrió un error al realizar la actualizacion de los sistemas. {ex.Message}");
            }
            return true;
        }

        public async Task CerrarProcesoEnEjecucion(string nombreProceso)
        {
            var procesos = Process.GetProcessesByName(nombreProceso);
            foreach (var proceso in procesos)
            {
                try
                {
                    proceso.Kill();
                    proceso.WaitForExit(); // Opcional, espera a que termine
                    Logger.Info($"Proceso '{nombreProceso}' finalizado.");
                }
                catch (Exception ex)
                {
                    Logger.Info($"Error al cerrar el proceso: {ex.Message}");
                }
            }
        }


        public void DescomprimirZips(string carpetaZips)
        {
            if (!Directory.Exists(carpetaZips))
            {
                Logger.Info("La carpeta de archivos ZIP no existe.");
                return;
            }
        }


        /// <summary>
        /// Envía información a la base de datos CENTRALIZADA.
        /// </summary>
        /// <param name="json"></param>
        /// <param name="nombre"></param>
        /// <param name="IdSucursal"></param>
        /// <returns></returns>
        public async Task<bool> SincronizaScriptManualAsync(string json, SPOS_SQLScripts script, bool conDatos)
        {

            bool exitoso = true;
            try
            {
                var url = await ObtieneUrlActiva();
                if (!string.IsNullOrEmpty(url))
                {
                    var apiUrl = $"{url}/venta/SincronizaScriptUltimo";

                    var ventaEnLinea = new ProcesosOnLine
                    {
                        Sucursal = Sucursal,
                        Json = json,
                        NombreProceso = script.Nombre,
                        IdSucursal = script.IdSucursal,
                        Ver1 = await ObtieneNumeroDeVersion("C:\\Sfspos\\Orquestador\\SOLTEC.SPOS.Orquestador\\SOLTEC.SPOS.Orquestador.exe"),
                        Ver2 = await ObtieneNumeroDeVersion("C:\\Sfspos\\Orquestador\\SOLTEC.SPOS.Monitor\\SOLTEC.SPOS.Monitor.exe"),
                        Ver3 = await ObtieneNumeroDeVersion("C:\\Sfspos\\Orquestador\\SOLTEC.SPOS.ServicioMonitor\\SOLTEC.SPOS.ServicioMonitor.exe"),
                        Ver4 = "",
                        Ver5 = "",
                        Ver6 = "",
                        ConDatos = conDatos,
                        MultiFra = script.MultiFra,
                        DatabaseName = script.DatabaseName,
                        HostName = script.HostName,
                        Password = script.Password,
                        UserName = script.UserName, TicketsFaltantes = script.TicketsFaltantes, TipoCarga = script.TipoCarga
                    };

                    HttpResponseMessage response;

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMinutes(30); 
                        client.DefaultRequestHeaders.Clear();

                        var content = new StringContent(
                            JsonConvert.SerializeObject(ventaEnLinea),
                            Encoding.UTF8,
                            "application/json"
                        );

                        response = await client.PostAsync(apiUrl, content);
                        response.EnsureSuccessStatusCode();
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            exitoso = true;
                            TranmisionInicialExitosa = true;
                        }
                        else
                        {
                            TranmisionInicialExitosa =false;
                            exitoso = false;
                        }
                    }
                }
                else
                {
                    Logger.Warning("No se encontraron servicios disponibles por el momento.");
                    TranmisionInicialExitosa = false;
                }
                return exitoso;
            }
            catch (Exception ex)
            {
                TranmisionInicialExitosa = false;
                Logger.Error($"Error en: SincronizaScriptAsync - {script.Nombre}.\n{ex.Message}");
                return false;
            }
        }
    }

    public class ControlOnDemand
    {
        public DateTime? UltimaEjecucion { get; set; }
        public TimeSpan? UltimaHora { get; set; }
    }
}
