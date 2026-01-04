namespace SOLTEC.SPOS.Modelo.Entidades
{
    public class SPOS_SQLScripts
    {
        public int IdSqlScript { get; set; }
        public string SQLScript { get; set; }
        public string Nombre { get; set; }
        public string Tipo { get; set; }
        public string Condicion { get; set; }
        public bool EsAPI { get; set; }
        public bool Activo { get; set; }
		public string Descripcion { get; set; }
        public bool EsCatalogo { get; set; }
        public int ValorIncrementoDecremento { get; set; }
		public bool EsSP { get; set; }
		public string Param1 { get; set; }
		public string Param2 { get; set; }
		public string Param3 { get; set; }
		public string Param4 { get; set; }
		public string Param5 { get; set; }
		public string Param6 { get; set; }
		public string Param7 { get; set; }
		public string Param8 { get; set; }
		public string Param9 { get; set; }
		public string Param10 { get; set; }
        public bool MultiplesTablas { get; set; }
        public int TiempoTransmision { get; set; }
        public string Carga_SQLServer_SQLite { get; set; }
        public string ScriptTable { get; set; }
        public int ResetearTablaSQLite { get; set; }
        public int IdSucursal { get; set; }
        public int MultiFra { get; set; }
        public string HostName { get; set; }
        public string DatabaseName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string UrlSQS { get; set; } = string.Empty;
        public string fechaInicial { get; set; } = string.Empty;
        public string fechaFinal { get; set; } = string.Empty;
        public bool ConTransmisionInicial { get; set; }
        public string TicketsFaltantes { get; set; }
        public string TipoCarga { get; set; }
    }

    public class ParametrosScripts {
        public string Param1 { get; set; }
		public string Param2 { get; set; }
		public string Param3 { get; set; }
		public string Param4 { get; set; }
		public string Param5 { get; set; }
		public string Param6 { get; set; }
		public string Param7 { get; set; }
		public string Param8 { get; set; }
		public string Param9 { get; set; }
		public string Param10 { get; set; }
	}

	public class ConfigSucursales
	{
		public string ClaveSucursal { get; set; }
	}
}
