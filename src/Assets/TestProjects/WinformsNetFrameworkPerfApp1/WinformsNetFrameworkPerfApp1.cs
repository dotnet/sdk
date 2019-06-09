using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinformsNetFrameworkPerfApp1
{
    public partial class WinformsNetFrameworkPerfApp1 : Form
    {
        public WinformsNetFrameworkPerfApp1()
        {
            InitializeComponent();
        }

        private void WinformsNetFrameworkPerfApp1_Load(object sender, EventArgs e)
        {
            Form1 frm1 = new Form1();
            frm1.TopLevel = false;
            frm1.Dock = DockStyle.Fill;
            frm1.Parent = this.tabPage1;
            frm1.Show();

            Form2 frm2 = new Form2();
            frm2.TopLevel = false;
            frm2.Dock = DockStyle.Fill;
            frm2.Parent = this.tabPage2;
            frm2.Show();

            Form3 frm3 = new Form3();
            frm3.TopLevel = false;
            frm3.Dock = DockStyle.Fill;
            frm3.Parent = this.tabPage3;
            frm3.Show();

        }
    }
}
