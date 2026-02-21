using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FleetCommand
{
    public class MainMenuForm : Form
    {
        private int     opponentCount = 1;
        private AiLevel[] opponentLevels = { AiLevel.Normal, AiLevel.Normal, AiLevel.Normal };

        private Timer animTimer;
        private float animPhase = 0;

        // UI refs for dynamic updates
        private Button[]   countBtns  = new Button[3];
        private Panel[]    aiRows     = new Panel[3];
        private Button[][] levelBtns  = new Button[3][];

        public MainMenuForm()
        {
            Text            = "Fleet Command";
            Size            = new Size(820, 760);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = Color.Black;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer, true);

            BuildUI();

            animTimer = new Timer { Interval = 33 };
            animTimer.Tick += (s, e) => { animPhase += 0.02f; Invalidate(); };
            animTimer.Start();
        }

        private void BuildUI()
        {
            int cx = 410;   // centre x

            // ── Title ─────────────────────────────────────────────────────────
            Controls.Add(new Label {
                Text = "FLEET COMMAND", ForeColor = Color.Cyan,
                Font = new Font("Arial", 32, FontStyle.Bold),
                AutoSize = true, BackColor = Color.Transparent,
                Location = new Point(cx - 210, 28)
            });
            Controls.Add(new Label {
                Text = "A Space Strategy Game", ForeColor = Color.DimGray,
                Font = new Font("Arial", 12, FontStyle.Italic),
                AutoSize = true, BackColor = Color.Transparent,
                Location = new Point(cx - 110, 80)
            });

            // ── Opponent count ────────────────────────────────────────────────
            Controls.Add(MakeHeader("NUMBER OF OPPONENTS", cx - 150, 120));

            string[] countLabels = { "1  Opponent", "2  Opponents", "3  Opponents" };
            for (int i = 0; i < 3; i++)
            {
                int n = i + 1;
                var btn = MakeBtn(countLabels[i], cx - 150 + i * 190, 148, 175, 36,
                    Color.Cyan, i == 0);
                btn.Tag = n;
                btn.Click += (s, e) => {
                    opponentCount = (int)((Button)s).Tag;
                    foreach (var b in countBtns) SetSelected(b, (int)b.Tag == opponentCount);
                    UpdateAiRows();
                    this.Focus();
                };
                countBtns[i] = btn;
                Controls.Add(btn);
            }

            // ── Per-opponent AI level rows ────────────────────────────────────
            Controls.Add(MakeHeader("OPPONENT AI LEVELS", cx - 150, 202));

            Color[] aiColors = { Color.LimeGreen, Color.Yellow, Color.Orange, Color.Red };
            string[] aiLabels = { "Easy", "Normal", "Hard", "Expert" };
            string[] aiDesc = {
                "Slow economy, small waves, no targeting",
                "Balanced economy & military expansion",
                "Smart economy, mixed fleet, targets miners",
                "Fast expansion, heavy ships, disrupts you"
            };

            for (int row = 0; row < 3; row++)
            {
                int r = row;
                var panel = new Panel {
                    Location  = new Point(cx - 280, 228 + r * 100),
                    Size      = new Size(560, 90),
                    BackColor = Color.FromArgb(12, 12, 28)
                };

                panel.Controls.Add(new Label {
                    Text = $"Opponent {r + 1}", ForeColor = Color.LightGray,
                    Font = new Font("Consolas", 9, FontStyle.Bold),
                    AutoSize = true, Location = new Point(8, 6)
                });

                levelBtns[r] = new Button[4];
                for (int lvl = 0; lvl < 4; lvl++)
                {
                    int l = lvl;
                    AiLevel al = (AiLevel)lvl;
                    var lb = MakeBtn(aiLabels[lvl], 8 + lvl * 132, 26, 126, 30,
                        aiColors[lvl], opponentLevels[r] == al);
                    lb.Tag = al;
                    lb.Click += (s, e) => {
                        opponentLevels[r] = (AiLevel)((Button)s).Tag;
                        foreach (var b in levelBtns[r]) SetSelected(b, (AiLevel)b.Tag == opponentLevels[r]);
                        this.Focus();
                    };
                    levelBtns[r][lvl] = lb;
                    panel.Controls.Add(lb);
                }

                // Description label — shows description for currently selected level
                var descLbl = new Label {
                    Text = aiDesc[(int)opponentLevels[r]],
                    ForeColor = Color.DarkGray, Font = new Font("Consolas", 7),
                    AutoSize = false, Size = new Size(544, 16),
                    Location = new Point(8, 62), BackColor = Color.Transparent
                };
                panel.Controls.Add(descLbl);

                // Update desc when level changes
                for (int lvl = 0; lvl < 4; lvl++)
                {
                    int l = lvl;
                    levelBtns[r][lvl].Click += (s, e) =>
                        descLbl.Text = aiDesc[(int)opponentLevels[r]];
                }

                aiRows[r] = panel;
                Controls.Add(panel);
            }

            UpdateAiRows();

            // ── Divider ───────────────────────────────────────────────────────
            int launchY = 228 + 3 * 100 + 16;

            Controls.Add(new Label {
                Text = "All players start with equal resources — victory through better strategy.",
                ForeColor = Color.FromArgb(80, 140, 80), Font = new Font("Consolas", 8),
                AutoSize = true, BackColor = Color.Transparent,
                Location = new Point(cx - 210, launchY)
            });

            // ── Launch ────────────────────────────────────────────────────────
            var playBtn = new Button {
                Text = "▶  LAUNCH GAME",
                Size = new Size(300, 50), Location = new Point(cx - 150, launchY + 22),
                BackColor = Color.FromArgb(20, 60, 20), ForeColor = Color.LimeGreen,
                FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 14, FontStyle.Bold)
            };
            playBtn.FlatAppearance.BorderColor = Color.LimeGreen;
            playBtn.FlatAppearance.BorderSize  = 2;
            playBtn.Click += StartGame;
            Controls.Add(playBtn);

            var quitBtn = new Button {
                Text = "QUIT", Size = new Size(140, 30),
                Location = new Point(cx + 10, launchY + 80),
                BackColor = Color.FromArgb(40, 10, 10), ForeColor = Color.OrangeRed,
                FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10)
            };
            quitBtn.Click += (s, e) => Application.Exit();
            Controls.Add(quitBtn);
        }

        private void UpdateAiRows()
        {
            for (int i = 0; i < 3; i++)
                aiRows[i].Visible = (i < opponentCount);
        }

        private void StartGame(object sender, EventArgs e)
        {
            animTimer?.Stop();
            var levels = new List<AiLevel>();
            for (int i = 0; i < opponentCount; i++)
                levels.Add(opponentLevels[i]);

            var world    = new GameWorld(levels);
            var gameForm = new GameForm(world);
            gameForm.Show();
            Hide();
            gameForm.FormClosed += (s, ev) => { Show(); animTimer?.Start(); };
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private Label MakeHeader(string text, int x, int y)
        {
            return new Label {
                Text = text, ForeColor = Color.LightGray,
                Font = new Font("Arial", 10, FontStyle.Bold),
                AutoSize = true, BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
        }

        private Button MakeBtn(string text, int x, int y, int w, int h, Color col, bool selected)
        {
            var btn = new Button {
                Text = text, Size = new Size(w, h), Location = new Point(x, y),
                BackColor = selected ? Color.FromArgb(30, 40, 70) : Color.FromArgb(15, 15, 30),
                ForeColor = col, FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 9, FontStyle.Bold),
                TabStop = false
            };
            btn.FlatAppearance.BorderColor = selected ? col : Color.FromArgb(50, col);
            btn.FlatAppearance.BorderSize  = selected ? 2 : 1;
            return btn;
        }

        private void SetSelected(Button btn, bool selected)
        {
            btn.BackColor = selected ? Color.FromArgb(30, 40, 70) : Color.FromArgb(15, 15, 30);
            btn.FlatAppearance.BorderSize = selected ? 2 : 1;
            btn.FlatAppearance.BorderColor = selected
                ? btn.ForeColor
                : Color.FromArgb(50, btn.ForeColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(5, 5, 20), Color.FromArgb(8, 5, 25), LinearGradientMode.Vertical))
                g.FillRectangle(bg, ClientRectangle);

            var rng2 = new Random(99);
            for (int i = 0; i < 150; i++)
            {
                float x = rng2.Next(0, 820), fy = rng2.Next(0, 760);
                float blink = 0.5f + 0.5f * (float)Math.Sin(animPhase + i);
                int br = (int)(80 + 120 * blink);
                int sz = rng2.Next(1, 3);
                g.FillEllipse(new SolidBrush(Color.FromArgb(br, br, br)), x, fy, sz, sz);
            }
            base.OnPaint(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            animTimer?.Stop();
            base.OnFormClosed(e);
        }
    }
}
