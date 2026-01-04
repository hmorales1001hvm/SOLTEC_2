using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Soltec.Common.LoggerFramework;

namespace SOLTEC.SPOS.Datos
{
    public class SQLDatos
    {
        public string cadenaConexion = "Server=localhost\\spos;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";

        public async Task<Dictionary<string, object>> ObtieneSetDeDatos(string script)
        {
            var resultado = new Dictionary<string, object>();

#if DEBUG
            cadenaConexion = "Server=localhost;Database=SPOSAA;Uid=SPV2;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#endif

            var consultaSQL = "";
            var bloquesSQL = script
                .Split(';')
                .Select(b => b.Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();

            using (var cnx = new SqlConnection(cadenaConexion))
            {
                try
                {
                    cnx.Open();

                    for (int i = 0; i < bloquesSQL.Count; i++)
                    {
                        var bloque = bloquesSQL[i];

                        // Dividir por líneas
                        var lineas = bloque.Split('\n');

                        // Buscar comentario tipo "--nombre"
                        string nombre = null;
                        foreach (var linea in lineas)
                        {
                            var l = linea.Trim();
                            if (l.StartsWith("--"))
                            {
                                nombre = l.Replace("--", "").Trim();
                                break;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(nombre))
                        {
                            nombre = $"tabla{i + 1}";
                        }

                        consultaSQL = string.Join("\n", lineas.Where(l => !string.IsNullOrWhiteSpace(l))
                                                 .Select(l =>
                                                 {
                                                     string line = l.Trim();

                                                     // Si la línea es un comentario completo, ignórala
                                                     if (line.StartsWith("--"))
                                                         return string.Empty;

                                                     // Si tiene comentario en línea, cortar antes de --
                                                     int index = line.IndexOf("--");
                                                     if (index >= 0)
                                                         line = line.Substring(0, index).Trim();

                                                     return line;
                                                 })
                                                 .Where(l => !string.IsNullOrWhiteSpace(l)) // eliminar líneas vacías post-trimming
                                         ).Trim();

                        if (!string.IsNullOrWhiteSpace(consultaSQL))
                        {
                            var filas = (await cnx.QueryAsync(consultaSQL, commandTimeout: 90000)).ToList();
                            resultado[nombre] = filas;
                        }
                    }

                    cnx.Close();
                }
                catch (Exception ex)
                {

                    Logger.Error($"Ocurrió un error al consultar su información: {ex.Message}, Query: {consultaSQL}");
                    throw new Exception($"Ocurrió un error al consultar su información: {ex.Message}, Query: {consultaSQL}");
                }
            }

            return resultado;
        }



        public string ObtieneSucursal()
        {
            var sucursal = "";

#if DEBUG
                cadenaConexion = "Server=localhost;Database=SPOSAA;Uid=SPV;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#endif

            try
            {
                var query = "SELECT Id_Farmacia AS ClaveSimi FROM Configuracion_Farmacia";
                var connection = new SqlConnection(cadenaConexion);

                try
                {
                    connection.Open();
                    sucursal = connection.QuerySingle<string>(query, commandTimeout: 420);
                    connection.Close();
                }
                catch (Exception ex)
                {
                    connection.Close();
                }

                if (!string.IsNullOrEmpty(sucursal))
                {
                    Logger.Info($"Sucursal obtenida: {sucursal}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en posible connection: {ex.Message}\n{cadenaConexion}");
            }

            return sucursal;
        }

        public async Task<List<object>> ObtieneDatos(string script)
        {
            var data = new List<object>();

#if DEBUG
                cadenaConexion = "Server=localhost;Database=SPOSAA;Uid=SPV;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#endif

            try
            {
                var connection = new SqlConnection(cadenaConexion);

                try
                {
                    connection.OpenAsync();
                    data = (await connection.QueryAsync<object>(script, commandTimeout: 50000)).ToList();
                    connection.Close();
                }
                catch (Exception ex)
                {
                    connection.Close();
                    Logger.Error($"Error al obtener los datos: {ex.Message}\n{script}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en posible connection: {ex.Message}\n{cadenaConexion}");
            }

            return data;
        }

        /// <summary>
        /// Método para obtener los datos de SQL Server y regresar un texto plano separado por pipes por cda columna.
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public async Task<string> ObtieneDatosPlano(string script)
        {
            var resultadoPlano = new StringBuilder();
#if DEBUG
            cadenaConexion = "Server=localhost;Database=SPOSAA;Uid=SPV;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#endif
            try
            {
                using (var connection = new SqlConnection(cadenaConexion))
                {
                    await connection.OpenAsync();

                    var data = (await connection.QueryAsync(script, commandTimeout: 50000)).ToList();

                    if (data.Count == 0)
                        return string.Empty;

                    // Tomar la primera fila como referencia de columnas
                    var dict = (IDictionary<string, object>)data[0];

                    // Encabezados
                    resultadoPlano.AppendLine(string.Join("|", dict.Keys));

                    // Filas
                    foreach (var row in data)
                    {
                        var values = ((IDictionary<string, object>)row)
                            .Select(kv => kv.Value?.ToString()?.Replace("|", " ") ?? "")
                            .ToArray();

                        resultadoPlano.AppendLine(string.Join("|", values));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error al obtener los datos: {ex.Message}\n{script}");
                return resultadoPlano.ToString();
            }

            return resultadoPlano.ToString();
        }
    }
}
