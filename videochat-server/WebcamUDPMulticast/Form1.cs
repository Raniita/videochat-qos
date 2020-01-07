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
//using Buffer = Microsoft.DirectX.DirectSound.Buffer;
using ALaw;

// RTP
using RTPStream;

using Timer = System.Windows.Forms.Timer;

namespace WebcamUDPMulticast
{
    public partial class Form1 : Form
    {
        // Camera Config
        private CameraFrameSource _frameSource;
        private static Bitmap _latestFrame;
        private MemoryStream jpegFrame;

        // Audio Config
        private Guid record_source;
        private readonly short channels = 1;
        private readonly short bitsPerSample = 16;
        private readonly int samplesPerSecond = 22050;

        private AutoResetEvent autoResetEvent;
        private Notify notify;

        private Thread audiosender;

        private Device device;
        private Capture capture;
        private WaveFormat waveFormat;
        private BufferDescription bufferDesc;
        private SecondaryBuffer bufferplayback;
        private int buffersize = 100000;
        private CaptureBuffer captureBuffer;
        private CaptureBufferDescription captureBuffDesc;

        // Multicast
        private readonly int videoport = 45040;
        private readonly int chat1port = 45041;
        private readonly int chat2port = 45042;
        private readonly int audioport = 45043;
        private IPAddress multicast = IPAddress.Parse("224.0.0.4");
        private static UdpClient videoserver, chat1server, chat2server, audioserver;
        private static IPEndPoint videoremote, chat1remote, chat2remote, audioremote;
        
        private RTP video;
        private RTP audio;

        // Chat
        private String username;
        private String msg;
        private byte[] encodedmsg;

        // QoS
        private Timer timer1;
        private Timer timer2;
        private int num_video;
        private int num_audio;

        public Form1()
        {
            InitializeComponent();

            richTextBox1.Enabled = false;
            button1.Enabled = false;
            listBox1.Items.Add("*** Ingresa tu usuario y pulsa conectar ***");
            button3.Enabled = false;
            button6.Enabled = false;
            button5.Enabled = false;
            button8.Enabled = false;
            button6.Visible = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Configuramos la camara
            InitCamera();

            // Configuramos el audio
            InitAudioCombo();
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
                    chat1server.Send(encodedmsg, encodedmsg.Length, chat1remote);
                } catch(Exception)
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
            button3.Enabled = true;
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
                
                // Timer para los paq/s
                timer1 = new Timer();
                timer1.Tick += new EventHandler(analyzeVideo);  // Sacamos parametros de la TX
                timer1.Interval = 1000;                         // Cada 1000 ms
                timer1.Start();

                // Transmision audio
                audioserver = new UdpClient();
                audioremote = new IPEndPoint(multicast, audioport);
                // Implementacion mi clase RTP
                audio = new RTP("audio1", audioserver, multicast, audioremote);

                // Iniciamos el audio
                InitAudioCapture();
                audiosender = new Thread(new ThreadStart(SendAudio));

                // Timer para los paq/s
                timer2 = new Timer();
                timer2.Tick += new EventHandler(analyzeAudio);  // Sacamos parametros de la TX
                timer2.Interval = 1000;                         // Cada 1000 ms
                timer2.Start();

                // Enviar chat
                chat1server = new UdpClient();
                chat1remote = new IPEndPoint(multicast, chat1port);
                chat1server.JoinMulticastGroup(multicast);

