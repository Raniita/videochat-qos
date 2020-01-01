using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private byte[] header;
        private byte[] payload;
        private byte[] packet;
        private int sequence;
        private static int interval = 100;


        // Constructor stream RTP
        public RTP(String m_uuid, UdpClient m_client, IPAddress m_multicast, IPEndPoint m_remote)
        {
            this.uuid = m_uuid;
            this.client = m_client;
            this.remote = m_remote;
            this.multicast = m_multicast;
            this.sequence = 0;

            client.JoinMulticastGroup(multicast);
        }

        public String test(String a)
        {
            return "OK!";
        }

        // Construimos el paquete RTP, con la info + header
        private byte[] newPacket(byte[] data, int nSeq, int type)
        {
            int timestamp = nSeq * interval;
            header = createHeader(nSeq, timestamp, type);
            payload = new byte[data.Length];
            payload = data;

            packet = new byte[data.Length + header.Length];

            for (int i = 0; i < header.Length; i++)
            {
                packet[i] = header[i];
            }

            for (int j = 12; j < packet.Length; j++)
            {
                packet[j] = payload[j - 12];
            }

            return packet;
        }

        private byte[] createHeader(int nSeq, int mTimestamp, int mPayloadType)
        {
            if(this.sequence >= 65535)
            {
                nSeq = 0;
                this.sequence = 0;
            } 
            
            int version = 2;
            int padding = 0;
            int extension = 0;
            int csrcCount = 0;
            int marker = 0;
            int payloadType = mPayloadType;
            int sequence = nSeq;
            long timestamp = mTimestamp;
            long SSRC = 0;

            byte[] buf = new byte[12];

            // Assembling according the spec
            // Byte 1.
            buf[0] = (byte)((version & 0x3) << 6 | (padding & 0x1) << 5 | (extension & 0x0) << 4 | (csrcCount & 0x0));

            // Byte 2.
            buf[1] = (byte)((marker & 0x1) << 7 | payloadType & 0x7f);

            // Byte 3 y 4. Numero de secuencia. MSB + LSB. Big endian
            buf[2] = (byte)((sequence & 0xff00) >> 8);
            buf[3] = (byte)(sequence & 0x00ff);

            // Timestamp on 4 bytes. Big endian
            buf[4] = (byte)((timestamp & 0xff000000) >> 24);
            buf[5] = (byte)((timestamp & 0x00ff0000) >> 16);
            buf[6] = (byte)((timestamp & 0x0000ff00) >> 8);
            buf[7] = (byte)(timestamp & 0x000000ff);

            // CSRC
            buf[8] = (byte)((SSRC & 0xff000000) >> 24);
            buf[9] = (byte)((SSRC & 0x00ff0000) >> 16);
            buf[10] = (byte)((SSRC & 0x0000ff00) >> 8);
            buf[11] = (byte)(SSRC & 0x000000ff);

            // Devolvemos todo el header
            return buf;
        }

        public String sendPacket(byte[] buffer)
        {
            // Enviamos la info por el canal
            byte[] toSend = newPacket(buffer, sequence, 20);

            try
            {
                client.Send(toSend, toSend.Length, remote);
                sequence++;
            }
            catch (Exception e)
            {
                return "KO";
            }

            return "OK";
        }

        public String sendJPEG(MemoryStream frame)
        {
            // Enviamos imagen por el canal
            byte[] toSend = newPacket(frame.ToArray(), sequence, 26);

            try
            {
                client.Send(toSend, toSend.Length, remote);
                sequence++;
            }
            catch (Exception e)
            {
                return "KO";
            }
            return "OK";
        }

        public String sendALaw(byte[] buffer)
        {
            // Enviamos Alaw por el canal
            // Payload type == 0 -> Audio
            byte[] toSend = newPacket(buffer, sequence, 8);

            try
            {
                client.Send(toSend, toSend.Length, remote);
                sequence++;
            } 
            catch(Exception e)
            {
                return "KO";
            }
            return "OK";
        }

        public String sendAudio(byte[] buffer)
        {
            // Enviamos Audio sin comprimir por el canal
            // Payload type == 0 -> Audio
            byte[] toSend = newPacket(buffer, sequence, 0);

            try
            {
                client.Send(toSend, toSend.Length, remote);
                sequence++;
            }
            catch (Exception e)
            {
                return "KO";
            }
            return "OK";
        }
    }
}
