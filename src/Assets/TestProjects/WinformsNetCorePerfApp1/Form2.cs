using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WinformsNetCorePerfApp1
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }
        private void TabControl2_Click(object sender, EventArgs e)
        {

        }

        private void Button12_Click(object sender, EventArgs e)
        {
            if (this.textBox3.Text == "")
            {
                MessageBox.Show("please input the username");
            }
            else if (this.textBox4.Text == "")
            {
                MessageBox.Show("please input the password");
            }
            else
            {
                MessageBox.Show("login");

            }
        }

        private void TableLayoutPanel1_CellPaint(object sender, TableLayoutCellPaintEventArgs e)
        {
            Pen pp = new Pen(Color.Red);
            e.Graphics.DrawRectangle(pp, e.CellBounds.X, e.CellBounds.Y, e.CellBounds.X + e.CellBounds.Width - 1, e.CellBounds.Y + e.CellBounds.Height - 1);

        }

        private void table_Paint(object sender, PaintEventArgs e)
        {
            Pen pp = new Pen(Color.Red);
            e.Graphics.DrawRectangle(pp, e.ClipRectangle.X - 1, e.ClipRectangle.Y - 1, e.ClipRectangle.X + e.ClipRectangle.Width - 0, e.ClipRectangle.Y + e.ClipRectangle.Height - 0);

        }
    }
}