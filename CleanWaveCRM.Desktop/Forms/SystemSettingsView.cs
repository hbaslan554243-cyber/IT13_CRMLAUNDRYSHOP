using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class SystemSettingsView : UserControl
    {
        private readonly SettingsController _controller;
        private readonly bool _platformManaged;
        private Company _settings = null!;

        private readonly TextBox _txtShopName = new();
        private readonly TextBox _txtAddress = new();
        private readonly TextBox _txtContact = new();
        private readonly CheckBox _chkAllowStaff = new();
        private readonly CheckBox _chkAllowManager = new();
        private readonly NumericUpDown _numFrequentThreshold = new();
        private readonly NumericUpDown _numInactiveDays = new();
        private readonly NumericUpDown _numHighValueSpend = new();
        private readonly NumericUpDown _numPesosPerPoint = new();
        private readonly TextBox _txtTerms = new();
        private readonly Label _lblStatus = new();

        public SystemSettingsView(LaundryShopDbContext db, bool platformManaged = false)
        {
            _controller = new SettingsController(db);
            _platformManaged = platformManaged;
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;

            var lblTitle = new Label { Text = "Shop Profile", Font = new Font("Segoe UI", 14, FontStyle.Bold), Left = 20, Top = 20, AutoSize = true };

            int y = 60;
            var lblName = new Label { Text = "Shop Name", Left = 20, Top = y, AutoSize = true };
            _txtShopName.Left = 220; _txtShopName.Top = y - 3; _txtShopName.Width = 300;
            y += 35;

            var lblAddress = new Label { Text = "Address", Left = 20, Top = y, AutoSize = true };
            _txtAddress.Left = 220; _txtAddress.Top = y - 3; _txtAddress.Width = 300;
            y += 35;

            var lblContact = new Label { Text = "Contact Number", Left = 20, Top = y, AutoSize = true };
            _txtContact.Left = 220; _txtContact.Top = y - 3; _txtContact.Width = 200;
            y += 45;

            var lblRolesHeader = new Label { Text = "What This Shop Avails", Left = 20, Top = y, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            y += 30;

            _chkAllowStaff.Text = "Front Desk Staff accounts";
            _chkAllowStaff.Left = 20; _chkAllowStaff.Top = y; _chkAllowStaff.AutoSize = true;
            y += 28;

            _chkAllowManager.Text = "Manager accounts (also unlocks Segmentation)";
            _chkAllowManager.Left = 20; _chkAllowManager.Top = y; _chkAllowManager.AutoSize = true;
            y += 40;

            var lblSegHeader = new Label { Text = "Segmentation Rules", Left = 20, Top = y, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            y += 30;

            var lblFreq = new Label { Text = "Frequent = at least this many orders", Left = 20, Top = y, AutoSize = true };
            _numFrequentThreshold.Left = 320; _numFrequentThreshold.Top = y - 3; _numFrequentThreshold.Width = 80; _numFrequentThreshold.Maximum = 1000;
            y += 35;

            var lblInactive = new Label { Text = "Inactive = no orders in this many days", Left = 20, Top = y, AutoSize = true };
            _numInactiveDays.Left = 320; _numInactiveDays.Top = y - 3; _numInactiveDays.Width = 80; _numInactiveDays.Maximum = 3650;
            y += 35;

            var lblHighValue = new Label { Text = "High Value = total spend at least", Left = 20, Top = y, AutoSize = true };
            _numHighValueSpend.Left = 320; _numHighValueSpend.Top = y - 3; _numHighValueSpend.Width = 100; _numHighValueSpend.Maximum = 10000000; _numHighValueSpend.DecimalPlaces = 2;
            y += 45;

            var lblLoyaltyHeader = new Label { Text = "Loyalty Configuration", Left = 20, Top = y, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            y += 30;

            var lblPesos = new Label { Text = "Pesos per 1 loyalty point", Left = 20, Top = y, AutoSize = true };
            _numPesosPerPoint.Left = 320; _numPesosPerPoint.Top = y - 3; _numPesosPerPoint.Width = 100; _numPesosPerPoint.Maximum = 100000; _numPesosPerPoint.DecimalPlaces = 2;
            y += 45;

            var lblTerms = new Label { Text = "Terms & Conditions", Left = 20, Top = y, AutoSize = true };
            _txtTerms.Left = 20; _txtTerms.Top = y + 20; _txtTerms.Width = 480; _txtTerms.Height = 80; _txtTerms.Multiline = true; _txtTerms.MaxLength = 2000;
            if (_platformManaged)
            {
                lblTerms.Text += " (set by Super Admin)";
                _txtTerms.ReadOnly = true;
                _chkAllowStaff.Enabled = false;
                _chkAllowManager.Enabled = false;
            }
            y += 110;

            var btnSave = new Button { Text = "Save Settings", Left = 20, Top = y, Width = 160, Height = 35, BackColor = Color.SeaGreen, ForeColor = Color.White };
            btnSave.Click += (_, _) => Save();

            _lblStatus.Left = 200; _lblStatus.Top = y + 8; _lblStatus.AutoSize = true; _lblStatus.ForeColor = Color.SeaGreen;

            Controls.AddRange(new Control[]
            {
                lblTitle, lblName, _txtShopName, lblAddress, _txtAddress, lblContact, _txtContact,
                lblRolesHeader, _chkAllowStaff, _chkAllowManager,
                lblSegHeader, lblFreq, _numFrequentThreshold, lblInactive, _numInactiveDays, lblHighValue, _numHighValueSpend,
                lblLoyaltyHeader, lblPesos, _numPesosPerPoint, lblTerms, _txtTerms, btnSave, _lblStatus
            });
        }

        private void LoadData()
        {
            _settings = _controller.GetMyCompany();
            _txtShopName.Text = _settings.ShopName;
            _txtAddress.Text = _settings.Address;
            _txtContact.Text = _settings.ContactNumber;
            _chkAllowStaff.Checked = _settings.AllowStaffAccounts;
            _chkAllowManager.Checked = _settings.AllowManagerAccounts;
            _numFrequentThreshold.Value = _settings.FrequentOrderThreshold;
            _numInactiveDays.Value = _settings.InactiveDaysThreshold;
            _numHighValueSpend.Value = _settings.HighValueSpendThreshold;
            _numPesosPerPoint.Value = _settings.PesosPerPoint;
            _txtTerms.Text = _settings.TermsAndConditions;
        }

        private void Save()
        {
            _settings.ShopName = _txtShopName.Text.Trim();
            _settings.Address = _txtAddress.Text;
            _settings.ContactNumber = _txtContact.Text;
            _settings.AllowStaffAccounts = _chkAllowStaff.Checked;
            _settings.AllowManagerAccounts = _chkAllowManager.Checked;
            _settings.FrequentOrderThreshold = (int)_numFrequentThreshold.Value;
            _settings.InactiveDaysThreshold = (int)_numInactiveDays.Value;
            _settings.HighValueSpendThreshold = _numHighValueSpend.Value;
            _settings.PesosPerPoint = _numPesosPerPoint.Value;
            _settings.TermsAndConditions = _txtTerms.Text;

            var result = _controller.UpdateSettings(_settings);
            _lblStatus.ForeColor = result.Success ? Color.SeaGreen : Color.Firebrick;
            _lblStatus.Text = result.Message + " Reopen a page from the menu to see role changes take effect.";
        }
    }
}
