using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FleetCommand
{
    /// <summary>
    /// Overlay panel opened from the mothership — shows all ship types,
    /// their current upgrade level, cost and time for the next level,
    /// and a button to start research.
    ///
    /// Performance note: no polling timer. The panel refreshes its ship rows
    /// only when it becomes visible or the user clicks a button. The active
    /// research progress bar is updated by a lightweight call from GameForm.UpdateUI.
    /// </summary>
    public class ResearchPanel : Panel
    {
        public event Action<ShipType> ResearchRequested;

        private readonly GameWorld          world;
        private readonly List<ResearchRow>  rows = new List<ResearchRow>();
        private ProgressBar activeBar;
        private Label       activeLabel;
        private Label       titleLabel;

        private static readonly ShipType[] Researchable =
        {
            ShipType.Miner,
            ShipType.Interceptor,
            ShipType.Bomber,
            ShipType.Corvet,
            ShipType.Frigate,
            ShipType.Destroyer,
            ShipType.Battlecruiser,
        };

        public ResearchPanel(GameWorld world)
        {
            this.world = world;

            BackColor   = Color.FromArgb(230, 8, 12, 28);
            BorderStyle = BorderStyle.FixedSingle;

            BuildLayout();

            // Full refresh once when panel is shown; no polling timer.
            VisibleChanged += (s, e) => { if (Visible) RefreshState(); };
        }

        private void BuildLayout()
        {
            int x = 10, y = 10, w = 310;

            titleLabel = new Label
            {
                Text      = "⚗  RESEARCH LAB",
                ForeColor = Color.Plum,
                Font      = new Font("Arial", 12, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(x, y)
            };
            Controls.Add(titleLabel);
            y += 30;

            Controls.Add(new Label
            {
                Text      = "Each upgrade: +20% hull  ·  +12% speed  ·  +10% damage",
                ForeColor = Color.DimGray,
                Font      = new Font("Consolas", 7),
                AutoSize  = false,
                Size      = new Size(w, 14),
                Location  = new Point(x, y)
            });
            y += 18;

            Controls.Add(new Label
            {
                Text      = $"{"Ship",-13}{"Level",-8}{"Cost",-8}{"Time",-8}",
                ForeColor = Color.Gray,
                Font      = new Font("Consolas", 8, FontStyle.Bold),
                AutoSize  = false,
                Size      = new Size(w, 14),
                Location  = new Point(x, y)
            });
            y += 16;

            foreach (var type in Researchable)
            {
                var row = new ResearchRow(type, w, x, y, world, this);
                rows.Add(row);
                foreach (Control c in row.Controls) Controls.Add(c);
                y += row.Height + 4;
            }
            y += 6;

            Controls.Add(new Label
            {
                Text      = "── Active Research ──",
                ForeColor = Color.DimGray,
                Font      = new Font("Arial", 8),
                AutoSize  = true,
                Location  = new Point(x, y)
            });
            y += 18;

            activeLabel = new Label
            {
                Text      = "Idle",
                ForeColor = Color.Plum,
                Font      = new Font("Consolas", 8),
                AutoSize  = false,
                Size      = new Size(w, 14),
                Location  = new Point(x, y)
            };
            Controls.Add(activeLabel);
            y += 16;

            activeBar = new ProgressBar
            {
                Location = new Point(x, y),
                Size     = new Size(w, 14),
                Minimum  = 0,
                Maximum  = 100,
                Value    = 0
            };
            Controls.Add(activeBar);
            y += 20;

            var closeBtn = new Button
            {
                Text      = "✕  Close",
                Location  = new Point(x, y),
                Size      = new Size(w, 28),
                BackColor = Color.FromArgb(60, 20, 20),
                ForeColor = Color.OrangeRed,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 8),
            };
            closeBtn.FlatAppearance.BorderColor = Color.DarkRed;
            closeBtn.Click += (s, e) => Hide();
            Controls.Add(closeBtn);
            y += 34;

            Width  = w + 20;
            Height = y + 10;
        }

        // Called by ResearchRow button clicks — full refresh after interaction.
        internal void RequestResearch(ShipType type)
        {
            ResearchRequested?.Invoke(type);
            RefreshState();
        }

        /// <summary>
        /// Called every game tick by GameForm.UpdateUI.
        /// Updates ONLY the active-research progress bar and countdown — no
        /// WinForms repaints on the 7 ship rows, so no lag during gameplay.
        /// </summary>
        public void UpdateProgress()
        {
            if (!Visible) return;

            var active = world.Research.ActiveOrder;
            if (active != null)
            {
                int secLeft      = Math.Max(0, (active.TotalMs - active.Elapsed) / 1000);
                activeLabel.Text = $"{active.Type}  →  Mk.{active.Level}   ({secLeft}s left)";
                activeBar.Value  = (int)(active.Progress * 100);
            }
            else
            {
                // Research just finished — refresh ship rows once to show new level
                if (activeBar.Value > 0)
                {
                    activeLabel.Text = "Idle — select a ship to begin";
                    activeBar.Value  = 0;
                    RefreshState();
                }
            }
        }

        /// <summary>Full refresh: updates all ship rows + the progress section.</summary>
        public void RefreshState()
        {
            foreach (var row in rows)
                row.Refresh(world);

            var active = world.Research.ActiveOrder;
            if (active != null)
            {
                int secLeft      = Math.Max(0, (active.TotalMs - active.Elapsed) / 1000);
                activeLabel.Text = $"{active.Type}  →  Mk.{active.Level}   ({secLeft}s left)";
                activeBar.Value  = (int)(active.Progress * 100);
            }
            else
            {
                activeLabel.Text = "Idle — select a ship to begin";
                activeBar.Value  = 0;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(120, Color.Plum), 1.5f))
                e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        }
    }

    // ── One row in the research table ─────────────────────────────────────────
    internal class ResearchRow
    {
        public int Height => 26;

        private readonly ShipType type;
        private readonly Label    levelLbl;
        private readonly Label    costLbl;
        private readonly Label    timeLbl;
        private readonly Button   resBtn;

        public IEnumerable<Control> Controls
        {
            get
            {
                yield return levelLbl;
                yield return costLbl;
                yield return timeLbl;
                yield return resBtn;
            }
        }

        public ResearchRow(ShipType type, int totalW, int x, int y, GameWorld world, ResearchPanel panel)
        {
            this.type = type;

            int col0 = x,       w0 = 90;
            int col1 = x + 95,  w1 = 60;
            int col2 = x + 158, w2 = 50;
            int col3 = x + 212, w3 = 108;

            levelLbl = new Label
            {
                Font      = new Font("Consolas", 8),
                AutoSize  = false,
                Size      = new Size(w0, Height),
                Location  = new Point(col0, y),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            costLbl = new Label
            {
                Font      = new Font("Consolas", 8),
                AutoSize  = false,
                Size      = new Size(w1, Height),
                Location  = new Point(col1, y),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            timeLbl = new Label
            {
                Font      = new Font("Consolas", 8),
                AutoSize  = false,
                Size      = new Size(w2, Height),
                Location  = new Point(col2, y),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            resBtn = new Button
            {
                Location  = new Point(col3, y),
                Size      = new Size(w3, Height),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 7, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            resBtn.FlatAppearance.BorderSize = 1;
            resBtn.Click += (s, e) => panel.RequestResearch(type);

            Refresh(world);
        }

        public void Refresh(GameWorld world)
        {
            var  rm        = world.Research;
            int  level     = rm.Levels[type];
            bool maxed     = level >= 3;
            bool busy      = rm.ActiveOrder != null;
            bool myTurn    = rm.IsResearching(type);
            bool canAfford = !maxed && !busy && world.PlayerResources >= rm.CostFor(type);

            string levelStr    = level == 0 ? "Base" : $"Mk.{level}";
            levelLbl.Text      = $"{type,-11} {levelStr}";
            levelLbl.ForeColor = level == 3 ? Color.Gold
                               : level == 2 ? Color.LightBlue
                               : level == 1 ? Color.LightGreen
                               : Color.LightGray;

            if (maxed)
            {
                costLbl.Text      = "MAX";
                costLbl.ForeColor = Color.Gold;
            }
            else
            {
                int cost          = rm.CostFor(type);
                costLbl.Text      = $"{cost} res";
                costLbl.ForeColor = canAfford ? Color.LightGreen : Color.IndianRed;
            }

            if (maxed)
            {
                timeLbl.Text      = "──";
                timeLbl.ForeColor = Color.DimGray;
            }
            else if (myTurn)
            {
                int secLeft       = Math.Max(0, (rm.ActiveOrder.TotalMs - rm.ActiveOrder.Elapsed) / 1000);
                timeLbl.Text      = $"{secLeft}s";
                timeLbl.ForeColor = Color.Plum;
            }
            else
            {
                timeLbl.Text      = $"{rm.DurationFor(type) / 1000}s";
                timeLbl.ForeColor = Color.DimGray;
            }

            if (maxed)
            {
                resBtn.Text      = "✓ MAX";
                resBtn.Enabled   = false;
                resBtn.ForeColor = Color.Gold;
                resBtn.BackColor = Color.FromArgb(30, 25, 10);
                resBtn.FlatAppearance.BorderColor = Color.DarkGoldenrod;
            }
            else if (myTurn)
            {
                resBtn.Text      = "Researching…";
                resBtn.Enabled   = false;
                resBtn.ForeColor = Color.Plum;
                resBtn.BackColor = Color.FromArgb(25, 10, 35);
                resBtn.FlatAppearance.BorderColor = Color.MediumPurple;
            }
            else if (busy)
            {
                resBtn.Text      = "Lab busy";
                resBtn.Enabled   = false;
                resBtn.ForeColor = Color.DimGray;
                resBtn.BackColor = Color.FromArgb(20, 20, 30);
                resBtn.FlatAppearance.BorderColor = Color.DimGray;
            }
            else
            {
                string nextLbl   = level == 0 ? "→ Mk.I" : level == 1 ? "→ Mk.II" : "→ Mk.III";
                resBtn.Text      = canAfford ? nextLbl : "Need res";
                resBtn.Enabled   = canAfford;
                resBtn.ForeColor = canAfford ? Color.Cyan     : Color.DimGray;
                resBtn.BackColor = canAfford ? Color.FromArgb(15, 35, 50) : Color.FromArgb(20, 20, 30);
                resBtn.FlatAppearance.BorderColor = canAfford ? Color.CadetBlue : Color.DimGray;
            }
        }
    }
}
