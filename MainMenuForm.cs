using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FleetCommand
{
    public class MainMenuForm : Form
    {
        private int     opponentCount  = 1;
        private AiLevel[] opponentLevels = { AiLevel.Normal, AiLevel.Normal, AiLevel.Normal };

        private Timer animTimer;
        private float animPhase = 0f;

        // Pre-generated star data — avoids per-frame allocations
        private struct StarData { public float X, Y, Sz, Phase; }
        private StarData[] stars;

        // UI refs for dynamic updates
        private Button[]   countBtns  = new Button[3];
        private Panel[]    aiRows     = new Panel[3];
        private Button[][] levelBtns  = new Button[3][];
        private Label[]    descLabels = new Label[3];

        // ── Layout constants ──────────────────────────────────────────────────
        //   Everything is anchored to CL/CW so all elements share the same
        //   left edge and right edge, and are perfectly centred on CX.
        private const int FW = 820;   // form width
        private const int FH = 760;   // form height
        private const int CX = 410;   // horizontal centre
        private const int CL = 115;   // content left  edge
        private const int CW = 590;   // content width  → right edge = 705

        // ── Accent colours (single source of truth) ───────────────────────────
        private static readonly Color AccentCyan   = Color.FromArgb( 55, 185, 255);
        private static readonly Color AccentGreen  = Color.FromArgb( 75, 215,  95);
        private static readonly Color AiEasy       = Color.FromArgb( 75, 210, 110);
        private static readonly Color AiNormal     = Color.FromArgb( 65, 165, 255);
        private static readonly Color AiHard       = Color.FromArgb(255, 190,  50);
        private static readonly Color AiExpert     = Color.FromArgb(255,  80,  70);

        // ─────────────────────────────────────────────────────────────────────
        public MainMenuForm()
        {
            Text            = "Fleet Command";
            Size            = new Size(FW, FH);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = Color.Black;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer, true);

            GenerateStars();
            BuildUI();

            animTimer = new Timer { Interval = 33 };
            animTimer.Tick += (s, e) => { animPhase += 0.018f; Invalidate(); };
            animTimer.Start();
        }

        // ── Star field ────────────────────────────────────────────────────────
        private void GenerateStars()
        {
            var rng = new Random(42);
            stars = new StarData[200];
            for (int i = 0; i < stars.Length; i++)
                stars[i] = new StarData {
                    X     = rng.Next(0, FW),
                    Y     = rng.Next(0, FH),
                    Sz    = 0.7f + (float)(rng.NextDouble() * 2.3),
                    Phase = (float)(rng.NextDouble() * Math.PI * 2)
                };
        }

        // ── Build UI ──────────────────────────────────────────────────────────
        private void BuildUI()
        {
            int y;   // running y cursor

            // ── Title ─────────────────────────────────────────────────────────
            var titleLbl = new Label {
                Text      = "FLEET COMMAND",
                ForeColor = Color.FromArgb(0, 210, 255),
                Font      = new Font("Arial", 34, FontStyle.Bold),
                AutoSize  = true, BackColor = Color.Transparent
            };
            Controls.Add(titleLbl);
            titleLbl.Location = new Point(CX - titleLbl.PreferredWidth / 2, 28);

            var subLbl = new Label {
                Text      = "A  SPACE  REAL-TIME  STRATEGY",
                ForeColor = Color.FromArgb(70, 105, 148),
                Font      = new Font("Segoe UI", 9),
                AutoSize  = true, BackColor = Color.Transparent
            };
            Controls.Add(subLbl);
            subLbl.Location = new Point(CX - subLbl.PreferredWidth / 2, 82);

            // ── Opponents section ──────────────────────────────────────────────
            y = 124;
            Controls.Add(MakeSectionHeader("NUMBER OF OPPONENTS", CL, y));
            y += 27;

            // Three count buttons — evenly distributed across the full content width
            // 3 × btnW + 2 × gap = CW  →  with gap = 10 → btnW = (CW-20)/3 = 190
            int cBtnW = (CW - 20) / 3;                              // 190
            int cBtnX = CX - (3 * cBtnW + 2 * 10) / 2;             // = CL = 115

            string[] countLabels = { "1  Opponent", "2  Opponents", "3  Opponents" };
            for (int i = 0; i < 3; i++)
            {
                int n  = i + 1;
                int bx = cBtnX + i * (cBtnW + 10);
                var btn = MakeToggleBtn(countLabels[i], bx, y, cBtnW, 36, AccentCyan, i == 0);
                btn.Tag    = n;
                btn.Click += (s, e) => {
                    opponentCount = (int)((Button)s).Tag;
                    foreach (var b in countBtns) SetSelected(b, (int)b.Tag == opponentCount);
                    UpdateAiRows();
                    Focus();
                };
                countBtns[i] = btn;
                Controls.Add(btn);
            }
            y += 50;

            // ── AI-level section ───────────────────────────────────────────────
            Controls.Add(MakeSectionHeader("OPPONENT AI LEVELS", CL, y));
            y += 27;

            Color[]  aiColors = { AiEasy, AiNormal, AiHard, AiExpert };
            string[] aiLabels = { "Easy", "Normal", "Hard", "Expert" };
            string[] aiDescs  = {
                "Slow economy · small attack waves · no priority targeting",
                "Balanced economy · mixed fleet · standard aggression",
                "Smart economy · mixed fleet · targets your miners",
                "Fast expansion · heavy warships · disrupts your economy"
            };

            const int RowH   = 88;
            const int RowGap = 6;

            // Inside each panel: 4 level-buttons fill panel width minus padding
            const int LPad   = 14;
            const int LGap   = 8;
            int       lvlW   = (CW - 2 * LPad - 3 * LGap) / 4;   // (590-28-24)/4 = 134

            for (int row = 0; row < 3; row++)
            {
                int r = row;

                var panel = new Panel {
                    Location  = new Point(CL, y + r * (RowH + RowGap)),
                    Size      = new Size(CW, RowH),
                    BackColor = Color.FromArgb(8, 12, 28)
                };
                panel.Paint += (s, ev) => PaintAiPanel(ev.Graphics, (Panel)s);

                panel.Controls.Add(new Label {
                    Text      = $"OPPONENT  {r + 1}",
                    ForeColor = Color.FromArgb(95, 125, 168),
                    Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    AutoSize  = true, BackColor = Color.Transparent,
                    Location  = new Point(LPad, 8)
                });

                levelBtns[r] = new Button[4];
                for (int lvl = 0; lvl < 4; lvl++)
                {
                    AiLevel al = (AiLevel)lvl;
                    int     lx = LPad + lvl * (lvlW + LGap);
                    var     lb = MakeToggleBtn(aiLabels[lvl], lx, 27, lvlW, 30,
                                              aiColors[lvl], opponentLevels[r] == al);
                    lb.Tag    = al;
                    lb.Click += (s, e) => {
                        opponentLevels[r] = (AiLevel)((Button)s).Tag;
                        foreach (var b in levelBtns[r])
                            SetSelected(b, (AiLevel)b.Tag == opponentLevels[r]);
                        descLabels[r].Text = aiDescs[(int)opponentLevels[r]];
                        Focus();
                    };
                    levelBtns[r][lvl] = lb;
                    panel.Controls.Add(lb);
                }

                descLabels[r] = new Label {
                    Text      = aiDescs[(int)opponentLevels[r]],
                    ForeColor = Color.FromArgb(75, 100, 135),
                    Font      = new Font("Segoe UI", 7.5f),
                    AutoSize  = false,
                    Size      = new Size(CW - 2 * LPad, 16),
                    Location  = new Point(LPad, 64),
                    BackColor = Color.Transparent
                };
                panel.Controls.Add(descLabels[r]);

                aiRows[r] = panel;
                Controls.Add(panel);
            }

            // ── Bottom area ────────────────────────────────────────────────────
            int bottomY = y + 3 * (RowH + RowGap) + 14;

            // Hint line
            var hintLbl = new Label {
                Text      = "All players start with equal resources  ·  victory through better strategy",
                ForeColor = Color.FromArgb(48, 88, 58),
                Font      = new Font("Segoe UI", 8, FontStyle.Italic),
                AutoSize  = true, BackColor = Color.Transparent
            };
            Controls.Add(hintLbl);
            hintLbl.Location = new Point(CX - hintLbl.PreferredWidth / 2, bottomY);

            // Launch button — centred on CX
            var playBtn = new Button {
                Text      = "▶   LAUNCH GAME",
                Size      = new Size(248, 46),
                BackColor = Color.FromArgb(9, 38, 14),
                ForeColor = AccentGreen,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 13, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            playBtn.FlatAppearance.BorderColor = Color.FromArgb(52, 175, 70);
            playBtn.FlatAppearance.BorderSize  = 2;
            playBtn.Location = new Point(CX - playBtn.Width / 2, bottomY + 20);
            playBtn.MouseEnter += (s, e) => {
                playBtn.BackColor                  = Color.FromArgb(14, 58, 20);
                playBtn.ForeColor                  = Color.FromArgb(125, 255, 145);
                playBtn.FlatAppearance.BorderColor = Color.FromArgb(85, 230, 105);
            };
            playBtn.MouseLeave += (s, e) => {
                playBtn.BackColor                  = Color.FromArgb(9, 38, 14);
                playBtn.ForeColor                  = AccentGreen;
                playBtn.FlatAppearance.BorderColor = Color.FromArgb(52, 175, 70);
            };
            playBtn.Click += StartGame;
            Controls.Add(playBtn);

            // Quit button — centred on CX
            var quitBtn = new Button {
                Text      = "QUIT",
                Size      = new Size(100, 26),
                BackColor = Color.FromArgb(22, 6, 6),
                ForeColor = Color.FromArgb(155, 60, 48),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9),
                Cursor    = Cursors.Hand
            };
            quitBtn.FlatAppearance.BorderColor = Color.FromArgb(75, 32, 24);
            quitBtn.FlatAppearance.BorderSize  = 1;
            quitBtn.Location = new Point(CX - quitBtn.Width / 2, bottomY + 76);
            quitBtn.MouseEnter += (s, e) => {
                quitBtn.ForeColor                  = Color.FromArgb(205, 85, 68);
                quitBtn.FlatAppearance.BorderColor = Color.FromArgb(135, 52, 40);
            };
            quitBtn.MouseLeave += (s, e) => {
                quitBtn.ForeColor                  = Color.FromArgb(155, 60, 48);
                quitBtn.FlatAppearance.BorderColor = Color.FromArgb(75, 32, 24);
            };
            quitBtn.Click += (s, e) => Application.Exit();
            Controls.Add(quitBtn);

            UpdateAiRows();
        }

        // ── AI panel custom paint ─────────────────────────────────────────────
        private static void PaintAiPanel(Graphics g, Panel p)
        {
            // Subtle dark-blue border
            using (var pen = new Pen(Color.FromArgb(26, 48, 84), 1))
                g.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            // Left accent stripe
            using (var brush = new SolidBrush(Color.FromArgb(42, 105, 195)))
                g.FillRectangle(brush, 0, 0, 3, p.Height);
        }

        // ── Show/hide AI rows ─────────────────────────────────────────────────
        private void UpdateAiRows()
        {
            for (int i = 0; i < 3; i++)
                aiRows[i].Visible = (i < opponentCount);
        }

        // ── Launch game ───────────────────────────────────────────────────────
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

        // ── Widget helpers ────────────────────────────────────────────────────
        private static Label MakeSectionHeader(string text, int x, int y)
        {
            return new Label {
                Text      = text,
                ForeColor = Color.FromArgb(125, 150, 192),
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                AutoSize  = true, BackColor = Color.Transparent,
                Location  = new Point(x, y)
            };
        }

        private static Button MakeToggleBtn(string text, int x, int y, int w, int h,
                                            Color col, bool selected)
        {
            var btn = new Button {
                Text      = text,
                Size      = new Size(w, h),
                Location  = new Point(x, y),
                BackColor = selected ? Color.FromArgb(18, 32, 58) : Color.FromArgb(7, 9, 20),
                ForeColor = col,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                TabStop   = false,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = selected
                ? col : Color.FromArgb(48, col.R, col.G, col.B);
            btn.FlatAppearance.BorderSize = selected ? 2 : 1;

            // Subtle hover highlight for unselected state
            btn.MouseEnter += (s, e) => {
                var b = (Button)s;
                if (b.FlatAppearance.BorderSize < 2)
                    b.BackColor = Color.FromArgb(11, 15, 30);
            };
            btn.MouseLeave += (s, e) => {
                var b = (Button)s;
                if (b.FlatAppearance.BorderSize < 2)
                    b.BackColor = Color.FromArgb(7, 9, 20);
            };
            return btn;
        }

        private static void SetSelected(Button btn, bool selected)
        {
            btn.BackColor                  = selected ? Color.FromArgb(18, 32, 58) : Color.FromArgb(7, 9, 20);
            btn.FlatAppearance.BorderSize  = selected ? 2 : 1;
            var c = btn.ForeColor;
            btn.FlatAppearance.BorderColor = selected
                ? c : Color.FromArgb(48, c.R, c.G, c.B);
        }

        // ── Background paint ──────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Deep-space gradient
            using (var bg = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(3, 5, 16), Color.FromArgb(5, 4, 20),
                LinearGradientMode.Vertical))
                g.FillRectangle(bg, ClientRectangle);

            // Animated star field — pre-allocated, each star twinkles at its own speed
            for (int i = 0; i < stars.Length; i++)
            {
                float speed  = 0.55f + (i % 7) * 0.09f;
                float blink  = 0.28f + 0.72f * (float)Math.Abs(
                                   Math.Sin(animPhase * speed + stars[i].Phase));
                int   br     = (int)(45 + 175 * blink);
                float sz     = stars[i].Sz * (0.7f + 0.3f * blink);
                using (var brush = new SolidBrush(
                    Color.FromArgb(br, br, Math.Min(255, br + 18))))
                    g.FillEllipse(brush,
                        stars[i].X - sz * 0.5f, stars[i].Y - sz * 0.5f, sz, sz);
            }

            // Soft radial glow behind the title
            using (var glow = new PathGradientBrush(new[]
            {
                new PointF(CX - 230f, 18f), new PointF(CX + 230f, 18f),
                new PointF(CX + 230f, 108f), new PointF(CX - 230f, 108f)
            }))
            {
                glow.CenterPoint    = new PointF(CX, 62f);
                glow.CenterColor    = Color.FromArgb(20, 0, 175, 255);
                glow.SurroundColors = new[] {
                    Color.Transparent, Color.Transparent,
                    Color.Transparent, Color.Transparent };
                g.FillEllipse(glow, CX - 230, 18, 460, 90);
            }

            // Thin content-width dividers under each section heading
            using (var pen = new Pen(Color.FromArgb(20, 42, 76), 1))
            {
                g.DrawLine(pen, CL, 108, CL + CW, 108);  // below subtitle
                g.DrawLine(pen, CL, 197, CL + CW, 197);  // below opponent count
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
