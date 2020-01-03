using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// UDP y Multicast
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
using Timer = System.Windows.Forms.Timer;

// RTP
//using RTPStream;

namespace videochat_client
{
    public partial class Form1 : Form
    {
        // Multicast
        private IPAddress multicast = IPAddress.Parse("224.0.0.4");
        private static UdpClient videoclient, chat1client, chat2client, audioclient;
        private static IPEndPoint videoremote, chat1remote, chat2remote, audioremote;
        private int videoport = 45040;
        private int chat1port = 45041;
        private int chat2port = 45042;
        private int audioport = 45043;

        // Audio
        //private Guid record_source;
        private short channels = 1;
        private short bitsPerSample = 16;
        private int samplesPerSecond = 22050;

        private byte[] audioBytes;

        private Thread receivedAudio;
        private Device device;
        //private Capture capture;
        private WaveFormat waveFormat;
        //private Buffer buffer;
        private BufferDescription bufferDesc;
        private SecondaryBuffer bufferplayback;
        //private int buffersize = 100000;
        //private CaptureBuffer captureBuffer;
        //private CaptureBufferDescription captureBuffDesc;

        // Video
        private Thread receivedVideo;
        private byte[] received;
        //private byte[] packet;
        private Image frame;

        // Chat
        private String username;
        private String msg;
        private byte[] encodedmsg;

        // QoA
        private Timer timer1;
        private Timer timer2;
        private int num_video, num_audio;
        private long video_prev_time, audio_prev_time;
        private int video_prev_timestamp, audio_prev_timestamp;
        private double audio_delay, audio_jitter;
        private double video_delay, video_jitter;

