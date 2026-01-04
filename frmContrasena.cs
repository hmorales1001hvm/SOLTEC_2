using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SOLTEC.SPOS.Orquestador
{
    public partial class frmContrasena : Form
    {
        public frmContrasena()
        {
            InitializeComponent();
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            this.Tag = "Cancelar";
            this.Close();
        }

        private void btnAceptar_Click(object sender, EventArgs e)
        {
            var pwd = string.Concat("SOLTEC",System.DateTime.Now.ToString("yyMMdd"));
            if (txtPassword.Text.Trim() == pwd)
            {
                this.Tag = "Aceptar";
                this.Close();
            } else
            {
                MessageBox.Show("La contraseña es incorrecta", "Aviso Importante", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtPassword.Focus();
            }
        }

        private void frmContrasena_Load(object sender, EventArgs e)
        {
            txtPassword.Focus();
        }

        private void frmContrasena_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SendKeys.Send("{TAB}");
            }
        }
    }
}
