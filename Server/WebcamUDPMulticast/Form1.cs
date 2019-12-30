﻿using System;
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

// CHAT
using System.Threading;

// IMAGE
using System.Drawing.Imaging;

// AUDIO
using Microsoft.DirectX.DirectSound;
using Buffer = Microsoft.DirectX.DirectSound.Buffer;
//using Alaw;

// RTP
using RTPStream;

namespace WebcamUDPMulticast
{
    public partial class Form1 : Form
    {
        // Camera Config
        private CameraFrameSource _frameSource;
        private static Bitmap _latestFrame;
        private MemoryStream jpegFrame;
        private byte[] byteArray;

        // Audio Config
        private Device device;
        private Capture capture;
        //private WaveFormat waveFormat;
        private Buffer buffer;
        private BufferDescription bufferDesc;
        private int buffersize = 100000;
        private CaptureBuffer captureBuffer;
        //private CaptureBufferDescription captureBuffDesc;
        private MemoryStream stream;
        private byte[] streamBuffer;

        // Multicast
        private IPAddress multicast = IPAddress.Parse("224.0.0.4");
        private static UdpClient videoserver, chat1server, chat2server, audioserver;
        private static IPEndPoint videoremote, chat1remote, chat2remote, audioremote;
        private int videoport = 45040;
        private int chat1port = 45041;
        private int chat2port = 45042;
        private int audioport = 45043;
        
        private RTP video;
        private RTP audio;

        // Chat
        private String username;
        private String msg;
        private byte[] encodedmsg;

        // QoS

        public Form1()
        {
            InitializeComponent();

            richTextBox1.Enabled = false;
            button1.Enabled = false;
            listBox1.Items.Add("*** Ingresa tu usuario y pulsa conectar ***");
            button6.Enabled = false;
            button5.Enabled = false;
            button8.Enabled = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Configuramos la camara
            InitCamera();

            // Configuramos el audio
            //InitAudio();
        }

        private void button1_Click(object sender, EventArgs e)
        {
           // Enviar mensaje al chat
           if(richTextBox1.Text != "")
            {
                // Enviamos al multicast
                try
                {
                    msg = String.Format("{0}: {1}", username, richTextBox1.Text);
                    encodedmsg = Encoding.UTF8.GetBytes(msg);
                    chat2server.Send(encodedmsg, encodedmsg.Length, chat2remote);
                } catch(Exception ex)
                {
                    MessageBox.Show("Error sending the chat message");
                }
                finally
                {
                    listBox1.Items.Add(msg);
                    richTextBox1.Text = "";
                }
            }
        }