        public Form1()
        {
            InitializeComponent();

            richTextBox1.Enabled = false;
            button1.Enabled = false;
            button2.Enabled = false;
            listBox1.Items.Add("*** Ingresa tu usuario y pulsa conectar ***");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Enviar mensaje al chat
            if (richTextBox1.Text != "")
            {
                // Enviamos al multicast
                try
                {
                    msg = String.Format("{0}: {1}", username, richTextBox1.Text);
                    encodedmsg = Encoding.UTF8.GetBytes(msg);
                    chat2client.Send(encodedmsg, encodedmsg.Length, chat2remote);
                }
                catch (Exception ex)
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

        private void button3_Click(object sender, EventArgs e)
        {
            // Hemos pulsado conectar

            // Guardamos el usuario del chat
            if (richTextBox2.Text == "")
            {
                richTextBox2.Text = "AnonymousClient";
                richTextBox2.Enabled = false;
                username = "AnonymousClient";
            }
            else
            {
                username = richTextBox2.Text;
                richTextBox2.Enabled = false;
            }

            button2.Enabled = true;
            button3.Enabled = false;
            richTextBox1.Enabled = true;
            button1.Enabled = true;
            listBox1.Items.Clear();
            listBox1.Items.Add(String.Format("*** Bienvenido {0} a la sala de chat ***", username));

            // Iniciamos los canales de comunicacion
            try
            {
                // Recepcion de video
                videoclient = new UdpClient();
                videoremote = new IPEndPoint(IPAddress.Any, videoport);
                videoclient.Client.Bind(videoremote);
                videoclient.JoinMulticastGroup(multicast);

                receivedVideo = new Thread(new ThreadStart(VideoThread));
                receivedVideo.Start();

                // Timer para los paq/s
                timer1 = new Timer();
                timer1.Tick += new EventHandler(analyzeVideo);  // Sacamos parametros de la TX
                timer1.Interval = 1000;                         // Cada 1000 ms
                timer1.Start();

                // QoA
                video_prev_time = 0;
                video_prev_timestamp = 0;

                // Recepcion de audio
                audioclient = new UdpClient();
                audioremote = new IPEndPoint(IPAddress.Any, audioport);
                audioclient.Client.Bind(audioremote);
                audioclient.JoinMulticastGroup(multicast);

                // Inicializamos el audio
                InitCaptureSound();
                // Inicializamos la recepción de Audio
                receivedAudio = new Thread(new ThreadStart(AudioThread));
                receivedAudio.Start();

                // Timer para los paq/s
                timer2 = new Timer();
                timer2.Tick += new EventHandler(analyzeAudio);  // Sacamos parametros de la TX
                timer2.Interval = 1000;                         // Cada 1000 ms
                timer2.Start();

                // QoA
                audio_prev_time = 0;
                audio_prev_timestamp = 0;

                // Enviar Chat
                chat2client = new UdpClient();
                chat2remote = new IPEndPoint(multicast, chat2port);
                chat2client.JoinMulticastGroup(multicast);

                // Recibir Chat
                chat1client = new UdpClient(chat1port);
                chat1remote = null;
                chat1client.JoinMulticastGroup(multicast);

                Thread t = new Thread(this.ChatThread);
                t.Start();
                CheckForIllegalCrossThreadCalls = false;
            } catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                checkBox4.Checked = true;
            }
        }

        private void ChatThread()
        {
            // Mantenemos a la escucha el socket
            while (true)
            {
                byte[] received = chat1client.Receive(ref chat1remote);

                msg = Encoding.UTF8.GetString(received);
                listBox1.Items.Add(msg);
            }
        }

        private void VideoThread()
        {
            while (true)
            {
                try
                {
                    received = videoclient.Receive(ref videoremote);

                    // Convertimos a imagen
                    byte[] imagenBytes = getPayloadRTP(received);
                    num_video++;

                    //MemoryStream packet = new MemoryStream(received);
                    //byte[] imagenBytes;
                    //byte[] buffer = new byte[received.Length - 12];
                    //using (MemoryStream ms = new MemoryStream())        // Guardamos la imagen en un nuevo memory stream
                    //{ 
                    //    int read;
                    //    packet.Seek(12, SeekOrigin.Begin);      // Ponemos el puntero a partir de la cabecera RTP
                    //    while ((read = packet.Read(buffer, 0, buffer.Length)) > 0)
                    //    {
                    //        ms.Write(buffer, 0, read);
                    //    }
                    //    imagenBytes = ms.ToArray();
                    //}

                    long video_time = (long)((DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds) / 1000;
                    int video_timestamp = (int)getTimestampRTP(received);

                    // Calculamos Delay + Jitter
                    if (video_prev_timestamp == 0)
                    {
                        video_jitter = 0;
                    }
                    else
                    {
                        video_delay = (video_time - video_prev_time) - (video_timestamp * 0.00001 - video_prev_timestamp * 0.00001);
                        video_jitter = video_jitter + (Math.Abs(video_delay) - video_jitter) / 16;
                    }

                    // Guardamos la información de timestamp + time
                    video_prev_timestamp = video_timestamp;
                    video_prev_time = video_time;

                    // Actualizamos las labels
                    label7.Text = String.Format("Delay: {0:0.00} ms", video_delay);
                    label8.Text = String.Format("Jitter: {0:0.00} ms", video_jitter);

                    frame = Image.FromStream(new MemoryStream(imagenBytes));
                    pictureBox1.Image = new Bitmap(frame, new Size(pictureBox1.Width, pictureBox1.Height));

                    // Marcamos como que recibimos RTP Video
                    checkBox2.Checked = true;
                }
                catch (Exception ex)
                {

                }
            }
        }

        public void InitCaptureSound()
        {
            device = new Device();
            device.SetCooperativeLevel(this, CooperativeLevel.Normal);

            //capture = new Capture();

            // Creamos el WaveFormat
            waveFormat = new WaveFormat
            {
                BitsPerSample = bitsPerSample,                                                                  // 16 bits
                BlockAlign = (short)(channels * (bitsPerSample / (short)8)),
                Channels = channels,                                                                            // Stereo
                AverageBytesPerSecond = (short)(channels * (bitsPerSample / (short)8)) * samplesPerSecond,      // 22kHz
                SamplesPerSecond = samplesPerSecond,                                                            // 22kHz
                FormatTag = WaveFormatTag.Pcm
            };

            //captureBuffDesc = new CaptureBufferDescription
            //{
            //    BufferBytes = waveFormat.AverageBytesPerSecond / 5,
            //    Format = waveFormat
            //};

            bufferDesc = new BufferDescription
            {
                BufferBytes = waveFormat.AverageBytesPerSecond / 5,
                Format = waveFormat
            };

            bufferplayback = new SecondaryBuffer(bufferDesc, device);
            //buffersize = captureBuffDesc.BufferBytes;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult diag = MessageBox.Show("Salir de la aplicacion?");
            if (diag == DialogResult.OK)
            {
                Application.ExitThread();
                Application.Exit();
            }
        }

        private void AudioThread()
        {
            try
            {
                //IsThreadReceivedEnd = false;

                byte[] byteData;

                while (!button3.Enabled)
                {
                    // Recibimos la info
                    try
                    {
                        try
                        {
                            byteData = audioclient.Receive(ref audioremote);

                            byte[] audioBytes = getPayloadRTP(byteData);
                            num_audio++;

                            // Extraemos el payload del paquete RTP
                            //MemoryStream packet = new MemoryStream(byteData);
                            //byte[] audioBytes;
                            //byte[] buffer = new byte[byteData.Length - 12];     // Quitamos el tamaño de la cabecera RTP
                            //using (MemoryStream ms = new MemoryStream())        // Guardamos la imagen en un nuevo memory stream
                            //{
                            //    int read;
                            //    packet.Seek(12, SeekOrigin.Begin);              // Ponemos el puntero a partir de la cabecera RTP
                            //    while ((read = packet.Read(buffer, 0, buffer.Length)) > 0)
                            //    {
                            //        ms.Write(buffer, 0, read);
                            //    }
                            //    audioBytes = ms.ToArray();
                            //}

                            long audio_time = (long)((DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds ) / 1000;
                            int audio_timestamp = (int)getTimestampRTP(byteData);

                            // Calculamos Delay + Jitter
                            if(audio_prev_timestamp == 0)
                            {
                                audio_jitter = 0;
                            } else
                            {
                                audio_delay = (audio_time - audio_prev_time) - (audio_timestamp * 0.000125 - audio_prev_timestamp * 0.000125);
                                audio_jitter = audio_jitter + (Math.Abs(audio_delay) - audio_jitter) / 16;
                            }

                            // Guardamos la información de timestamp + time
                            audio_prev_timestamp = audio_timestamp;
                            audio_prev_time = audio_time;

                            // Actualizamos las labels
                            label5.Text = String.Format("Delay: {0:0.00} ms", audio_delay);
                            label6.Text = String.Format("Jitter: {0:0.00} ms", audio_jitter);

                            //long timestamp = DateTime2Unix(DateTime.Now);
                            //uint timestamp = (uint)(DateTime.Now.ToUniversalTime().Ticks / TimeSpan.TicksPerMillisecond);

                            //label3.Text = String.Format("{0}", getTimestampRTP(byteData));
                            //label4.Text = getSequenceRTP(byteData).ToString();

                            // Marcamos como que recibimos RTP Audio
                            checkBox1.Checked = true;

                            // Teoria
                            // G711 comprime la info al 50%, necesitamos hacer el buffer más grande
                            byte[] byteDecodedData = new byte[audioBytes.Length * 2];

                            //Usando ALaw
                            ALawDecoder.ALawDecode(audioBytes, out byteDecodedData);
                            //Sin comprension
                            //byteDecodedData = new byte[audioBytes.Length];
                            //byteDecodedData = audioBytes;

                            // Reproducimos la información recibida.
                            checkBox3.Checked = true;
                            bufferplayback = new SecondaryBuffer(bufferDesc, device);
                            bufferplayback.Write(0, byteDecodedData, LockFlag.None);
                            bufferplayback.Play(0, BufferPlayFlags.Default);
                        }
                        catch (Exception)
                        {
                            return;
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            } catch(Exception ex)
            {
                MessageBox.Show("Error on audiothread.");
            }

            //IsThreadReceiveEnd = true;
        }

        #region RTP
        private uint getTimestampRTP(byte[] receivedData)
        {
            uint timestamp;

            MemoryStream packet = new MemoryStream(receivedData);
            packet.Seek(4, SeekOrigin.Begin);
            timestamp = ReadUInt32(packet);

            return timestamp;
        }

        private ushort getSequenceRTP(byte[] receivedData)
        {
            ushort seq;
            
            MemoryStream packet = new MemoryStream(receivedData);  
            packet.Seek(2, SeekOrigin.Begin);
            seq = (ushort)ReadUInt16(packet);

            return seq;

        }

        private byte[] getPayloadRTP(byte[] receivedData)
        {
            // Extraemos el payload del paquete RTP
            MemoryStream packet = new MemoryStream(receivedData);
            byte[] payload;
            byte[] buffer = new byte[receivedData.Length - 12];     // Quitamos el tamaño de la cabecera RTP
            using (MemoryStream ms = new MemoryStream())            // Guardamos el payload en un nuevo memory stream
            {
                int read;
                packet.Seek(12, SeekOrigin.Begin);                  // Ponemos el puntero a partir de la cabecera RTP
                while ((read = packet.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                payload = ms.ToArray();
            }

            return payload;
        }
        #endregion

        #region utils
        private static uint ReadUInt32(Stream stream)
        {
            return (uint)((stream.ReadByte() << 24)
                            + (stream.ReadByte() << 16)
                            + (stream.ReadByte() << 8)
                            + (stream.ReadByte())); 
        }

        private static ushort ReadUInt16(Stream stream)
        {
            return (ushort)((stream.ReadByte() << 8)
                            + (stream.ReadByte()));
        }

        private static long DateTime2Unix(DateTime now)
        {
            TimeSpan timeSpan = now - new DateTime(1970, 1, 1, 0, 0, 0);

            return (long)timeSpan.TotalSeconds;
        }
        #endregion

        private void analyzeVideo(object sender, EventArgs e)
        {
            // Actualizamos el Form
            label4.Text = String.Format("{0} paq/s", num_video);
            num_video = 0;
        }

        private void analyzeAudio(object sender, EventArgs e)
        {
            // Actualizamos el Form
            label3.Text = String.Format("{0} paq/s", num_audio);
            num_audio = 0;
        }
    }
}
