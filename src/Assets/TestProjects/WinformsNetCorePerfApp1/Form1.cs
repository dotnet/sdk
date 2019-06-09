using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WinformsNetCorePerfApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load_1(object sender, EventArgs e)
        {
            for (int i = 0; i < 1000; i++)
            {
                int index = this.dataGridView2.Rows.Add();
                this.dataGridView2.Rows[index].Cells[0].Value = i.ToString();
                this.dataGridView2.Rows[index].Cells[1].Value = "Amy";
                this.dataGridView2.Rows[index].Cells[2].Value = "Li";
                this.dataGridView2.Rows[index].Cells[3].Value = "Xi'an";
                this.dataGridView2.Rows[index].Cells[4].Value = "029-1234567";
                this.dataGridView2.Rows[index].Cells[5].Value = "15245963214";
                this.dataGridView2.Rows[index].Cells[6].Value = "Running";
            }

            this.propertyGrid1.SelectedObject = this.richTextBox2;
        }
    }
}
