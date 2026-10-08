using CRM.api.Controllers;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class PromotionListView : UserControl
    {
        private readonly PromotionController _controller;
        private readonly LaundryShopDbContext _db;
        private readonly DataGridView _grid = new();
        private readonly Label _lblMessage = new();

        public PromotionListView(LaundryShopDbContext db)
        {
            _db = db;
            _controller = new PromotionController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var top = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var lblTitle = new Label { Text = "Targeted Promotions", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };

            var btnNew = new Button { Text = "+ New Promotion", Location = new Point(250, 10), Width = 160, Height = 30, BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnNew.Click += (_, _) => OpenCreate();

            var btnSend = new Button { Text = "Send Selected", Location = new Point(420, 10), Width = 140, Height = 30, BackColor = Color.SteelBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnSend.Click += (_, _) => SendSelected();

            _lblMessage.Location = new Point(570, 15);
            _lblMessage.AutoSize = true;
            _lblMessage.ForeColor = Color.SeaGreen;

            top.Controls.AddRange(new Control[] { lblTitle, btnNew, btnSend, _lblMessage });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(_grid);
            Controls.Add(top);
        }

        private void LoadData()
        {
            var promos = _controller.GetAll();
            _grid.DataSource = promos.Select(p => new
            {
                p.Id,
                p.Title,
                Target = p.TargetCustomer != null ? p.TargetCustomer.FullName
                    : p.TargetSegment.HasValue ? p.TargetSegment.Value.ToString() : "Everyone",
                Reason = p.Reason.HasValue ? p.Reason.Value.ToString() : "-",
                p.DiscountType,
                p.DiscountValue,
                Valid = $"{p.StartDate:d} - {p.EndDate:d}",
                p.IsActive
            }).ToList();
        }

        private void OpenCreate()
        {
            using var form = new PromotionCreateForm(_db);
            if (form.ShowDialog() == DialogResult.OK) LoadData();
        }

        private void SendSelected()
        {
            if (_grid.CurrentRow == null) return;
            var id = Convert.ToInt32(_grid.CurrentRow.Cells["Id"].Value);
            var result = _controller.SendPromotion(id);
            _lblMessage.Text = result.Message;
        }
    }
}
