using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class SegmentationView : UserControl
    {
        private readonly SegmentationController _controller;
        private readonly ComboBox _cmbSegment = new();
        private readonly DataGridView _grid = new();
        private readonly Label _lblMessage = new();

        public SegmentationView(LaundryShopDbContext db)
        {
            _controller = new SegmentationController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var top = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var lblTitle = new Label { Text = "Customer Segmentation", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };

            _cmbSegment.Location = new Point(240, 12);
            _cmbSegment.Width = 160;
            _cmbSegment.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbSegment.Items.Add("All Segments");
            _cmbSegment.Items.AddRange(Enum.GetNames(typeof(CustomerSegment)));
            _cmbSegment.SelectedIndex = 0;
            _cmbSegment.SelectedIndexChanged += (_, _) => LoadData();

            var btnRecalc = new Button
            {
                Text = "Recalculate Segments",
                Location = new Point(420, 10),
                Width = 180,
                Height = 30,
                BackColor = Color.SteelBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRecalc.Click += (_, _) => Recalculate();

            _lblMessage.Location = new Point(610, 15);
            _lblMessage.AutoSize = true;
            _lblMessage.ForeColor = Color.SeaGreen;

            top.Controls.AddRange(new Control[] { lblTitle, _cmbSegment, btnRecalc, _lblMessage });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(_grid);
            Controls.Add(top);
        }

        private void Recalculate()
        {
            var changed = _controller.RecalculateAllSegments();
            _lblMessage.Text = $"Updated {changed} customer(s).";
            LoadData();
        }

        private void LoadData()
        {
            var grouped = _controller.GetCustomersBySegment();
            List<Customer> rows;

            if (_cmbSegment.SelectedIndex == 0)
            {
                rows = grouped.Values.SelectMany(x => x).OrderBy(c => c.Segment).ThenBy(c => c.FullName).ToList();
            }
            else
            {
                var seg = Enum.Parse<CustomerSegment>(_cmbSegment.SelectedItem!.ToString()!);
                rows = grouped.TryGetValue(seg, out var list) ? list : new List<Customer>();
            }

            _grid.DataSource = rows.Select(c => new
            {
                c.FullName,
                c.ContactNumber,
                Segment = c.Segment.ToString(),
                c.TotalOrders,
                c.TotalSpend,
                LastOrder = c.LastOrderDate.HasValue ? c.LastOrderDate.Value.ToLocalTime().ToString("d") : "-"
            }).ToList();
        }
    }
}
