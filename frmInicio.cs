using SOLTEC.SPOS.Negocio.Sincronizacion;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace SOLTEC.SPOS.Orquestador
{
    public partial class frmInicio : Form
    {
        private NotifyIcon notifyIcon;
        public frmInicio()
        {
            InitializeComponent();

            // Registrar limpieza global al salir de la app
            Application.ApplicationExit += (s, e) =>
            {
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }
            };
        }


        private void CerrarAplicacion()
        {
            frmContrasena frm = new frmContrasena();
            frm.ShowDialog();
            if (frm.Tag?.ToString() == "Aceptar")
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                Application.Exit();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;          // ocultar
                notifyIcon.ContextMenuStrip = null;  // desvincular eventos
                notifyIcon.Dispose();                 // liberar recursos
                notifyIcon = null;
            }
            base.OnFormClosing(e);
        }

        async Task SincroManual()
        {
            var _urls = Settings1.Default.Urls;
            var _rutaSucursalObtenida = Settings1.Default.RutaSucursalObtenida;
            var _rutaActualizador = Settings1.Default.RutaActualizador;
            var contador = 0;
            Sincronizador _sincronizador = new Sincronizador(_urls, _rutaSucursalObtenida, _rutaActualizador);

            while (contador <= 10)
            {
                var listaDeScripts = await _sincronizador.ObtieneScripts(1);
                if (listaDeScripts != null)
                {
                    if (listaDeScripts.Success)
                    {
                        if (listaDeScripts?.List == null || listaDeScripts.List.Count == 0)
                            return;

                        foreach (var script in listaDeScripts.List)
                        {
                            if (script != null && string.IsNullOrEmpty(script.ScriptTable) && script.MultiplesTablas)
                            {
                                await _sincronizador.ProcesaScriptManualAsync(script);
                            }
                        }
                        contador = 100;
                        break;
                    }

                }
                contador++;
            }
        }

        private void frmInicio_Load_1(object sender, EventArgs e)
        {
            this.ShowInTaskbar = false; // evita que quede en la barra de tareas
            this.WindowState = FormWindowState.Minimized;
            this.Hide();

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = Properties.Resources.Icon1;
            notifyIcon.Visible = true;
            notifyIcon.Text = "Sincronizador";

            var menu = new ContextMenuStrip();



            var itemSincro = new ToolStripMenuItem
            {
                Name = "mnuSincroManual",
                Text = "&Sincronización Manual",
                Enabled = true,
                ForeColor = Color.Blue
            };

            
            itemSincro.Click += async (s, ev) =>
            {
                if (MessageBox.Show("¿Está seguro de sincronizar manualmente su información?", "Confirmación", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    itemSincro.Enabled = false;
                    await SincroManualConEfecto(itemSincro);
                }
            };

            menu.Items.Add(itemSincro);
            menu.Items.Add("&Salir", null, (s, ev) => CerrarAplicacion());
            notifyIcon.ContextMenuStrip = menu;
        }


        private async Task SincroManualConEfecto(ToolStripMenuItem item)
        {
            
            Color colorOriginal = item.ForeColor;
            item.ForeColor = Color.Red;
            item.Text = "Espere un momento...";
            await SincroManual();

            item.Text = "&Sincronización Manual";
            item.ForeColor = colorOriginal;
            item.Enabled = true;

        }

    }
}
