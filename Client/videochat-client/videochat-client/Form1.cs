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
//using Microsoft.DirectX.DirectSound;
//using Buffer = Microsoft.DirectX.DirectSound.Buffer;
//using Alaw;

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

        // Video
        private Thread receivedVideo;
        private byte[] received;
        private byte[] packet;
        private Image frame;

        // Chat
        private String username;
        private String msg;
        private byte[] encodedmsg;

        public Form1()
        {
            InitializeComponent();

            richTextBox1.Enabled = false;
            button1.Enabled = false;
            button2.Enabled = false;
            listBox1.Items.Add("Ingresa tu usuario y pulsa conectar ***");
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
                // Transmision de video
                videoclient = new UdpClient();
                videoremote = new IPEndPoint(IPAddress.Any, videoport);
                videoclient.Client.Bind(videoremote);
                videoclient.JoinMulticastGroup(multicast);

                this.receivedVideo = new Thread(new ThreadStart(this.VideoThread));
                receivedVideo.Start();

                // Transmision de audio
                audioclient = new UdpClient();
                audioremote = new IPEndPoint(IPAddress.Any, audioport);
                audioclient.Client.Bind(audioremote);
                audioclient.JoinMulticastGroup(multicast);

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
                    MemoryStream packet = new MemoryStream(received);
                    byte[] imagenBytes;
                    byte[] buffer = new byte[received.Length - 12];
                    using (MemoryStream ms = new MemoryStream())        // Guardamos la imagen en un nuevo memory stream
                    { 
                        int read;
                        packet.Seek(12, SeekOrigin.Begin);      // Ponemos el puntero a partir de la cabecera RTP
                        while ((read = packet.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        imagenBytes = ms.ToArray();
                    }

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
    }
}
