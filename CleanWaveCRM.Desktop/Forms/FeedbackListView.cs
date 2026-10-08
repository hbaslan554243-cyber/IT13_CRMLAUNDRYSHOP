using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class FeedbackListView : UserControl
    {
        private readonly FeedbackController _controller;
        private readonly LaundryShopDbContext _db;
        private readonly DataGridView _grid = new();
        private readonly ComboBox _cmbStatus = new();

        public FeedbackListView(LaundryShopDbContext db)
        {
            _db = db;
            _controller = new FeedbackController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var top = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var lblTitle = new Label { Text = "Feedback & Complaints", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };

            _cmbStatus.Location = new Point(270, 12);
            _cmbStatus.Width = 150;
            _cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbStatus.Items.Add("All");
            _cmbStatus.Items.AddRange(Enum.GetNames(typeof(FeedbackStatus)));
            _cmbStatus.SelectedIndex = 0;
            _cmbStatus.SelectedIndexChanged += (_, _) => LoadData();

            var btnNew = new Button { Text = "+ Log Feedback", Location = new Point(440, 10), Width = 150, Height = 30, BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnNew.Click += (_, _) => OpenCreate();

            var btnOpen = new Button { Text = "Open Selected", Location = new Point(600, 10), Width = 130, Height = 30, FlatStyle = FlatStyle.Flat };
            btnOpen.Click += (_, _) => OpenResolve();

            top.Controls.AddRange(new Control[] { lblTitle, _cmbStatus, btnNew, btnOpen });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += (_, _) => OpenResolve();

            Controls.Add(_grid);
            Controls.Add(top);
        }

        private void LoadData()
        {
            var list = _cmbStatus.SelectedIndex == 0
                ? _controller.GetAll()
                : _controller.GetByStatus(Enum.Parse<FeedbackStatus>(_cmbStatus.SelectedItem!.ToString()!));

            _grid.DataSource = list.Select(f => new
            {
                f.Id,
                Customer = f.Customer?.FullName ?? "(unknown)",
                f.Type,
                f.Status,
                Rating = f.Rating.HasValue ? new string('\u2605', f.Rating.Value) + new string('\u2606', 5 - f.Rating.Value) : "-",
                Message = f.Message.Length > 60 ? f.Message[..60] + "..." : f.Message,
                Logged = f.CreatedAt.ToLocalTime().ToString("g")
            }).ToList();
        }

        private void OpenCreate()
        {
            using var form = new FeedbackCreateForm(_db);
            if (form.ShowDialog() == DialogResult.OK) LoadData();
        }

        private void OpenResolve()
        {
            if (_grid.CurrentRow == null) return;
            var id = Convert.ToInt32(_grid.CurrentRow.Cells["Id"].Value);

            using var form = new FeedbackResolveForm(_db, id);
            form.ShowDialog();
            LoadData();
        }
    }
}