                // Recibir chat
                chat2server = new UdpClient(chat2port);
                chat2remote = null;
                chat2server.JoinMulticastGroup(multicast);

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
            // Boton TX Video
            if(button2.Enabled == false)
            {
                comboBoxCameras.Enabled = false;
                button4.Enabled = false;
                button5.Enabled = true;
                checkBox2.Checked = true;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // Stop TX Video
            if (button2.Enabled == false)
            {
                comboBoxCameras.Enabled = true;
                button4.Enabled = true;
                button5.Enabled = false;
                checkBox2.Checked = false;
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            // Boton TX Audio
            if(button2.Enabled == false)
            {
                comboBoxAudio.Enabled = false;
                button7.Enabled = false;
                button8.Enabled = true;
                checkBox3.Checked = true;

                // Start Audio Send
                audiosender.Start();

            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            // Stop TX Audio
            if (button2.Enabled == false)
            {
                comboBoxAudio.Enabled = true;
                button7.Enabled = true;
                button8.Enabled = false;
                checkBox3.Checked = false;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            DialogResult diag = MessageBox.Show("Salir de la aplicación?");
            if (diag == DialogResult.OK)
            {
                Application.ExitThread();
                Application.Exit();
            }
        }

        private void drawLatestImage(object sender, PaintEventArgs e)
        {
            if (_latestFrame != null)
            {
                _latestFrame = new Bitmap(_latestFrame, new Size(460, 270));
                e.Graphics.DrawImage(_latestFrame, 0, 0, _latestFrame.Width, _latestFrame.Height);

                if (button4.Enabled == false && button2.Enabled == false)
                {
                    // Transmitimos la imagen
                    // Comprimimos el frame
                    Bitmap cFrame = new Bitmap(_latestFrame, new Size(320, 240));

                    // Conversion a JPEG
                    jpegFrame = new MemoryStream();
                    cFrame.Save(jpegFrame, ImageFormat.Jpeg);

                    // Enviamos por el canal
                    video.sendJPEG(jpegFrame);
                    num_video++;
                }
            }
        }

        private void ChatThread(){
            // Mantenemos a la escucha el socket
            while(true){
                byte[] received = chat2server.Receive(ref chat2remote);

                msg = Encoding.UTF8.GetString(received, 0 , received.Length);
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

        private void comboBoxAudio_SelectedIndexChanged(object sender, EventArgs e)
        {
            record_source = (Guid)comboBoxAudio.SelectedValue;
        }

        class capture_device
        {
            public Guid DriverGuid { get; set; }
            public string Description { get; set; }
            public string ModuleName { get; set; }
        }

        List<capture_device> devices = new List<capture_device>();

        private void InitAudioCombo()
        {
            // Record Devices
            CaptureDevicesCollection captureDevicesCollection = new CaptureDevicesCollection();
            comboBoxAudio.ValueMember = "DriverGuid";
            comboBoxAudio.DisplayMember = "Description";
            foreach (DeviceInformation item in captureDevicesCollection)
            {
                devices.Add(new capture_device()
                {
                    Description = item.Description,
                    ModuleName = item.ModuleName,
                    DriverGuid = item.DriverGuid
                });
            }

            comboBoxAudio.DataSource = devices;
            comboBoxAudio.SelectedIndex = 1;
            record_source = (Guid)comboBoxAudio.SelectedValue;
        }

        private void InitAudioCapture()
        {
            // Configuramos DirectSound
            device = new Device();
            device.SetCooperativeLevel(this, CooperativeLevel.Normal);

            capture = new Capture(record_source);

            // Creamos Waveformat
            waveFormat = new WaveFormat
            {
                BitsPerSample = bitsPerSample,                                                                  // 16 bits
                BlockAlign = (short)(channels * (bitsPerSample / (short)8)),
                Channels = channels,                                                                            // Stereo
                AverageBytesPerSecond = (short)(channels * (bitsPerSample / (short)8)) * samplesPerSecond,      // 22kHz
                SamplesPerSecond = samplesPerSecond,                                                            // 22kHz
                FormatTag = WaveFormatTag.Pcm
            };

            captureBuffDesc = new CaptureBufferDescription
            {
                BufferBytes = waveFormat.AverageBytesPerSecond / 5,
                Format = waveFormat
            };

            bufferDesc = new BufferDescription
            {
                BufferBytes = waveFormat.AverageBytesPerSecond / 5,
                Format = waveFormat
            };

            bufferplayback = new SecondaryBuffer(bufferDesc, device);
            buffersize = captureBuffDesc.BufferBytes;
        }

        private void CreateNotifyPositions()
        {
            try
            {
                autoResetEvent = new AutoResetEvent(false);
                notify = new Notify(captureBuffer);
                BufferPositionNotify bufferPositionNotify1 = new BufferPositionNotify
                {
                    Offset = buffersize / 2 - 1,
                    EventNotifyHandle = autoResetEvent.SafeWaitHandle.DangerousGetHandle()
                };
                BufferPositionNotify bufferPositionNotify2 = new BufferPositionNotify
                {
                    Offset = buffersize - 1,
                    EventNotifyHandle = autoResetEvent.SafeWaitHandle.DangerousGetHandle()
                };

                notify.SetNotificationPositions(new BufferPositionNotify[] { bufferPositionNotify1, bufferPositionNotify2 });
            } catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Error on CreatePositionNotify", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendAudio()
        {
            try
            {
                // Capturamos el audio y lo enviamos por la red
                int halfbuffer = buffersize / 2;
                captureBuffer = new CaptureBuffer(captureBuffDesc, capture);
                CreateNotifyPositions();
                captureBuffer.Start(true);
                bool readFirstBufferPart = true;
                int offset = 0;
                MemoryStream memStream = new MemoryStream(halfbuffer);

                while (!button7.Enabled)
                {
                    // Esperamos un evento
                    autoResetEvent.WaitOne();
                    // Ponemos el puntero al principio del MS
                    memStream.Seek(0, SeekOrigin.Begin);
                    // Leemos el Buffer de Captura y lo guardamos en la primera mitad
                    captureBuffer.Read(offset, memStream, halfbuffer, LockFlag.None);
                    readFirstBufferPart = !readFirstBufferPart;
                    offset = readFirstBufferPart ? 0 : halfbuffer;

                    // Preparamos el stream de datos
                    //byte[] data = memStream.GetBuffer();    // Sin compresión
                    byte[] data = ALawEncoder.ALawEncode(memStream.GetBuffer());

                    // Enviamos via RTP al usuario.
                    audio.sendALaw(data);
                    num_audio++;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending audio.");
            }
        }

        private void analyzeVideo(object sender, EventArgs e)
        {
            // Calculamos los paq/s enviados
            label5.Text = String.Format("{0} paq/s", num_video);
            num_video = 0;
        }

        private void analyzeAudio(object sender, EventArgs e)
        {
            // Calculamos los paq/s enviados
            label6.Text = String.Format("{0} paq/s", num_audio);
            num_audio = 0;
        }
    }
}
