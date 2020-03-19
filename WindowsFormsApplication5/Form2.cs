using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WindowsFormsApplication5
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        public void setLeftImage(Bitmap diff_bitmap)
        {
            pictureBox1.Image = diff_bitmap;
            pictureBox1.Invalidate();
            pictureBox1.Update();
        }

        public void setRightImage(Bitmap sum_bitmap)
        {
            pictureBox2.Image = sum_bitmap;
            pictureBox2.Invalidate();
            pictureBox2.Update();
        }

        public void setText(String input_text)
        {
            this.Text = input_text;
        }
    }
}
