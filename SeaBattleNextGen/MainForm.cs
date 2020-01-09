using System;
using System.Diagnostics;
using System.Drawing;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SeaBattleNextGen
{
    public partial class MainForm : Form
    {
        NamedPipeServerStream pipeServer;
        NamedPipeClientStream pipeClient;

        Map playerMap;
        Map enemyMap;

        // текущий тип корабля для добавления на поле
        byte shipIndex = 0;

        public bool IsEnemyReady { get; set; }
        public bool IsReady { get; set; }

        public bool GameEnded { get; set; }
        public bool Looser { get; set; }

        public bool IsPlayerTurn { get; set; }

        public MainForm()
        {
            InitializeComponent();

            // получаем число запущенных сейчас экземпляров этого приложения
            var instancesCount = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length;

            if (instancesCount > 2)
            {
                MessageBox.Show("Третий лишний");
                return;
            }

            var hasOtherInstances = instancesCount > 1;
            // если этот экземпляр - первый, то и ходим первым
            IsPlayerTurn = !hasOtherInstances;

            // настриваем клиент и сервер в зависимости от очередности
            pipeServer = new NamedPipeServerStream(IsPlayerTurn ? "pipe1" : "pipe2", PipeDirection.Out, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            pipeClient = new NamedPipeClientStream(".", IsPlayerTurn ? "pipe2" : "pipe1", PipeDirection.In, PipeOptions.Asynchronous);

            // создаем карты
            playerMap = new Map();
            enemyMap = new Map();
            
            hidePanel.Visible = !IsPlayerTurn;

            // рисуем ячейки
            DrawPlayerPanels(true);
            DrawPlayerPanels(false);

            panelStatus.BackgroundImage = Properties.Resources.Disconnected;

            // запуск потока приёма
            new Thread(Reader)
            {
                IsBackground = true
            }.Start();

            //автоматический запуск второго, если надо
            if (IsPlayerTurn)
               Process.Start(Process.GetCurrentProcess().MainModule.FileName);
        }

        /// <summary>
        /// Отрисовка поля
        /// </summary>
        private void DrawPlayerPanels(bool isEnemy)
        {
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    var panel = new Panel
                    {
                        BackColor = SystemColors.ActiveCaption,
                        BorderStyle = BorderStyle.FixedSingle,
                        BackgroundImageLayout = ImageLayout.Stretch,
                        Location = new Point(x * 25 + 25, y * 25 + 25),
                        Margin = new Padding(0),
                        Name = isEnemy
                            ? "panelE_" + x + "_" + y
                            : "panelS_" + x + "_" + y,
                        Size = new Size(25, 25)
                    };
                    if (isEnemy)
                    {
                        panel.Click += EnemyPanel_Click;
                        panelEnemy.Controls.Add(panel);
                    }
                    else
                    {
                        panel.Click += SeveralPanel_Click;
                        panelSeveral.Controls.Add(panel);
                    }
                }
            }
        }

        /// <summary>
        /// Обработчик нажатия по полю врага (выстрел)
        /// </summary>
        private void EnemyPanel_Click(object sender, EventArgs e)
        {
            if (!IsReady
                || !IsEnemyReady
                || !IsPlayerTurn)
                return;

            var panel = ((Panel)sender);
            var nameParts = panel.Name.Split('_');
            int x = int.Parse(nameParts[1]);
            int y = int.Parse(nameParts[2]);

            if (!enemyMap.CanBeShoted(x, y))
                return;

            Send("shot|" + x + "|" + y);

            IsPlayerTurn = enemyMap.Shot(x, y);

            UpdatePanels(true);

            GameEnded = !enemyMap.HasAnyShip();
            if (GameEnded)
            {
                MessageBox.Show("Вы победили!");
                IsPlayerTurn = false;
                Looser = false;
            }

            UpdateStatusLabel();
            UpdateVisibility();
        }

        /// <summary>
        /// Обработчик клика по своему полю (добавление корабля)
        /// </summary>
        private void SeveralPanel_Click(object sender, EventArgs e)
        {
            if (IsReady
                || !IsPlayerTurn)
                return;

            var panel = ((Panel)sender);
            var nameParts = ((Panel)sender).Name.Split('_');
            int x = int.Parse(nameParts[1]);
            int y = int.Parse(nameParts[2]);

            var setStarted = playerMap.GetShipPartsCount(shipIndex) > 0;

            if (playerMap[x, y] == Map.EmptyField
                && playerMap.HasPlaceForShip(x, y, shipIndex))
            {
                if (setStarted
                    && !playerMap.IsNear(x, y, shipIndex))
                    return;

                playerMap[x, y] = shipIndex;

                playerMap.BlockNeighbors(x, y);

                if (playerMap.IsShipSetted(shipIndex))
                {
                    playerMap.BlockShipNeighbors(shipIndex);

                    shipIndex++;
                    // если новый индекс больше не индекс нового корабля
                    // то есть - расстановка закончилась
                    if (!Map.IsShipIndex(shipIndex))
                    {
                        panelHelper.Visible = false;
                        IsReady = true;
                        IsPlayerTurn = false;
                        // отправка готовности
                        Send("ready|" + playerMap.ToString());
                        UpdateVisibility();
                    }
                }

                UpdateCurrentShipImage();
                UpdatePanels(false);
            }
        }

        /// <summary>
        /// Обновление всех ячеек
        /// </summary>
        private void UpdatePanels(bool isEnemy)
        {
            var map = isEnemy ? enemyMap : playerMap;

            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    UpdatePanel(x, y, isEnemy);
        }

        /// <summary>
        /// Обновление конкретной ячейки
        /// </summary>
        private void UpdatePanel(int x, int y, bool isEnemy)
        {
            var index = isEnemy
                ? enemyMap[x, y]
                : playerMap[x, y];

            var panel = Controls.Find((isEnemy ? "panelE_" : "panelS_") + x + "_" + y, true).First() as Panel;

            // Если ячейка "мимо"
            if (index == Map.MissField)
            {
                panel.BackgroundImage = Properties.Resources.Miss;
                return;
            }
            // Если ячейка "блок"
            if (index == Map.BlockedField
                && !isEnemy)
            {
                panel.BackColor = Color.FromArgb(133, 160, 189);
                return;
            }

            var color = isEnemy
                ? Color.FromArgb((byte)((enemyMap[x, y] % 10) * 15) + 105, 0, 0)
                : Color.FromArgb(0, 0, (byte)((playerMap[x, y] % 10) * 15) + 105);

            // Если ячейка живого корабля
            if (Map.IsShipIndex(index)
                && !isEnemy)
            {
                panel.BackColor = color;
            }
            // Если ячейка битого корабля
            else if (Map.IsKilledShipIndex(index))
            {
                panel.BackColor = color;
                panel.BackgroundImage = Properties.Resources.Killed;
            }
        }

        /// <summary>
        /// Обновление ячеек текущего корабля на добавление
        /// </summary>
        private void UpdateCurrentShipImage()
        {
            panelX0.Visible = shipIndex <= 9;
            panelX1.Visible = shipIndex <= 5;
            panelX2.Visible = shipIndex <= 2;
            panelX3.Visible = shipIndex == 0;
        }

        /// <summary>
        /// Обновление строки статуса
        /// </summary>
        private void UpdateStatusLabel()
        {
            InvokeOnUI(() =>
            {
                if (GameEnded)
                {
                    statusLabel.Text = Looser
                        ? "Вы проиграли"
                        : "Вы победили!";
                }
                else if (IsEnemyReady)
                {
                    if (!IsReady)
                    {
                        statusLabel.Text = "Соперник готов";
                    }
                    else if (IsPlayerTurn)
                    {
                        statusLabel.Text = "Ваш ход";
                    }
                    else
                    {
                        statusLabel.Text = "Ход соперника";
                    }
                }
                else
                {
                    statusLabel.Text = "";
                }
            });
        }

        /// <summary>
        /// Обновление видимости прячущей поле панели
        /// </summary>
        private void UpdateVisibility()
        {
            if (GameEnded)
            {
                InvokeOnUI(() =>
                {
                    hidePanel.Visible = false;
                });
            }
            else if (hidePanel.Visible
                && IsPlayerTurn)
            {

                new Thread(new ThreadStart(() =>
                {
                    Thread.Sleep(1000);
                    InvokeOnUI(() =>
                    {
                        hidePanel.Visible = false;
                    });
                }))
                .Start();
            }
            else
            {
                InvokeOnUI(() =>
                {
                    hidePanel.Visible = !IsPlayerTurn;
                });
            }
        }

        /// <summary>
        /// Метод, обрабатывающий получение сообщения по каналу
        /// </summary>
        private void OnRecieve(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            // если получили готовность от оппонента вместе с его картой
            if (message.StartsWith("ready"))
            {
                if (IsEnemyReady)
                    return;

                IsEnemyReady = true;
                IsPlayerTurn = true;
                enemyMap = new Map(message.Substring(message.IndexOf("|") + 1));
            }
            // если получили выстрел от оппонента
            else if (message.StartsWith("shot"))
            {
                if (IsPlayerTurn)
                    return;

                var parts = message.Split('|');
                var x = int.Parse(parts[1]);
                var y = int.Parse(parts[2]);

                // доп проверка, мог ли он так выстрелить
                if (!playerMap.CanBeShoted(x, y))
                    return;

                var shoted = playerMap.Shot(x, y);

                InvokeOnUI(() =>
                {
                    var panel = Controls.Find("panelS_" + x + "_" + y, true).First();

                    panel.BackgroundImage = shoted
                        ? Properties.Resources.Killed
                        : Properties.Resources.Miss;

                    UpdatePanels(false);
                });

                IsPlayerTurn = !shoted;

                // проверка на поражение
                GameEnded = !playerMap.HasAnyShip();
                if (GameEnded)
                {
                    MessageBox.Show("Вы проиграли");
                    Looser = true;
                    IsPlayerTurn = false;
                }

            }

            UpdateStatusLabel();
            UpdateVisibility();
        }

        /// <summary>
        /// Отправка сообщения в отдельном потоке по каналу другому процессу
        /// </summary>
        private void Send(string message)
        {
            new Thread(new ThreadStart(() =>
            {
                try
                {
                    if (!pipeServer.IsConnected)
                    {
                        pipeServer.WaitForConnection();
                    }

                    var bytes = Encoding.UTF8.GetBytes(message);
                    pipeServer.Write(bytes, 0, bytes.Length);
                    pipeServer.Flush();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }))
            .Start();
        }

        /// <summary>
        /// Метод с кодом для потока получателя информации с канала
        /// </summary>
        void Reader()
        {
            try
            {
                pipeClient.Connect();

                InvokeOnUI(() =>
                {
                    panelStatus.BackgroundImage = Properties.Resources.Connected;
                });
                UpdateVisibility();

                // читаем из канала в цикле, пока подключены
                while (pipeClient.IsConnected)
                {
                    var buffer = new byte[2048];
                    var length = pipeClient.Read(buffer, 0, buffer.Length);

                    var message = Encoding.UTF8.GetString(buffer, 0, length);

                    OnRecieve(message);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            InvokeOnUI(() =>
            {
                panelStatus.BackgroundImage = Properties.Resources.Disconnected;
            });
        }

        /// <summary>
        /// Вызов делегата в основном потоке
        /// </summary>
        private void InvokeOnUI(Action action)
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
    }
}