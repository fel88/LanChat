using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace LanChat
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            textBox3.Text = Environment.MachineName + "/" + Environment.UserName;
            //label11.Text = $"My IP: {GetLocalIPAddress()}";
            var host = Dns.GetHostEntry(Dns.GetHostName());

            List<string> ips = new List<string>();
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ips.Add(ip.ToString());
                }
            }
            foreach (var item in ips.OrderByDescending(z => z.StartsWith("192") ? 1 : 0))
            {
                comboBox1.Items.Add(item);
            }
            if (comboBox1.Items.Count == 0) { comboBox1.Visible = false; } else { comboBox1.SelectedIndex = 0; }
        }


        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
        ChatClient client = new ChatClient();

        public Dictionary<string, Bitmap> loaded = new Dictionary<string, Bitmap>();

        public static void Invoke(Control ctrl, Action act)
        {
            if (ctrl.InvokeRequired)
                ctrl.Invoke(act);
            else
                act();
        }

        public void UpdateClientsList()
        {
            Invoke(listView2, () =>
            {
                listView2.Items.Clear();
                foreach (var userInfo in client.Users)
                {
                    listView2.Items.Add(new ListViewItem(new string[] { userInfo.Name }) { Tag = userInfo });
                }
            });
        }

        void UpdateLed(Label label, string text, bool status)
        {
            label.Text = text;
            if (status)
            {
                label.BackColor = Color.Green;
                label.ForeColor = Color.White;
            }
            else
            {
                label.BackColor = Color.Yellow;
                label.ForeColor = Color.Gray;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            UpdateLed(label1, "connected", client.Connected);
            UpdateLed(label2, "server state", server != null);

            label3.Text = "clients: " + ((server != null) ? (server.streams.Count + "") : "null");
        }

        private ChatServer server;

        private bool isStarted = false;

        private void button1_Click(object sender, EventArgs e)
        {
            if (client == null || !client.Connected) return;
            client.SendMsg(richTextBox1.Text);
        }


        private void updateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (client == null || !client.Connected) return;
            client.FetchClients();
        }

        public UserInfo TargetUser;

        private void listView2_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listView2.FirstSelected() == null) return;

            var fri = listView2.FirstSelected();
            var fr = fri.Tag as UserInfo;
            TargetUser = fr;
            foreach (var item in listView2.Items.OfType<ListViewItem>())
            {
                var u = (item.Tag as UserInfo);
                if (u != TargetUser)
                {
                    item.BackColor = Color.White;
                    item.ForeColor = Color.Black;
                }
            }
            fri.BackColor = Color.Green;
            fri.ForeColor = Color.White;

        }


        private void button7_Click(object sender, EventArgs e)
        {
            if (TargetUser == null)
            {
                MessageBox.Show("Select target user first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var split = textBox4.Text.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
            foreach (var s in split)
            {
                client.SendFile(s, TargetUser, progress: (perc) =>
                {
                    Invoke(progressBar2, () =>
                    {
                        progressBar2.Value = (int)perc;
                        label6.Text = (int)perc + "%";
                    });
                });
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                textBox4.Text = "";
                foreach (var fileName in ofd.FileNames)
                {
                    textBox4.Text += fileName + ";";
                }
            }
        }

        public void GetFilesRec(DirectoryInfo d, List<FileInfo> list)
        {
            list.AddRange(d.GetFiles());
            foreach (var dd in d.GetDirectories())
            {
                GetFilesRec(dd, list);
            }
        }

        private void button11_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists("Saved"))
            {
                Directory.CreateDirectory("Saved/");
            }
            Process.Start(@"Saved\");
        }

        private void textBox6_TextChanged(object sender, EventArgs e)
        {
            try
            {
                client.ChunkSize = int.Parse(textBox6.Text);
                textBox6.BackColor = Color.White;
            }
            catch (Exception ex)
            {
                textBox6.BackColor = Color.Red;
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            if (!isStarted)
            {
                server = new ChatServer();
                server.Init(int.Parse(textBox1.Text));
                isStarted = true;
            }
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            client.OnClientsListUpdate = UpdateClientsList;
            client.OnError = (msg) =>
            {
                richTextBox1.Invoke((Action)(() =>
                {
                    richTextBox1.Text += $"{DateTime.Now.ToLongTimeString()}: {msg}";

                }));
            };
            client.OnFileRecieved = (uin, path, size) =>
            {
                progressBar1.Value = (int)100;
                label4.Text = (int)100 + "%";
                listView1.Items.Add(new ListViewItem(new string[]
            {
                                    DateTime.Now.ToLongTimeString(),
                                    uin,
                                   size/1024 + "Kb",
                                    "file recieved: " + path + "(size: " + size/1024 + "Kb)" +
                                    Environment.NewLine,
            })
                { Tag = path });
            };
            client.OnFileChunkRecieved = (uin, path, chunkSize, size, perc) =>
            {
                Invoke(listView1, () =>
                {
                    progressBar1.Value = (int)perc;
                    label4.Text = (int)perc + "%";
                });
            };
            client.OnMsgRecieved = (user, str) =>
            {
                Invoke((Action)(() =>
                {
                    listView1.Items.Add(new ListViewItem(new string[]
                {
                    DateTime.Now.ToLongTimeString() ,
                    user+"",
                    str.Length+"",
                    str
                })
                    { Tag = str });


                    richTextBox2.Invoke((Action)(() =>
                    {
                        richTextBox2.Text = str;
                    }));
                }));

            };
            client.Connect(textBox2.Text, int.Parse(textBox1.Text));
            client.FetchClients();
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            client.Nickname = textBox3.Text;
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.FirstSelected() == null) return;

            if (listView1.FirstSelected().Tag is string str1)
            {
                richTextBox2.Text = str1;
            }
            else if (listView1.FirstSelected().Tag is byte[] bb)
            {
                richTextBox2.Text = bb.Aggregate("", (x, y) => x + y.ToString("X") + "");

            }
        }

    }
}
