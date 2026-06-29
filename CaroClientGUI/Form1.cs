using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace CaroClientGUI
{
    public partial class Form1 : Form
    {
        TextBox txtIp;
        TextBox txtPort;
        TextBox txtName;
        TextBox txtRoom;
        TextBox txtChat;
        TextBox txtMessage;
        TextBox txtRank;

        Button btnConnect;
        Button btnCreateRoom;
        Button btnJoinRoom;
        Button btnSendChat;
        Button btnReplay;
        Button btnExit;

        Label lblStatus;
        Label lblRoom;
        Label lblSymbol;
        Label lblTime;
        Label lblPlayers;
        Label lblSkip;

        Button[,] cells = new Button[15, 15];

        TcpClient tcp;
        StreamReader reader;
        StreamWriter writer;
        object sendLock = new object();

        string mySymbol = "";
        bool myTurn = false;
        bool inGame = false;
        bool connected = false;

        public Form1()
        {
            BuildUI();
            SetBoardEnabled(false);
            FormClosing += Form1_FormClosing;
        }

        void BuildUI()
        {
            Text = "Caro Client";
            Width = 1100;
            Height = 690;
            StartPosition = FormStartPosition.CenterScreen;

            Label lblNameText = new Label();
            lblNameText.Text = "Tên:";
            lblNameText.Location = new Point(15, 15);
            lblNameText.Width = 35;

            txtName = new TextBox();
            txtName.Text = "Player";
            txtName.Location = new Point(55, 12);
            txtName.Width = 100;

            Label lblIp = new Label();
            lblIp.Text = "IP:";
            lblIp.Location = new Point(170, 15);
            lblIp.Width = 25;

            txtIp = new TextBox();
            txtIp.Text = "127.0.0.1";
            txtIp.Location = new Point(200, 12);
            txtIp.Width = 105;

            Label lblPort = new Label();
            lblPort.Text = "Port:";
            lblPort.Location = new Point(315, 15);
            lblPort.Width = 35;

            txtPort = new TextBox();
            txtPort.Text = "9999";
            txtPort.Location = new Point(355, 12);
            txtPort.Width = 65;

            btnConnect = new Button();
            btnConnect.Text = "Kết nối";
            btnConnect.Location = new Point(435, 10);
            btnConnect.Width = 80;
            btnConnect.Click += BtnConnect_Click;

            btnCreateRoom = new Button();
            btnCreateRoom.Text = "Tạo phòng";
            btnCreateRoom.Location = new Point(530, 10);
            btnCreateRoom.Width = 90;
            btnCreateRoom.Enabled = false;
            btnCreateRoom.Click += BtnCreateRoom_Click;

            txtRoom = new TextBox();
            txtRoom.Location = new Point(635, 12);
            txtRoom.Width = 80;

            btnJoinRoom = new Button();
            btnJoinRoom.Text = "Vào phòng";
            btnJoinRoom.Location = new Point(725, 10);
            btnJoinRoom.Width = 90;
            btnJoinRoom.Enabled = false;
            btnJoinRoom.Click += BtnJoinRoom_Click;

            lblStatus = new Label();
            lblStatus.Text = "Trạng thái: chưa kết nối";
            lblStatus.Location = new Point(15, 45);
            lblStatus.Width = 250;

            lblRoom = new Label();
            lblRoom.Text = "Phòng:";
            lblRoom.Location = new Point(275, 45);
            lblRoom.Width = 120;

            lblSymbol = new Label();
            lblSymbol.Text = "Quân:";
            lblSymbol.Location = new Point(405, 45);
            lblSymbol.Width = 80;

            lblTime = new Label();
            lblTime.Text = "Thời gian: 30";
            lblTime.Location = new Point(495, 45);
            lblTime.Width = 110;

            lblSkip = new Label();
            lblSkip.Text = "Quá giờ: 0/3";
            lblSkip.Location = new Point(615, 45);
            lblSkip.Width = 110;

            lblPlayers = new Label();
            lblPlayers.Text = "Người chơi:";
            lblPlayers.Location = new Point(735, 45);
            lblPlayers.Width = 300;

            Panel boardPanel = new Panel();
            boardPanel.Location = new Point(15, 80);
            boardPanel.Size = new Size(480, 480);
            boardPanel.BorderStyle = BorderStyle.FixedSingle;

            for (int i = 0; i < 15; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    Button b = new Button();
                    b.Location = new Point(j * 32, i * 32);
                    b.Size = new Size(32, 32);
                    b.Font = new Font("Arial", 10, FontStyle.Bold);
                    b.Tag = i + "|" + j;
                    b.Click += Cell_Click;

                    cells[i, j] = b;
                    boardPanel.Controls.Add(b);
                }
            }

            txtChat = new TextBox();
            txtChat.Location = new Point(520, 80);
            txtChat.Size = new Size(330, 360);
            txtChat.Multiline = true;
            txtChat.ReadOnly = true;
            txtChat.ScrollBars = ScrollBars.Vertical;

            txtRank = new TextBox();
            txtRank.Location = new Point(870, 80);
            txtRank.Size = new Size(190, 360);
            txtRank.Multiline = true;
            txtRank.ReadOnly = true;
            txtRank.ScrollBars = ScrollBars.Vertical;
            txtRank.Text = "Bảng xếp hạng";

            txtMessage = new TextBox();
            txtMessage.Location = new Point(520, 455);
            txtMessage.Width = 330;
            txtMessage.KeyDown += TxtMessage_KeyDown;

            btnSendChat = new Button();
            btnSendChat.Text = "Gửi";
            btnSendChat.Location = new Point(870, 453);
            btnSendChat.Width = 80;
            btnSendChat.Enabled = false;
            btnSendChat.Click += BtnSendChat_Click;

            btnReplay = new Button();
            btnReplay.Text = "Chơi lại";
            btnReplay.Location = new Point(520, 500);
            btnReplay.Width = 100;
            btnReplay.Enabled = false;
            btnReplay.Click += BtnReplay_Click;

            btnExit = new Button();
            btnExit.Text = "Thoát";
            btnExit.Location = new Point(640, 500);
            btnExit.Width = 100;
            btnExit.Click += BtnExit_Click;

            Controls.Add(lblNameText);
            Controls.Add(txtName);
            Controls.Add(lblIp);
            Controls.Add(txtIp);
            Controls.Add(lblPort);
            Controls.Add(txtPort);
            Controls.Add(btnConnect);
            Controls.Add(btnCreateRoom);
            Controls.Add(txtRoom);
            Controls.Add(btnJoinRoom);
            Controls.Add(lblStatus);
            Controls.Add(lblRoom);
            Controls.Add(lblSymbol);
            Controls.Add(lblTime);
            Controls.Add(lblSkip);
            Controls.Add(lblPlayers);
            Controls.Add(boardPanel);
            Controls.Add(txtChat);
            Controls.Add(txtRank);
            Controls.Add(txtMessage);
            Controls.Add(btnSendChat);
            Controls.Add(btnReplay);
            Controls.Add(btnExit);
        }

        void BtnConnect_Click(object sender, EventArgs e)
        {
            int port;

            if (!int.TryParse(txtPort.Text.Trim(), out port))
            {
                MessageBox.Show("Port không hợp lệ");
                return;
            }

            if (txtName.Text.Trim() == "")
            {
                MessageBox.Show("Bạn chưa nhập tên");
                return;
            }

            try
            {
                tcp = new TcpClient(txtIp.Text.Trim(), port);

                NetworkStream ns = tcp.GetStream();
                reader = new StreamReader(ns);
                writer = new StreamWriter(ns);
                writer.AutoFlush = true;

                connected = true;

                btnConnect.Enabled = false;
                btnCreateRoom.Enabled = true;
                btnJoinRoom.Enabled = true;
                btnSendChat.Enabled = true;
                btnReplay.Enabled = true;

                txtName.Enabled = false;
                txtIp.Enabled = false;
                txtPort.Enabled = false;

                lblStatus.Text = "Trạng thái: đã kết nối";
                Send("NAME|" + txtName.Text.Trim());

                Thread t = new Thread(Receive);
                t.IsBackground = true;
                t.Start();
            }
            catch
            {
                MessageBox.Show("Không kết nối được server");
            }
        }

        void BtnCreateRoom_Click(object sender, EventArgs e)
        {
            Send("CREATE_ROOM");
        }

        void BtnJoinRoom_Click(object sender, EventArgs e)
        {
            if (txtRoom.Text.Trim() == "")
            {
                MessageBox.Show("Bạn chưa nhập ID phòng");
                return;
            }

            Send("JOIN_ROOM|" + txtRoom.Text.Trim());
        }

        void BtnSendChat_Click(object sender, EventArgs e)
        {
            SendChat();
        }

        void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendChat();
            }
        }

        void SendChat()
        {
            if (!connected)
                return;

            string text = txtMessage.Text.Trim();

            if (text == "")
                return;

            Send("CHAT|" + text);
            txtMessage.Clear();
        }

        void BtnReplay_Click(object sender, EventArgs e)
        {
            if (!connected)
                return;

            Send("REPLAY");
            AddChat("Bạn đã gửi yêu cầu chơi lại");
        }

        void BtnExit_Click(object sender, EventArgs e)
        {
            CloseConnection();
            Close();
        }

        void Cell_Click(object sender, EventArgs e)
        {
            if (!inGame)
            {
                MessageBox.Show("Game chưa bắt đầu");
                return;
            }

            if (!myTurn)
            {
                MessageBox.Show("Chưa đến lượt bạn");
                return;
            }

            Button b = sender as Button;

            if (b == null)
                return;

            if (b.Text != "")
                return;

            string[] pos = b.Tag.ToString().Split('|');
            Send("MOVE|" + pos[0] + "|" + pos[1]);
        }

        void Receive()
        {
            try
            {
                while (connected)
                {
                    string msg = reader.ReadLine();

                    if (msg == null)
                        break;

                    try
                    {
                        BeginInvoke(new Action<string>(HandleMessage), msg);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            catch
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        lblStatus.Text = "Trạng thái: mất kết nối";
                        SetBoardEnabled(false);
                    }));
                }
                catch
                {
                }
            }
        }

        void HandleMessage(string msg)
        {
            string[] data = msg.Split('|');

            if (data.Length == 0)
                return;

            if (data[0] == "CONNECTED")
            {
                lblStatus.Text = "Trạng thái: " + GetText(data, 1, "đã kết nối");
            }
            else if (data[0] == "ROOM_ID")
            {
                string roomId = GetText(data, 1, "");
                lblRoom.Text = "Phòng: " + roomId;
                txtRoom.Text = roomId;
                AddChat("ID phòng: " + roomId);
            }
            else if (data[0] == "WAIT")
            {
                lblStatus.Text = "Trạng thái: chờ đối thủ";
                AddChat(GetText(data, 1, "Đang chờ người chơi khác"));
            }
            else if (data[0] == "JOIN_OK")
            {
                AddChat(GetText(data, 1, "Vào phòng thành công"));
            }
            else if (data[0] == "START")
            {
                mySymbol = GetText(data, 1, "");
                myTurn = GetText(data, 2, "0") == "1";
                inGame = true;

                ClearBoard();

                if (data.Length >= 4)
                    lblRoom.Text = "Phòng: " + data[3];

                lblSymbol.Text = "Quân: " + mySymbol;
                lblStatus.Text = myTurn ? "Trạng thái: đến lượt bạn" : "Trạng thái: chờ đối thủ";
                lblTime.Text = "Thời gian: 30";

                if (data.Length >= 6)
                    lblPlayers.Text = GetText(data, 4, "Bạn") + " - " + GetText(data, 5, "Đối thủ");

                SetBoardEnabled(myTurn);
                AddChat("Bắt đầu. Bạn là " + mySymbol);
            }
            else if (data[0] == "MOVE")
            {
                if (data.Length < 4)
                    return;

                int row;
                int col;

                if (!int.TryParse(data[1], out row))
                    return;

                if (!int.TryParse(data[2], out col))
                    return;

                if (row < 0 || row >= 15 || col < 0 || col >= 15)
                    return;

                string symbol = data[3];

                cells[row, col].Text = symbol;
                cells[row, col].Enabled = false;
            }
            else if (data[0] == "TURN")
            {
                myTurn = GetText(data, 1, "0") == "1";
                lblStatus.Text = myTurn ? "Trạng thái: đến lượt bạn" : "Trạng thái: chờ đối thủ";

                if (inGame)
                    SetBoardEnabled(myTurn);
            }
            else if (data[0] == "TIME")
            {
                lblTime.Text = "Thời gian: " + GetText(data, 1, "30");
                lblSkip.Text = "Quá giờ: " + GetText(data, 2, "0") + "/3";
            }
            else if (data[0] == "END")
            {
                inGame = false;
                myTurn = false;
                SetBoardEnabled(false);

                string result = GetText(data, 1, "END");
                string text = GetText(data, 2, "Ván đấu kết thúc");

                if (result == "WIN")
                    lblStatus.Text = "Trạng thái: bạn thắng";
                else if (result == "LOSE")
                    lblStatus.Text = "Trạng thái: bạn thua";
                else if (result == "DRAW")
                    lblStatus.Text = "Trạng thái: hòa";
                else
                    lblStatus.Text = "Trạng thái: kết thúc ván";

                AddChat(text);
                MessageBox.Show(text);
            }
            else if (data[0] == "CHAT")
            {
                string text = "";

                if (msg.StartsWith("CHAT|") && msg.Length > 5)
                    text = msg.Substring(5);

                AddChat(text);
            }
            else if (data[0] == "RANK")
            {
                ShowRank(msg);
            }
            else if (data[0] == "REPLAY_ASK")
            {
                string text = GetText(data, 1, "Đối thủ muốn chơi lại");
                AddChat(text);
                MessageBox.Show(text + ". Nếu muốn chơi lại hãy bấm nút Chơi lại.");
            }
            else if (data[0] == "LEFT")
            {
                inGame = false;
                myTurn = false;
                SetBoardEnabled(false);

                string text = GetText(data, 1, "Đối thủ đã thoát khỏi phòng");
                lblStatus.Text = "Trạng thái: đối thủ đã thoát";
                AddChat(text);
                MessageBox.Show(text);
            }
            else if (data[0] == "ERROR")
            {
                MessageBox.Show(GetText(data, 1, "Có lỗi xảy ra"));
            }
        }

        void ShowRank(string msg)
        {
            string text = "Bảng xếp hạng" + Environment.NewLine;
            text += "Tên   T  B  H" + Environment.NewLine;

            if (msg.StartsWith("RANK|") && msg.Length > 5)
            {
                string body = msg.Substring(5);
                string[] rows = body.Split(';');

                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i].Trim() == "")
                        continue;

                    string[] c = rows[i].Split(',');

                    if (c.Length >= 4)
                        text += c[0] + "   " + c[1] + "  " + c[2] + "  " + c[3] + Environment.NewLine;
                }
            }

            txtRank.Text = text;
        }

        string GetText(string[] data, int index, string defaultText)
        {
            if (data.Length > index)
                return data[index];

            return defaultText;
        }

        void Send(string msg)
        {
            lock (sendLock)
            {
                try
                {
                    if (writer != null)
                        writer.WriteLine(msg);
                }
                catch
                {
                }
            }
        }

        void ClearBoard()
        {
            for (int i = 0; i < 15; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    cells[i, j].Text = "";
                    cells[i, j].Enabled = true;
                }
            }
        }

        void SetBoardEnabled(bool enabled)
        {
            for (int i = 0; i < 15; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    if (cells[i, j].Text == "")
                        cells[i, j].Enabled = enabled;
                    else
                        cells[i, j].Enabled = false;
                }
            }
        }

        void AddChat(string text)
        {
            txtChat.AppendText(text + Environment.NewLine);
        }

        void CloseConnection()
        {
            if (!connected)
                return;

            Send("EXIT");
            connected = false;

            try
            {
                if (tcp != null)
                    tcp.Close();
            }
            catch
            {
            }
        }

        void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseConnection();
        }
    }
}
