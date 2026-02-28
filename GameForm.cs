using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FleetCommand.Properties;
using NAudio.Wave;


namespace FleetCommand
{
    public class GameForm : Form
    {
        private GameWorld world;
        private Timer gameTimer;

        // Camera
        private PointF cameraOffset = PointF.Empty;
        private float zoom = 1.0f;
        private const float ZoomMin = 0.5f;
        private const float ZoomMax = 3.0f;
        private const float ZoomStep = 0.1f;
        private const float ScrollSpeed = 15f;

        // Input
        private bool scrollLeft, scrollRight, scrollUp, scrollDown;
        private DateTime lastUpdate = DateTime.Now;

        // Selection
        private List<Ship> selectedShips = new List<Ship>();
        private PointF? dragStart;
        private RectangleF dragRect = RectangleF.Empty;
        private Ship _groupSelectAnchor = null;   // tracks the ship clicked for group-select
        private const float GroupSelectRadius = 300f;

        // Panning
        private bool isPanning;
        private Point panLastPos;

        // ── Side panel refs ────────────────────────────────────────────────────
        private Panel sidePanel;
        private Label resourceLabel;
        private Label diffLabel;
        private ListBox eventLog;
        private Label _eventLogSep;
        private PictureBox _miniMapBox;
        private const int MiniMapW = 330, MiniMapH = 220; // ≈ 6000:4000 world ratio
        private Label buildQueueLabel;
        private ProgressBar buildProgress;
        private Label buildProgressLabel;
        private Label researchStatusLabel;

        // Tab system
        private Button tabBuildBtn;
        private Button tabResearchBtn;
        private Panel buildTabPanel;
        private Panel researchTabPanel;

        // Build tab — two button sets (only one visible at a time)
        private readonly List<Button> _msBuildButtons      = new List<Button>();
        private readonly List<Button> _carrierBuildButtons = new List<Button>();
        private Ship  _activeBuildSource = null;  // null = mothership
        private Label _buildSourceLabel;

        // Research tab
        private readonly List<ResearchRowInline> researchRows = new List<ResearchRowInline>();
        private Label researchActiveLabel;
        private ProgressBar researchActiveBar;

        // Research queue UI
        private Label researchQueueHeader;
        private readonly Label[]  _queueSlotLabels     = new Label[2];
        private readonly Button[] _queueSlotRemoveBtns = new Button[2];

        // Change tracking — avoid touching WinForms controls every tick
        private int _lastResources = -1;
        private bool _researchDirty = false;
        private int _lastResearchPct = -1;
        private int _lastQueueCount = -1;
        private ShipType _lastActiveResearchType = (ShipType)(-1);

        private float _boundaryX1, _boundaryX2, _boundaryY1, _boundaryY2;
        private int sidePanelWidth = 440;

		private Bitmap wp;

        private IWavePlayer outputDevice;

        private Boolean _IsDebug = false;

		// ── Constructor ────────────────────────────────────────────────────────

		public GameForm(GameWorld world)
        {
            this.world = world;
            InitializeUI();
            InitializeTimers();
            StartMusic();
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer, true);
            UpdateStyles();
        }

        // ── Music ──────────────────────────────────────────────────────────────

        private void StartMusic()
        {
            try
            {
                WaveStream mp3Reader;

                string[] music = new string[] { 
                    "FleetCommand.Resources.bgmusic.mp3", 
                    "FleetCommand.Resources.bgmusic2.mp3",
                    "FleetCommand.Resources.bgmusic3.mp3",
					"FleetCommand.Resources.bgmusic4.mp3",
					"FleetCommand.Resources.bgmusic5.mp3" };
                Random rand = new Random();


                var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(music[rand.Next(0,5)]);
                //if (stream != null) { musicPlayer = new SoundPlayer(stream); musicPlayer.PlayLooping(); }
                if (stream != null)
                {
                    mp3Reader = new Mp3FileReader(stream);
                    var loop = new LoopStream(mp3Reader);

                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(loop);
                    outputDevice.Play();
                }

            }
            catch { }
        }

        private void StopMusic()
        {
            try { outputDevice?.Stop(); } catch { }
            outputDevice = null;
        }

        // ── UI Construction ────────────────────────────────────────────────────

        private void InitializeUI()
        {
            Text = "Fleet Command";
            WindowState = FormWindowState.Maximized;
            BackColor = Color.Black;
            MinimumSize = new Size(1200, 600);

            // Wider side panel — houses build + research inline
            // NoFocusPanel ensures clicking any button here never steals keyboard focus
            sidePanel = new NoFocusPanel
            {
                Dock = DockStyle.Right,
                Width = 440,
                BackColor = Color.FromArgb(20, 20, 35),
            };
            Controls.Add(sidePanel);

            int y = 8;
            const int W = 424; // usable width

            // Title / diff / resources
            SP(new Label
            {
                Text = "FLEET COMMAND",
                ForeColor = Color.Cyan,
                Font = new Font("Arial", 12, FontStyle.Bold),
                AutoSize = true,
                Location = P(8, y)
            }); y += 26;
            diffLabel = SPL(GetOpponentSummary(), Color.Cyan, 9, true, 8, y); y += 22;
            resourceLabel = SPL("Resources: 0", Color.Gold, 10, true, 8, y); y += 26;

            // Fleet status — 2-column compact grid
            SP(Sep("── FLEET STATUS ──", y)); y += 18;
            int col = 0;
            foreach (ShipType st in Enum.GetValues(typeof(ShipType)))
            {
                if (st == ShipType.Mothership) continue;
                SP(new Label
                {
                    Text = $"{st}: 0",
                    ForeColor = Color.LightGreen,
                    Font = new Font("Consolas", 8),
                    AutoSize = true,
                    Location = P(col == 0 ? 8 : 220, y),
                    Tag = st
                });
                if (col == 1) y += 16;
                col = 1 - col;
            }
            if (col == 1) y += 16;
            y += 4;

            // Build queue status
            SP(Sep("── BUILD QUEUE ──", y)); y += 18;
            buildQueueLabel = new Label
            {
                Text = "Queue: empty",
                ForeColor = Color.LightBlue,
                Font = new Font("Consolas", 8),
                AutoSize = false,
                Width = W - 8,
                Location = P(8, y)
            };
            SP(buildQueueLabel); y += 16;
            buildProgress = new ProgressBar
            {
                Location = P(8, y),
                Size = new Size(W - 8, 13),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                TabStop = false
            };
            SP(buildProgress); y += 15;
            buildProgressLabel = new Label
            {
                Text = "",
                ForeColor = Color.LightBlue,
                Font = new Font("Consolas", 7),
                AutoSize = false,
                Width = W - 8,
                Location = P(8, y)
            };
            SP(buildProgressLabel); y += 18;

            // Research status strip
            SP(Sep("── RESEARCH ──", y)); y += 18;
            researchStatusLabel = new Label
            {
                Text = "No active research",
                ForeColor = Color.Plum,
                Font = new Font("Consolas", 7),
                AutoSize = false,
                Width = W - 8,
                Height = 13,
                Location = P(8, y)
            };
            SP(researchStatusLabel); y += 18;

            // ── TAB BAR ──────────────────────────────────────────────────────
            y += 2;
            int tabW = (W - 4) / 2;
            tabBuildBtn = MakeTabBtn("⚙ BUILD", 8, y, tabW);
            tabResearchBtn = MakeTabBtn("⚗ RESEARCH", 8 + tabW + 4, y, tabW);
            tabBuildBtn.Click += (s, e) => { SwitchTab(true); this.Focus(); };
            tabResearchBtn.Click += (s, e) => { SwitchTab(false); this.Focus(); };
            SP(tabBuildBtn); SP(tabResearchBtn);
            y += 30;

            // ── BUILD TAB ────────────────────────────────────────────────────
            buildTabPanel = new Panel
            {
                Location = P(4, y),
                Size = new Size(W, 300),
                BackColor = Color.Transparent,
                Visible = true,
            };
            BuildBuildTab(buildTabPanel);
            SP(buildTabPanel);

            // ── RESEARCH TAB ─────────────────────────────────────────────────
            researchTabPanel = new Panel
            {
                Location = P(4, y),
                Size = new Size(W, 300),
                BackColor = Color.Transparent,
                Visible = false,
            };
            BuildResearchTab(researchTabPanel, W);
            SP(researchTabPanel);

            y += 304;

            // Controls help
            SP(Sep("── CONTROLS ──", y)); y += 18;
            SP(new Label
            {
                Text =
                    "Arrows / RightDrag: scroll map\n" +
                    "Scroll / +- : zoom    MiddleDrag: pan\n" +
                    "LClick: select   Ctrl+A: select all\n" +
                    "RClick enemy: ATTACK   RClick: MOVE\n" +
                    "RClick asteroid: MINE\n" +
                    "DblClick Mothership/Carrier → Build tab\n" +
                    "SPACE: pause   ESC: deselect/menu",
                ForeColor = Color.DarkGray,
                Font = new Font("Consolas", 7),
                AutoSize = false,
                Width = W - 8,
                Height = 80,
                Location = P(8, y)
            }); y += 84;


			// MiniMap
			SP(Sep("── MINI MAP ──", y)); y += 18;
            _miniMapBox = new PictureBox
            {
                Location  = P((W - MiniMapW) / 2, y),
                Size      = new Size(MiniMapW, MiniMapH),
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
            };
            _miniMapBox.MouseClick += OnMiniMapClick;
            SP(_miniMapBox);
            y += MiniMapH + 6;

			// Event log (debug only — toggle with F12)
			_eventLogSep = Sep("── EVENT LOG ──", y);
            SP(_eventLogSep); y += 18;
            eventLog = new ListBox
            {
                Location = P(4, y),
                Size = new Size(W, 170),
                BackColor = Color.FromArgb(10, 10, 20),
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 7),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollAlwaysVisible = false,
                TabStop = false,
            };
            SP(eventLog);
            _eventLogSep.Visible = _IsDebug;
            eventLog.Visible     = _IsDebug;

