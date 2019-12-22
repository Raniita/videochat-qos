using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Touchless.Vision.Camera;

// UDP y Multicast.
using System.Net.Sockets;
using System.Net;
using System.IO;

// IMAGE
using System.Drawing.Imaging;

namespace WebcamUDPMulticast
{
    public partial class Form1 : Form
    {
        private CameraFrameSource _frameSource;
        private static Bitmap _latestFrame;

        public Form1()
        {
            InitializeComponent();
            // Inicializamos los canales de comunicación
            try
            {

            } catch(Exception e)
            {

            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            foreach(Camera cam in CameraService.AvailableCameras)
            {
                comboBoxCameras.Items.Add(cam);
            }

            comboBoxCameras.SelectedIndex = 0;
            comboBoxCameras.Select();
            Camera c = (Camera)comboBoxCameras.SelectedItem;
            _frameSource = new CameraFrameSource(c);
            _frameSource.Camera.CaptureWidth = 320;
            _frameSource.Camera.CaptureHeight = 240;
            _frameSource.Camera.Fps = 20;
            _frameSource.NewFrame += OnImageCaptured;
            pictureBox1.Paint += new PaintEventHandler(drawLatestImage);
            
            _frameSource.StartFrameCapture();
        }

        private void button1_Click(object sender, EventArgs e)
        {
           
        }

        
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void OnImageCaptured(Touchless.Vision.Contracts.IFrameSource frameSource, Touchless.Vision.Contracts.Frame frame, double fps)
        {
            _latestFrame = frame.Image;
            pictureBox1.Invalidate();
        }

        private void drawLatestImage(object sender, PaintEventArgs e)
        {
            if (_latestFrame != null)
            {
                _latestFrame = new Bitmap(_latestFrame, new Size(320, 240));
                // Redimensionames la imagen a 320x240
                e.Graphics.DrawImage(_latestFrame, 0, 0, _latestFrame.Width, _latestFrame.Height);

                // Enviamos las imagenes
            }
        }
    }
}
