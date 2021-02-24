using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace LanChat
{
    public class ChatClient
    {
        private NetworkStream clientStream;

        public string Nickname = Environment.UserName;
        TcpClient client = new TcpClient();
        public bool Connected
        {
            get
            {
                return client.Connected;
            }
        }
        
        private Thread th2;
        public void SendFile(string s, UserInfo target, string savePath = "", Action<int> progress = null)
        {
            Thread th = new Thread(() =>
            {

                lock (clientStream)
                {
                    fileDownloadSemaphore = new Semaphore(0, 1);
                    var wr = new StreamWriter(clientStream);
                    var fin = new FileInfo(s);
                    var f = new FileInfo(s);

                    int chunkSize = ChunkSize * 1024;
                    var fs = File.OpenRead(s);
                    var pp = Path.Combine(savePath, f.Name);

                    for (int i = 0; i < fin.Length; i += chunkSize)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("<?xml version=\"1.0\"?>");
                        sb.AppendLine("<root>");
                        sb.AppendLine(string.Format("<file sender=\"{0}\" target=\"{1}\" name=\"{2}\" size=\"{3}\" >",
                            Nickname, target.Name, pp, fin.Length));
                        int sz = (int)Math.Min(chunkSize, fin.Length - i);
                        byte[] bb = new byte[sz];
                        fs.Read(bb, 0, sz);
                        var bs = Convert.ToBase64String(bb);

                        sb.AppendFormat("<chunk offset=\"{0}\" size=\"{1}\">", i, sz);
                        sb.AppendLine("<![CDATA[" + bs + "]]>");
                        sb.AppendFormat("</chunk>");
                        sb.AppendLine("</file>");
                        sb.AppendLine("</root>");

                        var bt = Encoding.UTF8.GetBytes(sb.ToString());
                      
                        var bs64 = Convert.ToBase64String(bt);

                        wr.WriteLine("FILE=" + bs64);
                        wr.Flush();
                        var perc = ((i + sz) / (decimal)fin.Length) * 100m;
                        progress((int)perc);
                   
                        fileDownloadSemaphore.WaitOne();

                    }

                    fs.Close();

                }

            });
            th.Start();
            th.IsBackground = true;
        }

        internal void SendMsg(string txt)
        {

            var bt = Encoding.UTF8.GetBytes(txt);

            var bs64 = Convert.ToBase64String(bt);
            var wr = new StreamWriter(clientStream);

            wr.WriteLine("MSG=" + bs64);
            wr.Flush();
        }

        public Action OnClientsListUpdate;
        public Action<string, string> OnMsgRecieved;
        public Action<string, string, long> OnFileRecieved;
        public Action<string, string, int, int, long> OnFileChunkRecieved;
        public Action<string> OnError;
        public int ChunkSize = 32;
        private Semaphore fileDownloadSemaphore;
        public List<UserInfo> Users = new List<UserInfo>();

        public void Connect(string ip, int port)
        {
            client.Connect(IPAddress.Parse(ip), port);
            clientStream = client.GetStream();

            var wrt = new StreamWriter(clientStream);
            var rdr = new StreamReader(clientStream);

            wrt.WriteLine("INIT=" + Nickname);
            wrt.Flush();
            th2 = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        while (true)
                        {
                          
                            var ln = rdr.ReadLine();
                          
                            if (ln.StartsWith("CLIENTS"))
                            {
                                ln = ln.Substring("CLIENTS".Length + 1);

                                var bs64 = Convert.FromBase64String(ln);

                                var str = Encoding.UTF8.GetString(bs64);
                                var doc = XDocument.Parse(str);
                                lock (Users)
                                {
                                    Users.Clear();
                                    foreach (var descendant in doc.Descendants("client"))
                                    {
                                        var nm = descendant.Attribute("name").Value;

                                        Users.Add(new UserInfo() { Name = nm });
                                    }
                                }
                                OnClientsListUpdate?.Invoke();
                            }
                            if (ln.StartsWith("MSG"))
                            {
                                ln = ln.Substring(4);                              
                                var bs64 = Convert.FromBase64String(ln);
                             
                                var str = Encoding.UTF8.GetString(bs64);
                                var doc = XDocument.Parse(str);
                                var msg = doc.Descendants("message").First();
                                str = msg.Value;
                                var user = msg.Attribute("user").Value;

                                OnMsgRecieved?.Invoke(user, str);
                              

                            }
                            if (ln.StartsWith("ACK"))//file download acknowledge delivery
                            {
                                ln = ln.Substring("ACK".Length + 1);
                                fileDownloadSemaphore.Release(1);
                            }


                            if (ln.StartsWith("FILE"))
                            {
                                ln = ln.Substring("FILE".Length + 1);

                                var bs64 = Convert.FromBase64String(ln);

                                var str = Encoding.UTF8.GetString(bs64);
                                var doc = XDocument.Parse(str);


                                var fl = doc.Descendants("file").First();
                                var uin = fl.Attribute("sender").Value;
                                var size = int.Parse(fl.Attribute("size").Value);
                                var nm = fl.Attribute("name").Value;
                                var chunk = fl.Descendants("chunk").First();
                                var szval = int.Parse(chunk.Attribute("size").Value);
                                var offset = int.Parse(chunk.Attribute("offset").Value);
                                var data = Convert.FromBase64String(chunk.Value);

                                var perc = ((offset + szval) / (decimal)size) * 100m;

                                var path = Path.Combine("Saved/", nm);

                                var pathes =
                                    nm.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries)
                                        .Reverse()
                                        .Skip(1)
                                        .Reverse()
                                        .ToArray();
                                string pp = "Saved/";
                                if (!Directory.Exists("Saved/"))
                                {
                                    Directory.CreateDirectory("Saved/");
                                }
                                foreach (var ff in pathes)
                                {
                                    pp = Path.Combine(pp, ff);

                                    if (!Directory.Exists(pp))
                                    {
                                        Directory.CreateDirectory(pp);

                                    }
                                }


                                bool rewriteRequre = true;
                                if (File.Exists(path))
                                {
                                    var rd = File.OpenRead(path);
                                    byte[] readd = new byte[szval];
                                    rd.Seek(offset, SeekOrigin.Begin);
                                    rd.Read(readd, 0, readd.Length);
                                    rd.Close();
                                    //calc hash code
                                    var hash = Utils.CreateMD5(readd);
                                    var hash2 = Utils.CreateMD5(data);
                                    if (hash == hash2)
                                    {
                                        rewriteRequre = false;
                                    }
                                }


                                if (rewriteRequre)
                                {
                                    var pt = File.OpenWrite(path);
                                    if (pt.Length != size)
                                    {
                                        pt.SetLength(size);
                                    }
                                    pt.Seek(offset, SeekOrigin.Begin);
                                    pt.Write(data, 0, data.Length);
                                    pt.Close();
                                }
                             

                                //send ack

                                StringBuilder sb = new StringBuilder();
                                sb.AppendLine("<?xml version=\"1.0\"?>");
                                sb.AppendLine("<root>");
                                sb.AppendFormat("<ack target=\"{0}\"/>", uin);
                                sb.AppendLine("</root>");
                               
                                var bt = Encoding.UTF8.GetBytes(sb.ToString());                                
                                var ree = Convert.ToBase64String(bt);

                                wrt.WriteLine("ACK=" + ree);
                                wrt.Flush();
                                ////////

                                if ((int)perc == 100)
                                {
                                    OnFileRecieved?.Invoke(uin, path, size);
                                }
                                OnFileChunkRecieved?.Invoke(uin, path, data.Length, size, (int)perc);
                               

                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        OnError?.Invoke(ex.Message);
                    }

                }
            })
            {               
                IsBackground = true
            };
            th2.Start();

        }

        internal void FetchClients()
        {
            var wr = new StreamWriter(clientStream);
            wr.WriteLine("CLIENTS");
            wr.Flush();
        }
    }
}
