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

// CHAT
using System.Threading;

// IMAGE
using System.Drawing.Imaging;

// AUDIO
using Microsoft.DirectX.DirectSound;
using Alaw;

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
        private WaveFormat waveFormat;
        private Buffer buffer;
        private BufferDescription bufferDesc;
        private int buffersize = 100000;
        private CaptureBuffer captureBuffer;
        private MemoryStream stream;
        private byte[] streamBuffer;

        // Multicast
        private IPAddress multicast = IPAddress.Parse("224.0.0.4");
        private static UdpClient videoserver, chatserver, audioserver;
        private static IPEndPoint videoremote, chatremote, audioremote;
        private int videoport = 45040;
        private int chatport = 45041;
        private int audioport = 45042;

        // Chat
        private String msg;

        // QoS

        public Form1()
        {
            InitializeComponent();

            // Inicializamos los canales de comunicación
            try
            {
                // Transmision de video
                videoserver = new UdpClient();
                videoremote = new IPEndPoint(multicast, videoport);
                // Implementando mi clase RTP
                RTP video = new RTP("video1", videoserver, multicast, videoremote);

                // Transmision audio
                audioserver = new UdpClient();
                audioremote = new IPEndPoint(multicast, audioport);
                // Implementacion mi clase RTP
                RTP audio = new RTP("audio1", audioserver, multicast, audioremote);

                // CHAT
                chatserver = new UdpClient();
                chatremote = new IPEndPoint(IPAddress.Any, chatport);
                chatserver.Client.Bind(chatremote);
                chatserver.JoinMulticastGroup(multicast);

                Thread t = new Thread(this.ChatThread);
                t.Start();
                CheckForIllegalCrossThreadCalls = false;

            } catch(Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Configuramos la camara
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

            // Configuramos el audio
            // Configuramos DirectSound?
            device = new Device();

            // Creamos Waveformat
            waveFormat = new WaveFormat();
            waveFormat.BitsPerSample = 16;              // 16 bits
            waveFormat.BlockAlign = 1;
            waveFormat.Channels = 1;                    // Stereo
            waveFormat.AverageBytesPerSecond = 20500;   // 22kHz
            waveFormat.SamplesPerSecond = 205000;       // 22kHz
            waveFormat.FormatTag = WaveFormatTag.Pcm;

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
            //device.SetCooperativeLevel(
            //    this,
            //    CooperativeLevel.Priority   // The cooperative level
            //);

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
            

            captureBuffDesc = new CaptureBufferDescription();
            captureBuffDesc.BufferBytes = buffersize;
            captureBuffDesc.Format = this.waveFormat;
            captureBuffer = new CaptureBuffer(captureBuffDesc, capture);

            // Stream
            streamBuffer = new byte[buffersize];
            for (int i = 0; i < buffersize;i++){
                streamBuffer[i] = 0;
            }

            stream = new MemoryStream(streamBuffer);
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
                _latestFrame = new Bitmap(_latestFrame, new Size(460, 270));
                e.Graphics.DrawImage(_latestFrame, 0, 0, _latestFrame.Width, _latestFrame.Height);

                // Comprimimos el frame
                Bitmap cFrame = new Bitmap(_latestFrame, new Size(320, 240));

                // Conversion a JPEG
                jpegFrame = new MemoryStream();
                cFrame.Save(jpegFrame, ImageFormat.Jpeg);

                // Enviamos por el canal
                video.sendJPEG(jpegFrame);
            }
        }

        private void ChatThread(){
            // Mantenemos a la escucha el socket
            while(true){
                byte[] received = chatserver.Receive(ref chatremote);

                msg = Encoding.UTF8.GetString(received, 0, received.Length);
                listBox1.Items.Add(msg);
            }
        }
    }
}
