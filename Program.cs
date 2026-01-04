using Soltec.Common.LoggerFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SOLTEC.SPOS.Orquestador
{
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            FileUtil.localLogPath = @"C:\Sfspos\Orquestador\SOLTEC.SPOS.Orquestador\Logs\";
            var service = new Sincronizacion.BackGroundService();
            service.Start();

            Application.Run(new frmInicio());

        }
    }
}
