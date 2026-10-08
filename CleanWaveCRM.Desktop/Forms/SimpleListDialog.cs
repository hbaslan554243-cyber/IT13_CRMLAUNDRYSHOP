namespace CleanWaveCRM.Desktop.Forms
{
    /// <summary>
    /// Generic "here's the basis of that number" dialog. Every clickable
    /// KPI/stat card on the Dashboard opens one of these: the headline
    /// value, the date/time period used, the formula/calculation, and the
    /// actual database records that produced it. Nothing here is
    /// hardcoded - callers pass in live-computed values every time.
    /// </summary>
    public class SimpleListDialog : Form
    {
        /// <param name="title">Dialog title and heading, e.g. "Today's Orders".</param>
        /// <param name="headlineValue">The KPI value itself, shown large, e.g. "3" or "\u20b1732.57".</param>
        /// <param name="period">The date/time period this covers, e.g. "September 24, 2026" or "Last 30 days".</param>
        /// <param name="formula">The calculation/formula used, e.g. "Total Valid Revenue \u00f7 Number of Valid Orders".</param>
        /// <param name="dataSource">The actual records list backing this number.</param>
        /// <param name="emptyMessage">Shown instead of an empty grid when there are no records.</param>
        public SimpleListDialog(string title, string headlineValue, string period, string formula, object dataSource, string? emptyMessage = null)
        {
            Text = title;
            Width = 680;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            BackColor = UiTheme.Background;

            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 8, 0, 0)
            };

            // --- Basis panel: value, period, formula - answers "why does it say this?" ---
            var basisPanel = new Panel { Dock = DockStyle.Top, Height = 95, BackColor = Color.White, Padding = new Padding(15, 8, 15, 8) };

            var lblValue = new Label
            {
                Text = headlineValue,
                Left = 0, Top = 5,
                AutoSize = true,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark
            };

            var lblPeriod = new Label
            {
                Text = $"Period: {period}",
                Left = 0, Top = 50,
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.BodyFont
            };

            var lblFormula = new Label
            {
                Text = $"Formula: {formula}",
                Left = 0, Top = 70,
                AutoSize = true,
                MaximumSize = new Size(630, 0),
                ForeColor = UiTheme.TextMuted,
                Font = new Font(UiTheme.BodyFont, FontStyle.Italic)
            };

            basisPanel.Controls.AddRange(new Control[] { lblValue, lblPeriod, lblFormula });

            var lblRecords = new Label
            {
                Text = "Records used in this calculation:",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 6, 0, 0)
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                DataSource = dataSource
            };

            var btnClose = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 40 };
            btnClose.Click += (_, _) => Close();

            Controls.Add(grid);
            Controls.Add(btnClose);
            Controls.Add(lblRecords);
            Controls.Add(basisPanel);
            Controls.Add(lblTitle);

            // Check the supplied records directly. DataGridView.RowCount can
            // still be zero immediately after assigning its DataSource,
            // before WinForms completes binding, which falsely displays the
            // empty-state message while valid KPI records are present.
            var hasRecords = dataSource is System.Collections.IEnumerable records
                && records.Cast<object>().Any();

            if (!string.IsNullOrEmpty(emptyMessage) && !hasRecords)
            {
                grid.Visible = false;
                Controls.Add(new Label
                {
                    Text = emptyMessage,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Gray
                });
            }
        }
    }
}
