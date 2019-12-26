using System;
using System.IO;
using System.Net.Sockets;
using System.Net;

namespace RTPStream
{
    public class RTP : MarshalByRefObject
    {
        // Vars
        private String uuid;
        private UdpClient client;
        private IPEndPoint remote;
        private IPAddress multicast;

        // Definitions of RTP
        //version
        //
        //
        //
        
        // Constructor stream RTP
        public RTP(String m_uuid, UdpClient m_client, IPAddress m_multicast, IPEndPoint m_remote)
        {
            this.uuid = m_uuid;
            this.client = m_client;
            this.remote = m_remote;
            this.multicast = m_multicast;

            client = new UdpClient();
            client.JoinMulticastGroup(multicast);
        }

        public String sendJPEG(MemoryStream frame)
        {
            // Enviamos imagen por el canal

            // Inicializamos el header RTP
            // 32 x 4 bits de tamaño
            
            return "OK!";
        }

        public String sendPacket(byte[] buffer)
        {
            // Enviamos la imagen por el canal


            return "OK";
        }

        public String test(String a)
        {
            return "OK!";
        }
    }
}