            SwitchTab(true); // start on Build tab

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            MouseDown += OnMouseDown;
            MouseUp += OnMouseUp;
            MouseMove += OnMouseMove;
            MouseWheel += OnMouseWheel;
            MouseDoubleClick += OnMouseDoubleClick;

            if (world.PlayerMothership != null)
                cameraOffset = new PointF(
                    -world.PlayerMothership.Position.X + (ClientSize.Width - 440) / 2f / zoom,
                    -world.PlayerMothership.Position.Y + ClientSize.Height / 2f / zoom);



            Bitmap[] wpList = new Bitmap[] { Resources.wallpaper, Resources.wallpaper2, Resources.wallpaper3, Resources.wallpaper4, Resources.wallpaper5, Resources.wallpaper6 };
            Random rand = new Random();
            wp = wpList[rand.Next(0, 6)];
        }

        // ── Build tab content ──────────────────────────────────────────────────

        // Mothership can build everything (including the Carrier itself)
        private static readonly (ShipType Type, bool Squad)[] MsBuildable =
        {
            (ShipType.Worker,              false),
            (ShipType.Interceptor,        true),
            (ShipType.Bomber,             true),
            (ShipType.Corvette,             false),
            (ShipType.Frigate,            false),
            (ShipType.Destroyer,          false),
            (ShipType.Battlecruiser,      false),
            (ShipType.ResourceCollector,  false),
            (ShipType.Carrier,            false),
        };

        // Carriers can only build fighters and workers
        private static readonly (ShipType Type, bool Squad)[] CarrierBuildable =
        {
            (ShipType.Interceptor, true),
            (ShipType.Bomber,      true),
            (ShipType.Corvette,      false),
            (ShipType.Worker,       false),
        };

        private Button MakeBuildButton(ShipType type, bool squad, int y, int w)
        {
            int    cost = GameConstants.BuildCosts[(int)type];
            string lbl  = $"  {type}{(squad ? " ×5" : "")}  —  {cost} res";
            var btn = new Button
            {
                Text      = lbl,
                Location  = new Point(0, y),
                Size      = new Size(w, 28),
                BackColor = Color.FromArgb(30, 30, 60),
                ForeColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 8, FontStyle.Bold),
                Tag       = type,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop   = false,
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(60, Color.Cyan);
            btn.MouseEnter += (s, e) => { if (((Button)s).Enabled) ((Button)s).BackColor = Color.FromArgb(50, 50, 100); };
            btn.MouseLeave += (s, e) => { if (((Button)s).Enabled) ((Button)s).BackColor = Color.FromArgb(30, 30, 60); };
            return btn;
        }

        private void BuildBuildTab(Panel p)
        {
            int y = 2;

            // ── Source indicator ─────────────────────────────────────────────
            _buildSourceLabel = new Label
            {
                Text      = "◈ Mothership",
                ForeColor = Color.Gold,
                Font      = new Font("Consolas", 8, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(0, y),
            };
            p.Controls.Add(_buildSourceLabel);
            y += 20;

            // ── Mothership build buttons (visible by default) ─────────────
            int msy = y;
            foreach (var (type, squad) in MsBuildable)
            {
                var btn = MakeBuildButton(type, squad, msy, p.Width);
                btn.Click += (s, e) =>
                {
                    if (!world.TryBuildShip((ShipType)((Button)s).Tag))
                        MessageBox.Show("Not enough resources, queue is full, or fleet cap reached!",
                            "Cannot Build", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.Focus();
                };
                p.Controls.Add(btn);
                _msBuildButtons.Add(btn);
                msy += 30;
            }

            // ── Carrier build buttons (hidden until carrier is selected) ──
            int cvy = y;
            foreach (var (type, squad) in CarrierBuildable)
            {
                var btn = MakeBuildButton(type, squad, cvy, p.Width);
                btn.Visible = false;
                btn.Click += (s, e) =>
                {
                    var carrier = _activeBuildSource as Carrier;
                    if (carrier == null || !carrier.IsAlive)
                    {
                        MessageBox.Show("No active carrier — double-click a carrier to select it.",
                            "No Carrier Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (!world.TryBuildFromCarrier(carrier, (ShipType)((Button)s).Tag))
                        MessageBox.Show("Not enough resources, queue is full, or fleet cap reached!",
                            "Cannot Build", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.Focus();
                };
                p.Controls.Add(btn);
                _carrierBuildButtons.Add(btn);
                cvy += 30;
            }
        }

        // ── Research tab content ──────────────────────────────────────────────

        private static readonly ShipType[] Researchable =
        {
            ShipType.Worker, ShipType.Interceptor, ShipType.Bomber, ShipType.Corvette,
            ShipType.Frigate, ShipType.Destroyer, ShipType.Battlecruiser, ShipType.Carrier,
        };

        private void BuildResearchTab(Panel p, int W)
        {
            int y = 2;

            // Column header
            p.Controls.Add(new Label
            {
                Text = $"{"Ship",-14}{"Lvl",-7}{"Cost",-8}{"Time",-5}",
                ForeColor = Color.Gray,
                Font = new Font("Consolas", 8, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(W - 100, 14),
                Location = new Point(0, y)
            });
            y += 15;

            foreach (var type in Researchable)
            {
                var row = new ResearchRowInline(type, p.Width, y, world);
                row.ResearchClicked += OnResearchClicked;
                researchRows.Add(row);
                foreach (var c in row.Controls) p.Controls.Add(c);
                y += 28;
            }
            y += 6;

            // Active research progress
            p.Controls.Add(new Label
            {
                Text = "── Active ──",
                ForeColor = Color.DimGray,
                Font = new Font("Arial", 7),
                AutoSize = true,
                Location = new Point(0, y)
            });
            y += 16;

            researchActiveLabel = new Label
            {
                Text = "Idle — select a ship above",
                ForeColor = Color.Plum,
                Font = new Font("Consolas", 8),
                AutoSize = false,
                Size = new Size(W - 8, 14),
                Location = new Point(0, y)
            };
            p.Controls.Add(researchActiveLabel);
            y += 16;

            researchActiveBar = new ProgressBar
            {
                Location = new Point(0, y),
                Size = new Size(W - 8, 12),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                TabStop = false
            };
            p.Controls.Add(researchActiveBar);
            y += 16;

            // Research queue display (max 2 items)
            researchQueueHeader = new Label
            {
                Text      = "── Queue (0/2) ──",
                ForeColor = Color.DimGray,
                Font      = new Font("Arial", 7),
                AutoSize  = true,
                Location  = new Point(0, y)
            };
            p.Controls.Add(researchQueueHeader);
            y += 16;

            for (int i = 0; i < 2; i++)
            {
                int slot = i;   // capture loop variable for lambda

                _queueSlotLabels[i] = new Label
                {
                    Text      = $"{i + 1}. Empty",
                    ForeColor = Color.FromArgb(55, 55, 55),
                    Font      = new Font("Consolas", 8),
                    AutoSize  = false,
                    Size      = new Size(W - 36, 14),
                    Location  = new Point(10, y)
                };
                p.Controls.Add(_queueSlotLabels[i]);

                _queueSlotRemoveBtns[i] = new Button
                {
                    Text      = "×",
                    Location  = new Point(W - 24, y - 1),
                    Size      = new Size(20, 16),
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font("Consolas", 8, FontStyle.Bold),
                    ForeColor = Color.IndianRed,
                    BackColor = Color.FromArgb(30, 10, 10),
                    Visible   = false,
                    TabStop   = false,
                };
                _queueSlotRemoveBtns[i].FlatAppearance.BorderColor = Color.DarkRed;
                _queueSlotRemoveBtns[i].FlatAppearance.BorderSize  = 1;
                _queueSlotRemoveBtns[i].Click += (s, e) => OnQueueRemoveClicked(slot);
                p.Controls.Add(_queueSlotRemoveBtns[i]);

                y += 18;
            }
        }

        private void OnResearchClicked(ShipType type)
        {
            string err;
            bool ok;

            // If the lab is busy (and this type is not the active one), add to queue.
            // Otherwise try to start research directly.
            if (world.Research.ActiveOrder != null && !world.Research.IsResearching(type))
                ok = world.TryEnqueueResearch(type, out err);
            else
                ok = world.TryStartResearch(type, out err);

            if (!ok)
                MessageBox.Show(err, "Cannot Research", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
            {
                _researchDirty = true;
                RefreshResearchRows();
                UpdateResearchQueueUI();
            }
            this.Focus();
        }

        private void OnQueueRemoveClicked(int slotIndex)
        {
            var q = world.Research.ResearchQueue;
            if (slotIndex >= q.Count) return;

            var type = q[slotIndex];
            string err;
            world.TryDequeueResearch(type, out err);

            _researchDirty = true;
            RefreshResearchRows();
            UpdateResearchQueueUI();
            this.Focus();
        }

        private void UpdateResearchQueueUI()
        {
            if (researchQueueHeader == null) return;

            var q = world.Research.ResearchQueue;
            int count = q.Count;

            researchQueueHeader.Text = $"── Queue ({count}/2) ──";

            for (int i = 0; i < 2; i++)
            {
                if (_queueSlotLabels[i] == null) continue;

                if (i < count)
                {
                    var type   = q[i];
                    int nextLv = world.Research.NextLevel(type);
                    int cost   = world.Research.CostFor(type);
                    int sec    = world.Research.DurationFor(type) / 1000;
                    string mk  = nextLv == 1 ? "Mk.I" : nextLv == 2 ? "Mk.II" : "Mk.III";

                    _queueSlotLabels[i].Text      = $"{i + 1}. {type} →{mk}  {cost}r {sec}s";
                    _queueSlotLabels[i].ForeColor  = Color.Orange;
                    _queueSlotRemoveBtns[i].Visible = true;
                }
                else
                {
                    _queueSlotLabels[i].Text       = $"{i + 1}. Empty";
                    _queueSlotLabels[i].ForeColor   = Color.FromArgb(55, 55, 55);
                    _queueSlotRemoveBtns[i].Visible = false;
                }
            }
        }

        // ── Tab switching ──────────────────────────────────────────────────────

        private void SwitchTab(bool showBuild)
        {
            buildTabPanel.Visible = showBuild;
            researchTabPanel.Visible = !showBuild;
            tabBuildBtn.BackColor = showBuild ? Color.FromArgb(50, 50, 110) : Color.FromArgb(25, 25, 50);
            tabResearchBtn.BackColor = !showBuild ? Color.FromArgb(50, 50, 110) : Color.FromArgb(25, 25, 50);
            if (!showBuild) RefreshResearchRows();
        }

        // ── Build source (mothership vs. a specific carrier) ───────────────────

        private void SetBuildSource(Ship source)
        {
            _activeBuildSource = source;
            bool isCarrier = source is Carrier;

            if (_buildSourceLabel != null)
            {
                _buildSourceLabel.Text      = isCarrier ? "◈ Carrier build" : "◈ Mothership";
                _buildSourceLabel.ForeColor = isCarrier ? Color.Cyan : Color.Gold;
            }

            foreach (var btn in _msBuildButtons)      btn.Visible = !isCarrier;
            foreach (var btn in _carrierBuildButtons) btn.Visible =  isCarrier;

            SwitchTab(true);   // always reveal the Build tab
        }

        private Button MakeTabBtn(string text, int x, int y, int w) => new Button
        {
            Text = text,
            Location = P(x, y),
            Size = new Size(w, 26),
            BackColor = Color.FromArgb(25, 25, 50),
            ForeColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 8, FontStyle.Bold),
            TabStop = false,
        };

        // ── Game loop ──────────────────────────────────────────────────────────

        private void InitializeTimers()
        {
            gameTimer = new Timer { Interval = 16 };
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        private void GameLoop(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            int delta = (int)(now - lastUpdate).TotalMilliseconds;
            lastUpdate = now;

            if (world.State == GameState.Playing)
            {
                if (scrollLeft) cameraOffset = ClampCameraOffset(new PointF(cameraOffset.X + ScrollSpeed / zoom, cameraOffset.Y));
                if (scrollRight) cameraOffset = ClampCameraOffset(new PointF(cameraOffset.X - ScrollSpeed / zoom, cameraOffset.Y));
                if (scrollUp) cameraOffset = ClampCameraOffset(new PointF(cameraOffset.X, cameraOffset.Y + ScrollSpeed / zoom));
                if (scrollDown) cameraOffset = ClampCameraOffset(new PointF(cameraOffset.X, cameraOffset.Y - ScrollSpeed / zoom));

                world.Update(delta);
                UpdateUI();

                if (world.State == GameState.GameOver) ShowEndScreen(false);
                else if (world.State == GameState.Victory) ShowEndScreen(true);
            }

            Invalidate();
        }

        // ── UI Update (called every game tick) ────────────────────────────────

        private void UpdateUI()
        {
            resourceLabel.Text = $"Resources: {world.PlayerResources:N0}";

            // Fleet counts (labels tagged with ShipType)
            foreach (Control c in sidePanel.Controls)
                if (c is Label lbl && lbl.Tag is ShipType st)
                    lbl.Text = $"{st}: {world.Ships.Count(s => s.IsPlayerOwned && s.IsAlive && s.Type == st)}";

            // Reset build source if the selected carrier has died
            if (_activeBuildSource is Carrier cvDead && !cvDead.IsAlive)
            {
                _activeBuildSource = null;
                if (_buildSourceLabel != null)
                {
                    _buildSourceLabel.Text      = "◈ Mothership";
                    _buildSourceLabel.ForeColor = Color.Gold;
                }
                foreach (var btn in _msBuildButtons)      btn.Visible = true;
                foreach (var btn in _carrierBuildButtons) btn.Visible = false;
            }

            // Build queue — show the active source (mothership or carrier)
            var activeCarrier = _activeBuildSource as Carrier;
            var queue = (activeCarrier != null && activeCarrier.IsAlive)
                ? activeCarrier.BuildQueue
                : world.PlayerMothership?.BuildQueue;
            string qSrc = (activeCarrier != null && activeCarrier.IsAlive) ? "Carrier" : "MS";
            if (queue != null && queue.Count > 0)
            {
                var building = queue[0];
                buildProgressLabel.Text = $"Building ({qSrc}): {building.Type}";
                buildProgress.Value = (int)(building.Progress * 100);
                var rem = queue.Skip(1).Select(q => q.Type.ToString()).ToArray();
                buildQueueLabel.Text = rem.Length > 0
                    ? "Next: " + string.Join(", ", rem) : "Queue: 1 item";
            }
            else
            {
                buildProgress.Value = 0; buildProgressLabel.Text = "";
                buildQueueLabel.Text = "Queue: empty";
            }

            // Research status strip
            var active = world.Research.ActiveOrder;
            if (active != null)
            {
                int sec = Math.Max(0, (active.TotalMs - active.Elapsed) / 1000);
                int pct = (int)(active.Progress * 100);
                researchStatusLabel.Text = $"⚗ {active.Type} → Mk.{active.Level}  {pct}% ({sec}s)";
                researchStatusLabel.ForeColor = Color.Plum;
            }
            else
            {
                researchStatusLabel.Text = "Idle — see Research tab";
                researchStatusLabel.ForeColor = Color.DimGray;
            }

            // --- Rate-limited control updates ---

            // Build affordability: only when resources changed
            if (world.PlayerResources != _lastResources)
            {
                _lastResources = world.PlayerResources;
                UpdateBuildAffordability();
                _researchDirty = true; // costs may have flipped
            }

            // Research progress bar: cheap, update when pct changes
            UpdateResearchProgress();

            // Research queue: refresh when count changes (e.g. item auto-popped on completion)
            int currentQueueCount = world.Research.QueueCount;
            if (currentQueueCount != _lastQueueCount)
            {
                _lastQueueCount = currentQueueCount;
                UpdateResearchQueueUI();
                _researchDirty = true;
            }

            // Research rows: only when dirty AND tab is visible
            if (_researchDirty && researchTabPanel.Visible)
            {
                RefreshResearchRows();
                _researchDirty = false;
            }

            // Event log: only when new entries arrive
            if (world.EventLog.Count != eventLog.Items.Count)
            {
                eventLog.BeginUpdate();
                eventLog.Items.Clear();
                foreach (var ev in world.EventLog.Take(30)) eventLog.Items.Add(ev);
                eventLog.EndUpdate();
            }

            RenderMiniMap();
        }

        private void RenderMiniMap()
        {
            if (_miniMapBox == null) return;

            var bmp = new Bitmap(MiniMapW, MiniMapH);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);

                // Team colours: 0 = player (blue), 1+ = enemies (orange-red tints)
                Color[] teamColors =
                {
                    Color.CornflowerBlue,
                    Color.OrangeRed,
                    Color.HotPink,
                    Color.Gold,
                };

                foreach (var ship in world.Ships)
                {
                    if (!ship.IsAlive) continue;
                    int px = (int)(ship.Position.X / GameConstants.MapWidth  * MiniMapW);
                    int py = (int)(ship.Position.Y / GameConstants.MapHeight * MiniMapH);
                    px = Math.Max(0, Math.Min(MiniMapW - 2, px));
                    py = Math.Max(0, Math.Min(MiniMapH - 2, py));

                    int   teamIdx = Math.Min(ship.TeamId, teamColors.Length - 1);
                    Color col     = teamColors[teamIdx];
                    using (var br = new SolidBrush(col))
                        g.FillRectangle(br, px, py, 2, 2);
                }
            }

            var old = _miniMapBox.Image;
            _miniMapBox.Image = bmp;
            old?.Dispose();
        }

        private void OnMiniMapClick(object sender, MouseEventArgs e)
        {
            // Convert pixel coordinates on mini-map to world coordinates
            float worldX = (e.X / (float)MiniMapW) * GameConstants.MapWidth;
            float worldY = (e.Y / (float)MiniMapH) * GameConstants.MapHeight;

            // Clamp clicked position to map bounds
            worldX = Math.Max(0f, Math.Min(GameConstants.MapWidth, worldX));
            worldY = Math.Max(0f, Math.Min(GameConstants.MapHeight, worldY));

            // Calculate desired camera offset to center viewport on clicked location
            float viewportWidth  = ClientSize.Width - sidePanelWidth;
            float viewportHeight = ClientSize.Height;
            float offsetX = (viewportWidth / 2f / zoom) - worldX;
            float offsetY = (viewportHeight / 2f / zoom) - worldY;

            // Clamp camera offset to map boundaries
            cameraOffset = ClampCameraOffset(new PointF(offsetX, offsetY));
        }

        private void UpdateBuildAffordability()
        {
            var active = (_activeBuildSource is Carrier) ? _carrierBuildButtons : _msBuildButtons;
            foreach (var btn in active)
            {
                int  cost = GameConstants.BuildCosts[(int)(ShipType)btn.Tag];
                bool ok   = world.PlayerResources >= cost;
                btn.Enabled   = ok;
                btn.ForeColor = ok ? Color.LightGreen : Color.DarkGray;
                btn.BackColor = ok ? Color.FromArgb(30, 30, 60) : Color.FromArgb(20, 20, 30);
            }
        }

        private void UpdateResearchProgress()
        {
            var active = world.Research.ActiveOrder;
            if (active != null)
            {
                int pct = (int)(active.Progress * 100);
                if (pct != _lastResearchPct)
                {
                    _lastResearchPct = pct;
                    int sec = Math.Max(0, (active.TotalMs - active.Elapsed) / 1000);
                    researchActiveLabel.Text = $"{active.Type} → Mk.{active.Level}  ({sec}s left)";
                    researchActiveBar.Value = pct;
                }

                // Detect when the active order switches to a new type (queue auto-pop)
                if (_lastActiveResearchType != active.Type)
                {
                    _lastActiveResearchType = active.Type;
                    _researchDirty = true;
                }
            }
            else
            {
                if (_lastResearchPct != 0 || _lastActiveResearchType != (ShipType)(-1))
                {
                    _lastResearchPct = 0;
                    _lastActiveResearchType = (ShipType)(-1);
                    researchActiveLabel.Text = "Idle — select a ship above";
                    researchActiveBar.Value = 0;
                    _researchDirty = true; // research just finished
                }
            }
        }

        private void RefreshResearchRows()
        {
            foreach (var row in researchRows) row.Refresh(world);
        }

        // ── Rendering ──────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.Bilinear;

            DrawBackground(g);
            DrawMapBorder(g);

            foreach (var ast in world.Asteroids) ast.Draw(g, cameraOffset, zoom);
            foreach (var ship in world.Ships.Where(s => s.IsAlive)) ship.Draw(g, cameraOffset, zoom);
            foreach (var fx in world.CombatEffects) fx.Draw(g, cameraOffset, zoom);

            // Attack-order lines
            foreach (var ship in selectedShips.Where(s => s.IsAlive && s.AttackTarget?.IsAlive == true))
            {
                float ax = (ship.Position.X + cameraOffset.X) * zoom;
                float ay = (ship.Position.Y + cameraOffset.Y) * zoom;
                float bx = (ship.AttackTarget.Position.X + cameraOffset.X) * zoom;
                float by = (ship.AttackTarget.Position.Y + cameraOffset.Y) * zoom;
                using (var pen = new Pen(Color.FromArgb(160, Color.Red), 1) { DashStyle = DashStyle.Dot })
                    g.DrawLine(pen, ax, ay, bx, by);
            }

            // Targeting rings — colour matches the targeted ship's computer team
            foreach (var ship in world.Ships.Where(s => !s.IsPlayerOwned && s.IsAlive && s.IsTargeted))
            {
                float sx   = (ship.Position.X + cameraOffset.X) * zoom;
                float sy   = (ship.Position.Y + cameraOffset.Y) * zoom;
                float r    = (GetShipHitRadius(ship.Type) + 6) * zoom;
                float tk   = 6 * zoom;
                Color tcol = Ship.GetTeamColor(ship.TeamId);
                using (var pen = new Pen(tcol, 2) { DashStyle = DashStyle.Dash })
                    g.DrawEllipse(pen, sx - r, sy - r, r * 2, r * 2);
                using (var pen = new Pen(tcol, 1.5f))
                {
                    g.DrawLine(pen, sx - r - tk, sy, sx - r, sy);
                    g.DrawLine(pen, sx + r, sy, sx + r + tk, sy);
                    g.DrawLine(pen, sx, sy - r - tk, sx, sy - r);
                    g.DrawLine(pen, sx, sy + r, sx, sy + r + tk);
                }
            }

            // Drag-select rect
            if (dragStart.HasValue && dragRect != RectangleF.Empty)
            {
                using (var pen = new Pen(Color.FromArgb(150, Color.Cyan), 1) { DashStyle = DashStyle.Dash })
                using (var brush = new SolidBrush(Color.FromArgb(20, Color.Cyan)))
                {
                    g.FillRectangle(brush, dragRect);
                    g.DrawRectangle(pen, dragRect.X, dragRect.Y, dragRect.Width, dragRect.Height);
                }
            }

            DrawStatusBar(g);

            if (world.State == GameState.Paused) DrawPauseOverlay(g);
            if (world.State == GameState.GameOver || world.State == GameState.Victory) DrawEndOverlay(g);
        }

        private void DrawBackground(Graphics g)
        {
            int pw = sidePanel?.Width ?? 440;
            int w = ClientSize.Width - pw, h = ClientSize.Height;


            if (wp != null)
            {
                int padX = (int)(w * 0.02f) + 4, padY = (int)(h * 0.02f) + 4;
                float px = Math.Max(-padX, Math.Min(padX, (cameraOffset.X * zoom) * 0.05f));
                float py = Math.Max(-padY, Math.Min(padY, (cameraOffset.Y * zoom) * 0.05f));
                g.DrawImage(wp, new Rectangle((int)px - padX, (int)py - padY, w + padX * 2, h + padY * 2));
                using (var ov = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                    g.FillRectangle(ov, 0, 0, w, h);
            }
            else
            {
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                    Color.FromArgb(5, 5, 20), Color.FromArgb(10, 5, 30), LinearGradientMode.Vertical))
                    g.FillRectangle(b, 0, 0, w, h);
            }

            // Parallax stars
            var rng = new Random(42);
            for (int i = 0; i < 120; i++)
            {
                float sx = (float)((rng.NextDouble() * GameConstants.MapWidth + cameraOffset.X * 0.15f) * zoom) % w;
                float sy = (float)((rng.NextDouble() * GameConstants.MapHeight + cameraOffset.Y * 0.15f) * zoom) % h;
                if (sx < 0) sx += w; if (sy < 0) sy += h;
                int br = rng.Next(160, 255), sz = rng.Next(1, 3);
                using (var sb = new SolidBrush(Color.FromArgb(br, br, br))) g.FillEllipse(sb, sx, sy, sz, sz);
            }
        }


        private void DrawMapBorder(Graphics g)
        {
            _boundaryX1 = cameraOffset.X * zoom;
            _boundaryY1 = cameraOffset.Y * zoom;
            _boundaryX2 = (cameraOffset.X + GameConstants.MapWidth) * zoom;
            _boundaryY2 = (cameraOffset.Y + GameConstants.MapHeight) * zoom;
            //using (var pen = new Pen(Color.FromArgb(40, Color.DarkGray), 2))
            //    g.DrawRectangle(pen, _boundaryX1, _boundaryY1, _boundaryX2 - _boundaryX1, _boundaryY2 - _boundaryY1);
        }

        private void DrawStatusBar(Graphics g)
        {
            int pw = sidePanel?.Width ?? 440;
            int w = ClientSize.Width - pw;
            using (var font = new Font("Consolas", 9))
            using (var brush = new SolidBrush(Color.FromArgb(150, Color.White)))
                g.DrawString($"Zoom: {zoom:F1}x", font, brush, w - 100, 8);

            if (selectedShips.Any())
            {
                int atk = selectedShips.Count(s => s.AttackTarget?.IsAlive == true);
                string sel = selectedShips.Count == 1
                    ? $"Selected: {selectedShips[0].Type}"
                    : $"Selected: {selectedShips.Count} ships";
                if (atk > 0) sel += $"  ⚔ {atk} attacking";
                using (var font = new Font("Consolas", 9, FontStyle.Bold))
                    g.DrawString(sel, font, Brushes.Cyan, 10, 8);
                using (var font = new Font("Consolas", 8))
                    g.DrawString("RClick enemy=ATTACK  |  RClick space=MOVE  |  RClick asteroid=MINE",
                        font, new SolidBrush(Color.FromArgb(120, Color.LightGray)), 10, ClientSize.Height - 22);
            }
        }

        private void DrawPauseOverlay(Graphics g)
        {
            int pw = sidePanel?.Width ?? 440;
            int w = ClientSize.Width - pw, h = ClientSize.Height;
            using (var b = new SolidBrush(Color.FromArgb(120, Color.Black))) g.FillRectangle(b, 0, 0, w, h);
            using (var font = new Font("Arial", 48, FontStyle.Bold))
            {
                const string t = "PAUSED";
                var sz = g.MeasureString(t, font);
                float tx = (w - sz.Width) / 2f, ty = h / 2f - sz.Height / 2f - 20;
                using (var sh = new SolidBrush(Color.FromArgb(180, Color.Black))) g.DrawString(t, font, sh, tx + 3, ty + 3);
                using (var mb = new SolidBrush(Color.FromArgb(230, Color.Cyan))) g.DrawString(t, font, mb, tx, ty);
            }
            using (var font = new Font("Arial", 13))
            {
                const string hint = "Press SPACE to resume";
                var sz = g.MeasureString(hint, font);
                g.DrawString(hint, font, new SolidBrush(Color.FromArgb(180, Color.LightGray)),
                    (w - sz.Width) / 2f, h / 2f + 36);
            }
        }

        private void DrawEndOverlay(Graphics g)
        {
            int pw = sidePanel?.Width ?? 440;
            int w = ClientSize.Width - pw, h = ClientSize.Height;
            using (var b = new SolidBrush(Color.FromArgb(160, Color.Black))) g.FillRectangle(b, 0, 0, w, h);
            bool vic = world.State == GameState.Victory;
            string title = vic ? "VICTORY!" : "DEFEAT!";
            using (var font = new Font("Arial", 40, FontStyle.Bold))
            {
                var sz = g.MeasureString(title, font);
                using (var b = new SolidBrush(vic ? Color.Gold : Color.Red))
                    g.DrawString(title, font, b, (w - sz.Width) / 2, h / 2 - 80);
            }
            using (var font = new Font("Arial", 16))
            {
                string sub = vic ? "The enemy fleet is destroyed!" : "Your mothership is gone...";
                string hint = "Press ESC to return to main menu";
                var sz = g.MeasureString(sub, font);
                g.DrawString(sub, font, Brushes.White, (w - sz.Width) / 2, h / 2);
                sz = g.MeasureString(hint, font);
                g.DrawString(hint, font, Brushes.LightGray, (w - sz.Width) / 2, h / 2 + 50);
            }
        }

        private void ShowEndScreen(bool v) { }

        // ── Input ──────────────────────────────────────────────────────────────

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: scrollLeft = true; break;
                case Keys.Right: scrollRight = true; break;
                case Keys.Up: scrollUp = true; break;
                case Keys.Down: scrollDown = true; break;
                case Keys.Oemplus: case Keys.Add: ZoomIn(); break;
                case Keys.OemMinus: case Keys.Subtract: ZoomOut(); break;
                case Keys.Space:
                    if (world.State == GameState.Playing) world.State = GameState.Paused;
                    else if (world.State == GameState.Paused) world.State = GameState.Playing;
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    if (world.State != GameState.Playing) { ReturnToMenu(); break; }
                    foreach (var s in selectedShips) { s.IsSelected = false; s.AttackTarget = null; }
                    foreach (var s in world.Ships) s.IsTargeted = false;
                    selectedShips.Clear();
                    break;
                case Keys.A:
                    if (e.Control)
                    {
                        selectedShips = world.Ships.Where(s => s.IsPlayerOwned && s.IsAlive && s.Type != ShipType.Mothership).ToList();
                        foreach (var s in world.Ships) s.IsSelected = selectedShips.Contains(s);
                    }
                    break;
                case Keys.F12:
                    _IsDebug = !_IsDebug;
                    _eventLogSep.Visible = _IsDebug;
                    eventLog.Visible     = _IsDebug;
                    break;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: scrollLeft = false; break;
                case Keys.Right: scrollRight = false; break;
                case Keys.Up: scrollUp = false; break;
                case Keys.Down: scrollDown = false; break;
            }
        }

        private void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var wp = ScreenToWorld(e.Location);

            // Double-click a player carrier → switch build source to that carrier
            var carrier = world.Ships.OfType<Carrier>()
                .FirstOrDefault(c => c.IsPlayerOwned && c.IsAlive
                    && c.HitTest(wp, GetShipHitRadius(c.Type)));
            if (carrier != null) { SetBuildSource(carrier); return; }

            // Double-click the mothership → switch build source back to mothership
            if (world.PlayerMothership?.IsAlive == true
                    && world.PlayerMothership.HitTest(wp, 45))
                SetBuildSource(world.PlayerMothership);
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                isPanning = true; panLastPos = e.Location; Cursor = Cursors.SizeAll; return;
            }
            if (e.Button == MouseButtons.Left)
            {
                var worldPt = ScreenToWorld(e.Location);
                var clicked = world.Ships.Where(s => s.IsPlayerOwned && s.IsAlive)
                    .FirstOrDefault(s => s.HitTest(worldPt, GetShipHitRadius(s.Type)));
                if (clicked != null)
                {
                    if (!ModifierKeys.HasFlag(Keys.Shift)) { foreach (var s in selectedShips) s.IsSelected = false; selectedShips.Clear(); }

                    if (clicked == _groupSelectAnchor)
                    {
                        // Second click on the same ship → narrow to just this ship
                        clicked.IsSelected = true;
                        selectedShips.Add(clicked);
                        _groupSelectAnchor = null;
                    }
                    else
                    {
                        // First click → select all same-type ships within GroupSelectRadius
                        float r2 = GroupSelectRadius * GroupSelectRadius;
                        var group = world.Ships.Where(s => s.IsPlayerOwned && s.IsAlive
                            && s.Type == clicked.Type
                            && (s.Position.X - clicked.Position.X) * (s.Position.X - clicked.Position.X)
                             + (s.Position.Y - clicked.Position.Y) * (s.Position.Y - clicked.Position.Y) <= r2)
                            .ToList();
                        foreach (var s in group) { s.IsSelected = true; if (!selectedShips.Contains(s)) selectedShips.Add(s); }
                        _groupSelectAnchor = clicked;
                    }
                    return;
                }
                // Clicked empty space → start drag-select; reset group anchor
                _groupSelectAnchor = null;
                dragStart = e.Location; panLastPos = e.Location;
                if (!ModifierKeys.HasFlag(Keys.Shift)) { foreach (var s in selectedShips) s.IsSelected = false; selectedShips.Clear(); }
            }
            else if (e.Button == MouseButtons.Right)
            {
                dragStart = e.Location; panLastPos = e.Location;
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle) { isPanning = false; Cursor = Cursors.Default; return; }
            if (e.Button == MouseButtons.Left)
            {
                if (isPanning) { isPanning = false; Cursor = Cursors.Default; }
                else if (dragStart.HasValue && dragRect != RectangleF.Empty)
                {
                    var wr = ScreenRectToWorld(dragRect);
                    var inRect = world.Ships.Where(s => s.IsPlayerOwned && s.IsAlive
                        && s.Type != ShipType.Mothership && wr.Contains(s.Position)).ToList();
                    if (!ModifierKeys.HasFlag(Keys.Shift)) { foreach (var s in selectedShips) s.IsSelected = false; selectedShips.Clear(); }
                    foreach (var s in inRect) { s.IsSelected = true; if (!selectedShips.Contains(s)) selectedShips.Add(s); }
                    _groupSelectAnchor = null;   // drag-select replaces the click-select state
                }
                dragRect = RectangleF.Empty; dragStart = null;
            }
            else if (e.Button == MouseButtons.Right)
            {
                bool wasPanning = isPanning;
                if (isPanning) { isPanning = false; Cursor = Cursors.Default; }
                dragRect = RectangleF.Empty; dragStart = null;

                if (!wasPanning)
                {
                    var worldPt = ScreenToWorld(e.Location);

                    var enemy = world.Ships.Where(s => !s.IsPlayerOwned && s.IsAlive)
                        .FirstOrDefault(s => s.HitTest(worldPt, GetShipHitRadius(s.Type)));
                    if (enemy != null)
                    {
                        foreach (var s in world.Ships) s.IsTargeted = false;
                        var atk = selectedShips.Where(s => s.IsAlive).ToList();
                        if (atk.Count > 0) { world.AssignAttackTarget(atk, enemy); return; }
                    }

                    var ast = world.Asteroids.FirstOrDefault(a => a.IsAlive && a.HitTest(worldPt));
                    if (ast != null)
                    {
                        var miners = selectedShips.OfType<Miner>().ToList();
                        if (miners.Count > 0) { world.AssignMiners(miners, ast); world.LogEvent($"{miners.Count} worker(s) → asteroid"); return; }
                    }

                    // ── Categorise selected ships into three formation groups ──────
                    var workers = new List<Ship>();
                    var lightFighters = new List<Ship>();
                    var capitals = new List<Ship>();

                    foreach (var s in selectedShips)
                    {
                        if (!s.IsAlive) continue;
                        switch (s.Type)
                        {
                            case ShipType.Interceptor:
                            case ShipType.Bomber:
                            case ShipType.Corvette:
                                lightFighters.Add(s); break;
                            case ShipType.Frigate:
                            case ShipType.Destroyer:
                            case ShipType.Battlecruiser:
                            case ShipType.Carrier:
                                capitals.Add(s); break;
                            default:   // Miner, ResourceCollector, Mothership
                                workers.Add(s); break;
                        }
                    }

                    // ── Workers: tight circle cluster ─────────────────────────────
                    int wCnt = workers.Count;
                    for (int i = 0; i < wCnt; i++)
                    {
                        workers[i].AttackTarget = null;
                        workers[i].FormationWaypoint = null;
                        float spread = Math.Min(wCnt * 5f, 20f);
                        double angle = i * Math.PI * 2 / Math.Max(wCnt, 1);
                        workers[i].Destination = new PointF(
                            worldPt.X + (float)Math.Cos(angle) * spread,
                            worldPt.Y + (float)Math.Sin(angle) * spread);
                        workers[i].IsMining = false;
                        workers[i].ReturningToMothership = false;
                    }

                    // ── Light fighters: expanding DELTA (triangle) formation ─────
                    if (lightFighters.Count > 0)
                    {
                        // Find the ship closest to destination as the formation anchor.
                        // This prevents unnecessary regrouping - farther ships catch up to the closer one.
                        float cx = 0f, cy = 0f, minDist = float.MaxValue;
                        foreach (var f in lightFighters)
                        {
                            float dx = f.Position.X - worldPt.X;
                            float dy = f.Position.Y - worldPt.Y;
                            float dist = dx * dx + dy * dy;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                cx = f.Position.X;
                                cy = f.Position.Y;
                            }
                        }

						float fdx = worldPt.X - cx, fdy = worldPt.Y - cy;
                        float fdist = (float)Math.Sqrt(fdx * fdx + fdy * fdy);
                        PointF fwd = fdist < 1f
                            ? new PointF(1f, 0f)
                            : new PointF(fdx / fdist, fdy / fdist);
                        PointF right = new PointF(fwd.Y, -fwd.X);   // 90° clockwise

                        // Row 0 = 1-ship tip, row 1 = 2 ships, row 2 = 3 ships, …
                        // Partial last rows are spread to the full row width so the
                        // triangle silhouette is preserved for any ship count.
                        const float rowSpacing = 15f;
                        const float colSpacing = 15f;

                        int n = lightFighters.Count, idx = 0;
                        for (int row = 0; idx < n; row++)
                        {
                            int shipsInRow = Math.Min(row + 1, n - idx);
                            float rear = row * rowSpacing;

                            for (int i = 0; i < shipsInRow; i++, idx++)
                            {
                                float lat = shipsInRow == 1
                                    ? 0f
                                    : (-row / 2.0f + (float)i * row / (shipsInRow - 1)) * colSpacing;

                                lightFighters[idx].AttackTarget = null;
                                lightFighters[idx].FormationWaypoint = ClampToMap(new PointF(
                                    cx + lat * right.X - rear * fwd.X,
                                    cy + lat * right.Y - rear * fwd.Y));
                                lightFighters[idx].Destination = ClampToMap(new PointF(
                                    worldPt.X + lat * right.X - rear * fwd.X,
                                    worldPt.Y + lat * right.Y - rear * fwd.Y));
                                lightFighters[idx].IsMining = false;
                                lightFighters[idx].ReturningToMothership = false;
                            }
                        }
                    }

                    // ── Capitals: WALL formation (line abreast, ⊥ to travel) ──────
                    if (capitals.Count > 0)
                    {
                        // Find the ship closest to destination as the formation anchor.
                        // This prevents unnecessary regrouping - farther ships catch up to the closer one.
                        float cx = 0f, cy = 0f, minDist = float.MaxValue;
                        foreach (var c in capitals)
                        {
                            float dx = c.Position.X - worldPt.X;
                            float dy = c.Position.Y - worldPt.Y;
                            float dist = dx * dx + dy * dy;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                cx = c.Position.X;
                                cy = c.Position.Y;
                            }
                        }

						float fdx = worldPt.X - cx, fdy = worldPt.Y - cy;
                        float fdist = (float)Math.Sqrt(fdx * fdx + fdy * fdy);
                        PointF fwd = fdist < 1f
                            ? new PointF(1f, 0f)
                            : new PointF(fdx / fdist, fdy / fdist);
                        PointF right = new PointF(fwd.Y, -fwd.X);   // 90° clockwise

                        // Space ships evenly along the perpendicular axis, centred on worldPt.
                        // 55 wu gap accommodates the widths of Frigate (44), Destroyer (66),
                        // and Battlecruiser (94) with a small margin between hulls.
                        const float wallSpacing = 55f;
                        int n = capitals.Count;
                        for (int i = 0; i < n; i++)
                        {
                            float wallOff = (i - (n - 1) / 2.0f) * wallSpacing;
                            capitals[i].AttackTarget = null;
                            capitals[i].FormationWaypoint = ClampToMap(new PointF(
                                cx + wallOff * right.X,
                                cy + wallOff * right.Y));
                            capitals[i].Destination = ClampToMap(new PointF(
                                worldPt.X + wallOff * right.X,
                                worldPt.Y + wallOff * right.Y));
                            capitals[i].IsMining = false;
                            capitals[i].ReturningToMothership = false;
                        }
                    }
                }
            }
        }

		private void OnMouseMove(object sender, MouseEventArgs e)
        {

			if (dragStart.HasValue && e.Button == MouseButtons.Left)
            {
                float x = Math.Min(dragStart.Value.X, e.X), y = Math.Min(dragStart.Value.Y, e.Y);
                float w = Math.Abs(e.X - dragStart.Value.X), h = Math.Abs(e.Y - dragStart.Value.Y);
                if (w > 5 || h > 5) dragRect = new RectangleF(x, y, w, h);
            }

            if ((e.Button == MouseButtons.Middle) ||
                (e.Button == MouseButtons.Right && dragStart.HasValue &&
                 (Math.Abs(e.X - dragStart.Value.X) > 8 || Math.Abs(e.Y - dragStart.Value.Y) > 8)))
            {
                if (!isPanning) { isPanning = true; Cursor = Cursors.SizeAll; }
                float dx = (e.X - panLastPos.X) / zoom, dy = (e.Y - panLastPos.Y) / zoom;
                cameraOffset = ClampCameraOffset(new PointF(cameraOffset.X + dx, cameraOffset.Y + dy));
                panLastPos = e.Location;
            }

            if (e.Button == MouseButtons.None)
            {
                var wp = ScreenToWorld(e.Location);
                bool oe = world.Ships.Any(s => !s.IsPlayerOwned && s.IsAlive && s.HitTest(wp, GetShipHitRadius(s.Type)));
                Cursor = oe && selectedShips.Count > 0 ? Cursors.Cross : Cursors.Default;
            }
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0) ZoomIn(e.Location); else ZoomOut(e.Location);
        }

        private void ZoomIn(Point? mp = null)
        {
            var pt = mp ?? new Point(ClientSize.Width / 2, ClientSize.Height / 2);
            PointF b = ScreenToWorld(pt);
            zoom = Math.Min(ZoomMax, zoom + ZoomStep);
            cameraOffset = new PointF(pt.X / zoom - b.X, pt.Y / zoom - b.Y);
        }

        private void ZoomOut(Point? mp = null)
        {
            var pt = mp ?? new Point(ClientSize.Width / 2, ClientSize.Height / 2);
            PointF b = ScreenToWorld(pt);
            zoom = Math.Max(ZoomMin, zoom - ZoomStep);
            cameraOffset = new PointF(pt.X / zoom - b.X, pt.Y / zoom - b.Y);
        }

        private PointF ScreenToWorld(Point p) =>
            new PointF(p.X / zoom - cameraOffset.X, p.Y / zoom - cameraOffset.Y);

        private RectangleF ScreenRectToWorld(RectangleF r)
        {
            var tl = ScreenToWorld(new Point((int)r.Left, (int)r.Top));
            var br = ScreenToWorld(new Point((int)r.Right, (int)r.Bottom));
            return new RectangleF(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
        }

        private float GetShipHitRadius(ShipType type)
        {
            switch (type)
            {
                case ShipType.Mothership: return 45;
                case ShipType.Worker: return 12;
                case ShipType.Interceptor:
                case ShipType.Bomber: return 10;
                case ShipType.Corvette: return 11;
                case ShipType.Frigate: return 18;
                case ShipType.Destroyer: return 22;
                case ShipType.Battlecruiser: return 30;
                case ShipType.ResourceCollector: return 20;
                case ShipType.Carrier:           return 50;
                default:                         return 15;
            }
        }

        private void ReturnToMenu()
        {
            gameTimer?.Stop(); StopMusic();
            new MainMenuForm().Show(); Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            gameTimer?.Stop(); StopMusic();
            base.OnFormClosed(e);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void SP(Control c) => sidePanel.Controls.Add(c);
        private static Point P(int x, int y) => new Point(x, y);

        private static PointF ClampToMap(PointF pt) => new PointF(
            Math.Max(0f, Math.Min(GameConstants.MapWidth,  pt.X)),
            Math.Max(0f, Math.Min(GameConstants.MapHeight, pt.Y)));

        private PointF ClampCameraOffset(PointF offset)
        {
            float viewportWidth = ClientSize.Width - sidePanelWidth;
            float viewportHeight = ClientSize.Height;

            // Valid camera offset range: [viewportWidth/zoom - MapWidth, 0]
            // This ensures viewport only shows world space [0, MapWidth] × [0, MapHeight]
            float minOffsetX = viewportWidth / zoom - GameConstants.MapWidth;
            float maxOffsetX = 0f;
            float minOffsetY = viewportHeight / zoom - GameConstants.MapHeight;
            float maxOffsetY = 0f;

            return new PointF(
                Math.Max(minOffsetX, Math.Min(maxOffsetX, offset.X)),
                Math.Max(minOffsetY, Math.Min(maxOffsetY, offset.Y)));
        }

        private Label Sep(string t, int y) => new Label
        {
            Text = t,
            ForeColor = Color.DimGray,
            Font = new Font("Arial", 8),
            AutoSize = true,
            Location = P(8, y)
        };

        private Label SPL(string text, Color color, float size, bool bold, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                ForeColor = color,
                Font = new Font("Arial", size, bold ? FontStyle.Bold : FontStyle.Regular),
                AutoSize = true,
                Location = P(x, y)
            };
            sidePanel.Controls.Add(lbl);
            return lbl;
        }

        private Color GetDiffColor() => Color.Cyan;

        private string GetOpponentSummary()
        {
            if (world.Enemies.Count == 0) return "No opponents";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var e in world.Enemies)
                parts.Add(e.Level.ToString());
            return $"vs {world.Enemies.Count} AI: {string.Join(", ", parts)}";
        }

        // ── Inline Research Row ────────────────────────────────────────────────────
        // Lives permanently in the docked side panel — no floating overlay.
        internal class ResearchRowInline
        {
            public event Action<ShipType> ResearchClicked;

            private readonly ShipType type;
            private readonly Label levelLbl;
            private readonly Label costLbl;
            private readonly Label timeLbl;
            private readonly Button resBtn;

            public IEnumerable<Control> Controls
            {
                get { yield return levelLbl; yield return costLbl; yield return timeLbl; yield return resBtn; }
            }

            public ResearchRowInline(ShipType type, int totalW, int y, GameWorld world)
            {
                this.type = type;

                // Column layout — slightly wider to use the extra side-panel space
                int c0 = 0, w0 = 112;
                int c1 = 114, w1 = 58;
                int c2 = 174, w2 = 42;
                int c3 = 218, w3 = totalW - 222;

                levelLbl = new Label
                {
                    Font = new Font("Consolas", 8),
                    AutoSize = false,
                    Size = new Size(w0, 24),
                    Location = new Point(c0, y),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                costLbl = new Label
                {
                    Font = new Font("Consolas", 8),
                    AutoSize = false,
                    Size = new Size(w1, 24),
                    Location = new Point(c1, y),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                timeLbl = new Label
                {
                    Font = new Font("Consolas", 8),
                    AutoSize = false,
                    Size = new Size(w2, 24),
                    Location = new Point(c2, y),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                resBtn = new Button
                {
                    Location = new Point(c3, y),
                    Size = new Size(w3, 24),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Consolas", 7, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    TabStop = false
                };
                resBtn.FlatAppearance.BorderSize = 1;
                resBtn.Click += (s, e) => ResearchClicked?.Invoke(type);

                Refresh(world);
            }

            public void Refresh(GameWorld world)
            {
                var rm = world.Research;
                int level = rm.Levels[type];
                bool maxed    = level >= 3;
                bool busy     = rm.ActiveOrder != null;
                bool myTurn   = rm.IsResearching(type);
                bool queued   = rm.IsQueued(type);
                bool queueFull = rm.QueueCount >= 2;
                bool canAfford = !maxed && world.PlayerResources >= rm.CostFor(type);

                string lvStr = level == 0 ? "Base" : $"Mk.{level}";
                levelLbl.Text = $"{type,-11}{lvStr}";
                levelLbl.ForeColor = level == 3 ? Color.Gold
                                   : level == 2 ? Color.LightBlue
                                   : level == 1 ? Color.LightGreen : Color.LightGray;

                if (maxed) { costLbl.Text = "MAX"; costLbl.ForeColor = Color.Gold; }
                else { costLbl.Text = $"{rm.CostFor(type)}r"; costLbl.ForeColor = canAfford ? Color.LightGreen : Color.IndianRed; }

                if (maxed) { timeLbl.Text = "──"; timeLbl.ForeColor = Color.DimGray; }
                else if (myTurn) { int s = Math.Max(0, (rm.ActiveOrder.TotalMs - rm.ActiveOrder.Elapsed) / 1000); timeLbl.Text = $"{s}s"; timeLbl.ForeColor = Color.Plum; }
                else { timeLbl.Text = $"{rm.DurationFor(type) / 1000}s"; timeLbl.ForeColor = Color.DimGray; }

                if (maxed)
                {
                    resBtn.Text = "✓MAX"; resBtn.Enabled = false;
                    resBtn.ForeColor = Color.Gold; resBtn.BackColor = Color.FromArgb(30, 25, 10);
                    resBtn.FlatAppearance.BorderColor = Color.DarkGoldenrod;
                }
                else if (myTurn)
                {
                    resBtn.Text = "Running…"; resBtn.Enabled = false;
                    resBtn.ForeColor = Color.Plum; resBtn.BackColor = Color.FromArgb(25, 10, 35);
                    resBtn.FlatAppearance.BorderColor = Color.MediumPurple;
                }
                else if (queued)
                {
                    // This type is sitting in the pending queue
                    resBtn.Text = "Queued"; resBtn.Enabled = false;
                    resBtn.ForeColor = Color.Orange; resBtn.BackColor = Color.FromArgb(30, 20, 5);
                    resBtn.FlatAppearance.BorderColor = Color.DarkOrange;
                }
                else if (busy)
                {
                    // Lab is busy with something else — offer queue slot if available
                    if (!queueFull && canAfford)
                    {
                        string nl = level == 0 ? "+Queue Mk.I" : level == 1 ? "+Queue Mk.II" : "+Queue Mk.III";
                        resBtn.Text = nl; resBtn.Enabled = true;
                        resBtn.ForeColor = Color.Yellow; resBtn.BackColor = Color.FromArgb(28, 24, 5);
                        resBtn.FlatAppearance.BorderColor = Color.Goldenrod;
                    }
                    else if (!queueFull)
                    {
                        resBtn.Text = "Need res"; resBtn.Enabled = false;
                        resBtn.ForeColor = Color.DimGray; resBtn.BackColor = Color.FromArgb(20, 20, 30);
                        resBtn.FlatAppearance.BorderColor = Color.DimGray;
                    }
                    else
                    {
                        resBtn.Text = "Q.Full"; resBtn.Enabled = false;
                        resBtn.ForeColor = Color.DimGray; resBtn.BackColor = Color.FromArgb(20, 20, 30);
                        resBtn.FlatAppearance.BorderColor = Color.DimGray;
                    }
                }
                else
                {
                    string nl = level == 0 ? "→Mk.I" : level == 1 ? "→Mk.II" : "→Mk.III";
                    resBtn.Text = canAfford ? nl : "Need res"; resBtn.Enabled = canAfford;
                    resBtn.ForeColor = canAfford ? Color.Cyan : Color.DimGray;
                    resBtn.BackColor = canAfford ? Color.FromArgb(15, 35, 50) : Color.FromArgb(20, 20, 30);
                    resBtn.FlatAppearance.BorderColor = canAfford ? Color.CadetBlue : Color.DimGray;
                }
            }
        }

        // ── NoFocusPanel ───────────────────────────────────────────────────────────
        // Returns MA_NOACTIVATE on every mouse message so clicking any button inside
        // the side panel never steals keyboard focus from the game canvas.
        internal class NoFocusPanel : Panel
        {
            protected override void WndProc(ref Message m)
            {
                const int WM_MOUSEACTIVATE = 0x0021;
                const int MA_NOACTIVATE = 3;
                if (m.Msg == WM_MOUSEACTIVATE)
                {
                    m.Result = (IntPtr)MA_NOACTIVATE;
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
