using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WinformsNetCorePerfApp1
{
    public partial class Form3 : Form
    {
        public Form3()
        {
            InitializeComponent();
        }
        private void Timer1_Tick(object sender, EventArgs e)
        {
            int i = 100;
            progressBar1.Value = progressBar1.Value + 1;
            i = 100 - progressBar1.Value;

            if (i == 0)
            {
                timer1.Enabled = false;
            }

        }

        private void Button2_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
        }

        private void TextBox2_TextChanged(object sender, EventArgs e)
        {
            this.errorProvider1.SetError(this.textBox2, "eror");
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog();
        }
    }
}