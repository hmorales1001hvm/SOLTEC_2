

using Soltec.Common.LoggerFramework;
using System;
using System.IO;
using System.Threading;

namespace Soltec.Common.LoggerFramework
{
	/// <summary>
	/// Clase de Loggeo con escritura en consolo, base de datos local o Archivo
	/// </summary>
	public class Logger
	{


		/// <summary>
		/// Semaforo de escritura de log
		/// </summary>
		private static readonly object logLock = new object();
		private static bool logIsActiveConsole = Settings1.Default.LogIsActiveConsole;
		private static bool logIsActive = Settings1.Default.LogIsActive;

		/// <summary>
		/// Semaforo de escritura de log
		/// </summary>Log
		private static ELevelLogError levelLog = (ELevelLogError)(ELevelLogError.Debug);


		/// <summary>
		/// Escribe un salto de linea en la consola.
		/// </summary>
		/// <param name="message"></param>
		public static void Enter()
		{
			Console.WriteLine();
		}

		/// <summary>
		/// Configura el nivel de log de la aplicación 
		/// </summary>
		/// <param name="eLevelLogError"></param>
		public static void SetLevelLog(ELevelLogError eLevelLogError)
		{
			levelLog = eLevelLogError;
		}

		/// <summary>
		/// Configura mediante un valor entero en nivel del log de acuerdo con el siguiente Enumerador
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="levelLog"></param>
		public static void SetLevelLog(int levelLog)
		{
			if (levelLog > 3 || levelLog < -1)
			{
				Error("Error al configurar el log valores permitidos: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3");
				Error("Log configurado con nivel Error");
				Logger.levelLog = ELevelLogError.Error;
			}
			else
				Logger.levelLog = (ELevelLogError)levelLog;
		}

		/// <summary>
		/// Escribe el log en blanco
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		public static void Debug(string message)
		{
			if ((int)levelLog >= (int)ELevelLogError.Debug)
			{
				Console.ForegroundColor = ConsoleColor.White;
				Write("DEBG - " + message);
			}
		}
		/// <summary>
		/// Escribe el log en blanco
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		public static void Debug(string message, params object[] parameters)
		{
			Debug(string.Format(message, parameters));
		}

		/// <summary>
		/// Escribe el log en Verde
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		public static void Info(string message)
		{
			if ((int)levelLog >= (int)ELevelLogError.Important)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Write("INFO - " + message);
				Console.ForegroundColor = ConsoleColor.White;
			}
		}
		/// <summary>
		/// Escribe el log en Verde
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		public static void Info(string message, params object[] parameters)
		{
			Info(string.Format(message, parameters));
		}


		/// <summary>
		/// Escribe el log en Verde
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		[Obsolete("Important is deprecated, please use Info instead.")]
		public static void Important(string message)
		{
			if ((int)levelLog >= (int)ELevelLogError.Important)
			{
				Console.ForegroundColor = ConsoleColor.Cyan;
				Write("IMPT - " + message);
				Console.ForegroundColor = ConsoleColor.White;
			}
		}
		/// <summary>
		/// Escribe el log en Verde
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		[Obsolete("Important is deprecated, please use Info instead.")]
		public static void Important(string message, params object[] parameters)
		{
			Important(string.Format(message, parameters));
		}

		/// <summary>
		/// Escribe el log en Rojo
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		public static void Error(string message)
		{
			if ((int)levelLog >= (int)ELevelLogError.Error)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Write("ERRO - " + message);
				Console.ForegroundColor = ConsoleColor.White;
			}
		}
		/// <summary>
		/// Escribe el log en Rojo
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		public static void Error(string message, params object[] parameters)
		{
			Error(string.Format(message, parameters));
		}
		/// <summary>
		/// Obtiene los datos de la clase Excetion y lo muestra en el log
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		public static void Error(Exception ex)
		{
			Error(string.Format("Error: {0}.{1}, {2}", ex.Message.ToString(), Environment.NewLine, ex.ToString()));
		}

		/// <summary>
		/// Escribe el log en Amarillo
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		public static void Warning(string message)
		{
			if ((int)levelLog >= (int)ELevelLogError.Warning)
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Write("WRNG - " + message);
				Console.ForegroundColor = ConsoleColor.White;
			}
		}
		/// <summary>
		/// Escribe el log en Amarillo
		/// LevelLog: Inactive = -1, Error = 0, Warning = 1, Important = 2, Debug = 3
		/// </summary>
		/// <param name="message"></param>
		public static void Warning(string message, params object[] parameters)
		{
			Warning(string.Format(message, parameters));
		}


		/// <summary>
		/// Agrega fecha y hora en le log
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		private static string setDate(string message)
		{
			try
			{
				return string.Format("{0} -- {1}", DateTime.Now.ToString("HH:mm:ss dd-MM-yyyy"), message);
			}
			catch
			{
				return "No date";
			}

		}

		/// <summary>
		/// Escribe el Log
		/// </summary>
		/// <param name="Message"></param>
		private static void Write(string Message)
		{
			try
			{
				if (logIsActiveConsole)
					Console.WriteLine(setDate(Message));

				lock (logLock)
				{
					try
					{
						if (logIsActive)
							FileUtil.WriteInLogFile(Message);
					}
					catch (Exception)
					{
						//Se garantiza no existir excepcion en un elemento de logueo
					}

				}

			}
			catch (Exception)
			{
				//Se garantiza no existir excepcion en un elemento tan importante
			}
		}

		/// <summary>
		/// Escribe el log en una base de datos local
		/// </summary>
		/// <param name="Message"></param>
		private static void WriteDBLocal(string Message)
		{
			try
			{
				if (logIsActiveConsole)
					Console.WriteLine(setDate(Message));

				new Thread(() =>
				{
					lock (logLock)
					{
						try
						{
							if (logIsActive)
								FileUtil.WriteInLogFile(Message);
						}
						catch (Exception)
						{

						}

					}

				}).Start();

			}
			catch (Exception)
			{
				//Se garantiza no existir excepcion en un elemnto tan importante
			}
		}

		/// <summary>
		/// Enumerador de LevelLog
		/// </summary>
		public enum ELevelLogError
		{
			Inactive = -1,
			Error = 0,
			Warning = 1,
			Important = 2,
			Debug = 3
		}
	}
}
