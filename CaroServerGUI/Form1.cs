using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace CaroServerGUI
{
    public partial class Form1 : Form
    {
        TextBox txtPort;
        Button btnStart;
        ListBox lstLog;

        TcpListener listener;
        bool running = false;

        Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        Dictionary<string, RankItem> ranks = new Dictionary<string, RankItem>();
        List<Player> players = new List<Player>();
        object roomLock = new object();
        object rankLock = new object();
        Random random = new Random();

        public Form1()
        {
            BuildUI();
            FormClosing += Form1_FormClosing;
        }

        void BuildUI()
        {
            Text = "Caro Server";
            Width = 540;
            Height = 430;
            StartPosition = FormStartPosition.CenterScreen;

            Label lblPort = new Label();
            lblPort.Text = "Port:";
            lblPort.Location = new Point(20, 22);
            lblPort.Width = 45;

            txtPort = new TextBox();
            txtPort.Text = "9999";
            txtPort.Location = new Point(70, 18);
            txtPort.Width = 100;

            btnStart = new Button();
            btnStart.Text = "Start";
            btnStart.Location = new Point(190, 16);
            btnStart.Width = 120;
            btnStart.Click += BtnStart_Click;

            lstLog = new ListBox();
            lstLog.Location = new Point(20, 60);
            lstLog.Size = new Size(480, 300);

            Controls.Add(lblPort);
            Controls.Add(txtPort);
            Controls.Add(btnStart);
            Controls.Add(lstLog);
        }

        void BtnStart_Click(object sender, EventArgs e)
        {
            int port;

            if (!int.TryParse(txtPort.Text.Trim(), out port))
            {
                MessageBox.Show("Port không hợp lệ");
                return;
            }

            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                running = true;

                btnStart.Enabled = false;
                txtPort.Enabled = false;

                Log("Đang chạy port " + port);

                Thread t = new Thread(AcceptClient);
                t.IsBackground = true;
                t.Start();
            }
            catch
            {
                MessageBox.Show("Không thể mở server");
            }
        }

        void AcceptClient()
        {
            while (running)
            {
                try
                {
                    TcpClient tcp = listener.AcceptTcpClient();
                    Player player = new Player(tcp, this);

                    lock (players)
                    {
                        players.Add(player);
                    }

                    Log("Có người kết nối");

                    Thread t = new Thread(player.Run);
                    t.IsBackground = true;
                    t.Start();
                }
                catch
                {
                    break;
                }
            }
        }

        public void RemovePlayer(Player player)
        {
            lock (players)
            {
                if (players.Contains(player))
                    players.Remove(player);
            }
        }

        public string CreateRoom(Player player)
        {
            lock (roomLock)
            {
                string id;

                do
                {
                    id = random.Next(1000, 9999).ToString();
                }
                while (rooms.ContainsKey(id));

                Room room = new Room(id, this);
                room.Player1 = player;

                player.Room = room;
                player.Symbol = "X";

                rooms.Add(id, room);

                Log("Tạo phòng " + id);

                return id;
            }
        }

        public bool JoinRoom(string id, Player player)
        {
            lock (roomLock)
            {
                if (!rooms.ContainsKey(id))
                    return false;

                Room room = rooms[id];

                if (room.Player2 != null)
                    return false;

                room.Player2 = player;
                player.Room = room;
                player.Symbol = "O";

                Log("Vào phòng " + id);

                player.Send("JOIN_OK|Vào phòng thành công");
                room.StartGame();

                return true;
            }
        }

        public void RemoveRoom(Room room)
        {
            if (room == null)
                return;

            lock (roomLock)
            {
                if (rooms.ContainsKey(room.Id))
                {
                    rooms.Remove(room.Id);
                    Log("Xóa phòng " + room.Id);
                }
            }
        }

        public void AddResult(Player winner, Player loser, bool draw)
        {
            lock (rankLock)
            {
                AddRank(winner);
                AddRank(loser);

                if (draw)
                {
                    ranks[winner.Name].Draw++;
                    ranks[loser.Name].Draw++;
                }
                else
                {
                    ranks[winner.Name].Win++;
                    ranks[loser.Name].Lose++;
                }
            }

            SendRankToAll();
        }

        void AddRank(Player player)
        {
            if (player == null)
                return;

            if (!ranks.ContainsKey(player.Name))
                ranks.Add(player.Name, new RankItem(player.Name));
        }

        public void SendRankToAll()
        {
            string text = GetRankText();

            lock (players)
            {
                foreach (Player p in players.ToArray())
                    p.Send("RANK|" + text);
            }
        }

        string GetRankText()
        {
            List<RankItem> list;

            lock (rankLock)
            {
                list = new List<RankItem>(ranks.Values);
            }

            list.Sort(delegate (RankItem a, RankItem b)
            {
                int c = b.Win.CompareTo(a.Win);
                if (c != 0)
                    return c;

                c = a.Lose.CompareTo(b.Lose);
                if (c != 0)
                    return c;

                return a.Name.CompareTo(b.Name);
            });

            List<string> rows = new List<string>();

            foreach (RankItem r in list)
                rows.Add(r.Name + "," + r.Win + "," + r.Lose + "," + r.Draw);

            return string.Join(";", rows.ToArray());
        }

        public void Log(string text)
        {
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(Log), text);
                }
                catch
                {
                }

                return;
            }

            lstLog.Items.Add(DateTime.Now.ToString("HH:mm:ss") + " - " + text);
            lstLog.TopIndex = lstLog.Items.Count - 1;
        }

        void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            running = false;

            try
            {
                if (listener != null)
                    listener.Stop();
            }
            catch
            {
            }
        }
    }

    public class RankItem
    {
        public string Name;
        public int Win;
        public int Lose;
        public int Draw;

        public RankItem(string name)
        {
            Name = name;
        }
    }

    public class Player
    {
        TcpClient tcp;
        StreamReader reader;
        StreamWriter writer;
        object sendLock = new object();

        Form1 server;
        bool closed = false;

        public Room Room;
        public string Symbol = "";
        public string Name = "Người chơi";
        public bool WantReplay = false;
        public int TimeoutCount = 0;

        public Player(TcpClient tcp, Form1 server)
        {
            this.tcp = tcp;
            this.server = server;
        }

        public void Run()
        {
            try
            {
                NetworkStream ns = tcp.GetStream();
                reader = new StreamReader(ns);
                writer = new StreamWriter(ns);
                writer.AutoFlush = true;

                Send("CONNECTED|Đã kết nối");
                server.SendRankToAll();

                while (!closed)
                {
                    string msg = reader.ReadLine();

                    if (msg == null)
                        break;

                    HandleMessage(msg);
                }
            }
            catch
            {
            }
            finally
            {
                Disconnect();
            }
        }

        void HandleMessage(string msg)
        {
            string[] data = msg.Split('|');

            if (data.Length == 0)
                return;

            if (data[0] == "NAME")
            {
                if (data.Length >= 2 && data[1].Trim() != "")
                    Name = data[1].Trim();

                server.SendRankToAll();
            }
            else if (data[0] == "CREATE_ROOM")
            {
                if (Room != null)
                {
                    Send("ERROR|Bạn đang ở trong phòng");
                    return;
                }

                string id = server.CreateRoom(this);
                Send("ROOM_ID|" + id);
                Send("WAIT|Đang chờ người chơi khác");
            }
            else if (data[0] == "JOIN_ROOM")
            {
                if (data.Length < 2 || data[1].Trim() == "")
                {
                    Send("ERROR|Bạn chưa nhập ID phòng");
                    return;
                }

                if (Room != null)
                {
                    Send("ERROR|Bạn đang ở trong phòng");
                    return;
                }

                bool ok = server.JoinRoom(data[1].Trim(), this);

                if (!ok)
                    Send("ERROR|Phòng không tồn tại hoặc đã đủ người");
            }
            else if (data[0] == "MOVE")
            {
                if (data.Length < 3)
                    return;

                int row;
                int col;

                if (!int.TryParse(data[1], out row))
                    return;

                if (!int.TryParse(data[2], out col))
                    return;

                if (Room != null)
                    Room.Move(this, row, col, false);
            }
            else if (data[0] == "CHAT")
            {
                if (Room == null)
                {
                    Send("ERROR|Bạn chưa vào phòng");
                    return;
                }

                string text = "";

                if (msg.StartsWith("CHAT|") && msg.Length > 5)
                    text = msg.Substring(5);

                Room.Chat(this, text);
            }
            else if (data[0] == "REPLAY")
            {
                if (Room != null)
                    Room.Replay(this);
            }
            else if (data[0] == "EXIT")
            {
                Disconnect();
            }
        }

        public void Send(string msg)
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

        void Disconnect()
        {
            if (closed)
                return;

            closed = true;
            Room oldRoom = Room;

            try
            {
                tcp.Close();
            }
            catch
            {
            }

            if (oldRoom != null)
            {
                oldRoom.PlayerLeft(this);
                server.RemoveRoom(oldRoom);
            }

            server.RemovePlayer(this);
        }
    }

    public class Room
    {
        public string Id;
        public Player Player1;
        public Player Player2;

        string[,] board = new string[15, 15];
        string turn = "X";
        int moveCount = 0;
        int timerId = 0;
        bool gameOver = false;

        Form1 server;
        object roomLock = new object();
        Random random = new Random();

        public Room(string id, Form1 server)
        {
            Id = id;
            this.server = server;
        }

        public void StartGame()
        {
            lock (roomLock)
            {
                board = new string[15, 15];
                turn = "X";
                moveCount = 0;
                gameOver = false;
                timerId++;

                Player1.WantReplay = false;
                Player2.WantReplay = false;
                Player1.TimeoutCount = 0;
                Player2.TimeoutCount = 0;

                Player1.Send("START|X|1|" + Id + "|" + Player1.Name + "|" + Player2.Name);
                Player2.Send("START|O|0|" + Id + "|" + Player2.Name + "|" + Player1.Name);

                server.Log("Phòng " + Id + " bắt đầu");
                StartTimer();
            }
        }

        void StartTimer()
        {
            timerId++;
            int id = timerId;

            Thread t = new Thread(delegate ()
            {
                for (int i = 30; i >= 0; i--)
                {
                    lock (roomLock)
                    {
                        if (gameOver || id != timerId || Player1 == null || Player2 == null)
                            return;

                        Player1.Send("TIME|" + i + "|" + Player1.TimeoutCount + "|" + Player2.TimeoutCount);
                        Player2.Send("TIME|" + i + "|" + Player2.TimeoutCount + "|" + Player1.TimeoutCount);
                    }

                    if (i == 0)
                        break;

                    Thread.Sleep(1000);
                }

                TimeoutMove(id);
            });

            t.IsBackground = true;
            t.Start();
        }

        void TimeoutMove(int id)
        {
            lock (roomLock)
            {
                if (gameOver || id != timerId || Player1 == null || Player2 == null)
                    return;

                Player player = GetCurrentPlayer();
                Player opponent = GetOpponent(player);

                if (player == null || opponent == null)
                    return;

                player.TimeoutCount++;

                if (player.TimeoutCount >= 3)
                {
                    EndGame(opponent, player, opponent.Name + " thắng vì " + player.Name + " quá giờ 3 lượt");
                    return;
                }

                int[] pos = GetAutoMove();

                if (pos == null)
                {
                    EndDraw();
                    return;
                }

                Move(player, pos[0], pos[1], true);
            }
        }

        int[] GetAutoMove()
        {
            int row = 7;
            int col = 7;

            if (board[row, col] == null)
                return new int[] { row, col };

            List<int[]> list = new List<int[]>();

            for (int i = 0; i < 15; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    if (board[i, j] == null)
                        list.Add(new int[] { i, j });
                }
            }

            if (list.Count == 0)
                return null;

            return list[random.Next(list.Count)];
        }

        public void Move(Player player, int row, int col, bool autoMove)
        {
            lock (roomLock)
            {
                if (gameOver)
                {
                    player.Send("ERROR|Ván đấu đã kết thúc");
                    return;
                }

                if (Player1 == null || Player2 == null)
                {
                    player.Send("ERROR|Chưa đủ người chơi");
                    return;
                }

                if (player.Symbol != turn)
                {
                    player.Send("ERROR|Chưa đến lượt bạn");
                    return;
                }

                if (row < 0 || row >= 15 || col < 0 || col >= 15)
                {
                    player.Send("ERROR|Nước đi không hợp lệ");
                    return;
                }

                if (board[row, col] != null)
                {
                    player.Send("ERROR|Ô này đã được đánh");
                    return;
                }

                board[row, col] = player.Symbol;
                moveCount++;
                timerId++;

                Player1.Send("MOVE|" + row + "|" + col + "|" + player.Symbol);
                Player2.Send("MOVE|" + row + "|" + col + "|" + player.Symbol);

                if (autoMove)
                {
                    player.Send("CHAT|Máy đã đánh giúp bạn. Quá giờ: " + player.TimeoutCount + "/3");
                    GetOpponent(player).Send("CHAT|Đối thủ quá giờ, máy đã đánh giúp");
                }

                if (CheckWin(row, col, player.Symbol))
                {
                    EndGame(player, GetOpponent(player), player.Name + " thắng");
                    return;
                }

                if (moveCount >= 225)
                {
                    EndDraw();
                    return;
                }

                turn = turn == "X" ? "O" : "X";

                Player1.Send("TURN|" + (turn == Player1.Symbol ? "1" : "0"));
                Player2.Send("TURN|" + (turn == Player2.Symbol ? "1" : "0"));

                StartTimer();
            }
        }

        void EndGame(Player winner, Player loser, string text)
        {
            gameOver = true;
            timerId++;

            string winText = "Bạn thắng. " + loser.Name + " thua.";
            string loseText = "Bạn thua. " + winner.Name + " thắng.";

            if (text.IndexOf("quá giờ 3 lượt") >= 0)
            {
                winText = "Bạn thắng vì " + loser.Name + " quá giờ 3 lượt.";
                loseText = "Bạn thua vì quá giờ 3 lượt. " + winner.Name + " thắng.";
            }

            winner.Send("END|WIN|" + winText);
            loser.Send("END|LOSE|" + loseText);
            server.AddResult(winner, loser, false);
        }

        void EndDraw()
        {
            gameOver = true;
            timerId++;

            Player1.Send("END|DRAW|Ván đấu hòa");
            Player2.Send("END|DRAW|Ván đấu hòa");
            server.AddResult(Player1, Player2, true);
        }

        Player GetCurrentPlayer()
        {
            if (Player1 != null && Player1.Symbol == turn)
                return Player1;

            if (Player2 != null && Player2.Symbol == turn)
                return Player2;

            return null;
        }

        public void Chat(Player player, string text)
        {
            lock (roomLock)
            {
                if (text == null)
                    text = "";

                text = text.Trim();

                if (text == "")
                    return;

                Player opponent = GetOpponent(player);

                if (opponent == null)
                {
                    player.Send("ERROR|Chưa có đối thủ để chat");
                    return;
                }

                player.Send("CHAT|Tôi: " + text);
                opponent.Send("CHAT|" + player.Name + ": " + text);
            }
        }

        public void Replay(Player player)
        {
            lock (roomLock)
            {
                if (Player1 == null || Player2 == null)
                {
                    player.Send("ERROR|Không thể chơi lại vì thiếu người chơi");
                    return;
                }

                player.WantReplay = true;

                if (Player1.WantReplay && Player2.WantReplay)
                {
                    StartGame();
                    return;
                }

                Player opponent = GetOpponent(player);

                if (opponent != null)
                    opponent.Send("REPLAY_ASK|Đối thủ muốn chơi lại");
            }
        }

        public void PlayerLeft(Player player)
        {
            lock (roomLock)
            {
                timerId++;
                Player opponent = GetOpponent(player);

                if (opponent != null)
                {
                    if (!gameOver && Player1 != null && Player2 != null)
                    {
                        gameOver = true;
                        opponent.Send("END|WIN|Đối thủ đã thoát, bạn đã thắng");
                        server.AddResult(opponent, player, false);
                    }
                    else
                    {
                        opponent.Send("LEFT|Đối thủ đã thoát khỏi phòng");
                    }

                    opponent.Room = null;
                    opponent.Symbol = "";
                    opponent.WantReplay = false;
                }

                player.Room = null;
                player.Symbol = "";
                player.WantReplay = false;

                server.Log("Rời phòng " + Id);
            }
        }

        Player GetOpponent(Player player)
        {
            if (player == Player1)
                return Player2;

            if (player == Player2)
                return Player1;

            return null;
        }

        bool CheckWin(int row, int col, string symbol)
        {
            if (Count(row, col, 1, 0, symbol) + Count(row, col, -1, 0, symbol) - 1 >= 5)
                return true;

            if (Count(row, col, 0, 1, symbol) + Count(row, col, 0, -1, symbol) - 1 >= 5)
                return true;

            if (Count(row, col, 1, 1, symbol) + Count(row, col, -1, -1, symbol) - 1 >= 5)
                return true;

            if (Count(row, col, 1, -1, symbol) + Count(row, col, -1, 1, symbol) - 1 >= 5)
                return true;

            return false;
        }

        int Count(int row, int col, int dr, int dc, string symbol)
        {
            int count = 0;

            while (row >= 0 && row < 15 && col >= 0 && col < 15 && board[row, col] == symbol)
            {
                count++;
                row += dr;
                col += dc;
            }

            return count;
        }
    }
}
