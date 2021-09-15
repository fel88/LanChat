using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LanChat
{
    public class TcpRoutine
    {
        public static void ErrorSend(NetworkStream stream)
        {
            var writer = new StreamWriter(stream);
            writer.WriteLine("error");
            writer.Flush();
        }

        public static void SendAck(NetworkStream stream)
        {
            var writer = new StreamWriter(stream);

            writer.WriteLine("ack");
            writer.Flush();
        }

        public void SendAll(string ln)
        {
            List<ConnectionInfo> infos = new List<ConnectionInfo>();
            foreach (var connectionInfo in streams)
            {
                try
                {
                    StreamWriter wrt = new StreamWriter(connectionInfo.Stream);
                    wrt.WriteLine(ln);
                    wrt.Flush();
                }
                catch (Exception ex)
                {
                    infos.Add(connectionInfo);
                }
            }
            lock (streams)
            {
                foreach (var connectionInfo in infos)
                {
                    streams.Remove(connectionInfo);
                }
            }
        }
     

        public List<ConnectionInfo> streams = new List<ConnectionInfo>();

        public virtual void NewClient()
        {

        }
        public void InitTcp(IPAddress ip, int port, Action<NetworkStream, object> threadProcessor, Func<object> factory = null)
        {
            server1 = new TcpListener(ip, port);
            server1.Start();
           

            Thread th = new Thread(() =>
            {
                while (true)
                {
                    var client = server1.AcceptTcpClient();
                    Console.WriteLine("client accepted");
                    lock (streams)
                    {
                        var stream = client.GetStream();
                        var addr = (client.Client.RemoteEndPoint as IPEndPoint).Address;
                        var _port = (client.Client.RemoteEndPoint as IPEndPoint).Port;
                        var obj = factory != null ? factory() : null;
                        var cinf = new ConnectionInfo() { Stream = stream, Client = client, Ip = addr, Port = _port, Tag = obj };

                        streams.Add(cinf);
                        Thread thp = new Thread(() => { threadProcessor(stream, cinf); });
                        thp.IsBackground = true;
                        thp.Start();                        
                    }

                }
            });
            th.IsBackground = true;
            th.Start();
        }

        private TcpListener server1;


    }
}