        private void button2_Click(object sender, EventArgs e)
        {
            // Hemos pulsado boton conectar

            // Guardamos el usuario del chat
            if (richTextBox2.Text == "")
            {
                richTextBox2.Text = "Anonymous";
                richTextBox2.Enabled = false;
                username = "Anonymous";
            } else
            {
                username = richTextBox2.Text;
                richTextBox2.Enabled = false;
            }

            button2.Enabled = false;
            richTextBox1.Enabled = true;
            button1.Enabled = true;
            listBox1.Items.Clear();
            listBox1.Items.Add(String.Format("*** Bienvenido {0} a la sala de chat ***", username));

            // Inicializamos los canales de comunicación
            try
            {
                // Transmision de video
                videoserver = new UdpClient();
                videoremote = new IPEndPoint(multicast, videoport);
                // Implementando mi clase RTP
                video = new RTP("video1", videoserver, multicast, videoremote);

                // Transmision audio
                audioserver = new UdpClient();
                audioremote = new IPEndPoint(multicast, audioport);
                // Implementacion mi clase RTP
                audio = new RTP("audio1", audioserver, multicast, audioremote);

                // Enviar chat
                chat2server = new UdpClient(chat2port);
                chat2remote = new IPEndPoint(multicast, chat2port);
                chat2server.JoinMulticastGroup(multicast);

                // Recibir chat
                chat1server = new UdpClient();
                chat1remote = new IPEndPoint(IPAddress.Any, chat1port);
                chat1server.Client.Bind(chat1remote);
                chat1server.JoinMulticastGroup(multicast);
                checkBox4.Checked = true;

                Thread t = new Thread(this.ChatThread);
                t.Start();
                CheckForIllegalCrossThreadCalls = false;
            } catch (Exception excp)
            {
                MessageBox.Show(excp.ToString());
            }
            finally
            {
                checkBox1.Checked = true;
            }
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

        private void button4_Click(object sender, EventArgs e)
        {
            button4.Enabled = false;
            button5.Enabled = true;
            checkBox2.Checked = true;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            button7.Enabled = false;
            button8.Enabled = true;
            checkBox3.Checked = true;
        }

        private void drawLatestImage(object sender, PaintEventArgs e)
        {
            if (_latestFrame != null)
            {
                _latestFrame = new Bitmap(_latestFrame, new Size(460, 270));
                e.Graphics.DrawImage(_latestFrame, 0, 0, _latestFrame.Width, _latestFrame.Height);

                if (button4.Enabled == false)
                {
                    // Transmitimos la imagen
                    // Comprimimos el frame
                    Bitmap cFrame = new Bitmap(_latestFrame, new Size(320, 240));

                    // Conversion a JPEG
                    jpegFrame = new MemoryStream();
                    cFrame.Save(jpegFrame, ImageFormat.Jpeg);

                    // Enviamos por el canal
                    video.sendJPEG(jpegFrame);
                }
            }
        }

        private void ChatThread(){
            // Mantenemos a la escucha el socket
            while(true){
                byte[] received = chat1server.Receive(ref chat1remote);

                msg = Encoding.UTF8.GetString(received);
                listBox1.Items.Add(msg);
            }
        }

        private void InitCamera()
        {
            // Configuramos la camara
            foreach (Camera cam in CameraService.AvailableCameras)
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

        private void InitAudio()
        {
            // Configuramos DirectSound?
            device = new Device();

            // Creamos Waveformat
            WaveFormat waveFormat = new WaveFormat
            {
                BitsPerSample = 16,              // 16 bits
                BlockAlign = 1,
                Channels = 1,                    // Stereo
                AverageBytesPerSecond = 20500,   // 22kHz
                SamplesPerSecond = 205000,       // 22kHz
                FormatTag = WaveFormatTag.Pcm
            };

            // CaptureBuffer y BufferDescription
            bufferDesc = new BufferDescription();
            buffer = new Buffer(bufferDesc, this.device);
            bufferDesc.Format = waveFormat;
            bufferDesc.BufferBytes = buffersize;
            bufferDesc.ControlPositionNotify = true;
            bufferDesc.ControlFrequency = true;
            bufferDesc.ControlPan = true;
            bufferDesc.ControlVolume = true;

            // Cooperative level
            device.SetCooperativeLevel(
                this,
                CooperativeLevel.Priority   // The cooperative level
            );

            // Capture
            CaptureDevicesCollection captureDevices = new CaptureDevicesCollection();
            foreach (DeviceInformation captureDevice in captureDevices){
                comboBoxAudio.Items.Add(captureDevice.Description);
            }

            comboBoxAudio.SelectedIndex = 0;
            comboBoxAudio.Select();
            DeviceInformation mic = (DeviceInformation)comboBoxAudio.SelectedItem;
            capture = new Capture(mic.DriverGuid);

            // Cambios en el buffer


            CaptureBufferDescription captureBuffDesc = new CaptureBufferDescription
            {
                BufferBytes = buffersize,
                Format = waveFormat
            };
            captureBuffer = new CaptureBuffer(captureBuffDesc, capture);

            // Stream
            streamBuffer = new byte[buffersize];
            for (int i = 0; i < buffersize;i++){
                streamBuffer[i] = 0;
            }

            stream = new MemoryStream(streamBuffer);
        }
    }
}
