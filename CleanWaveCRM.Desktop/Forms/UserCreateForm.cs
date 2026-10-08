using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class UserCreateForm : Form
    {
        private readonly AuthController _authController;

        private readonly TextBox _txtFullName = new();
        private readonly TextBox _txtEmail = new();
        private readonly TextBox _txtPassword = new();
        private readonly TextBox _txtTenantName = new();
        private readonly ComboBox _cmbRole = new();
        private readonly Label _lblStatus = new();

        /// <param name="allowedRoles">Which roles the dropdown offers. Defaults to Staff + Manager (the normal "Admin creates staff" case).</param>
        public UserCreateForm(LaundryShopDbContext db, IEnumerable<UserRole>? allowedRoles = null)
        {
            _authController = new AuthController(db);
            BuildUi((allowedRoles ?? new[] { UserRole.FrontDeskStaff, UserRole.Manager }).ToArray());
        }

        private void BuildUi(UserRole[] allowedRoles)
        {
            var creatingTenant = allowedRoles.Contains(UserRole.AdminOwner);
            Text = creatingTenant ? "New Tenant Owner Account" : "New Staff Account";
            Width = 400;
            Height = 330;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblName = new Label { Text = "Full Name*", Left = 20, Top = 20, AutoSize = true };
            _txtFullName.Left = 150; _txtFullName.Top = 18; _txtFullName.Width = 210;

            var lblEmail = new Label { Text = "Email*", Left = 20, Top = 55, AutoSize = true };
            _txtEmail.Left = 150; _txtEmail.Top = 53; _txtEmail.Width = 210;

            var lblPassword = new Label { Text = "Password*", Left = 20, Top = 90, AutoSize = true };
            _txtPassword.Left = 150; _txtPassword.Top = 88; _txtPassword.Width = 210; _txtPassword.PasswordChar = '*';

            var lblRole = new Label { Text = "Role", Left = 20, Top = 125, AutoSize = true };
            _cmbRole.Left = 150; _cmbRole.Top = 123; _cmbRole.Width = 210;
            _cmbRole.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbRole.Items.AddRange(allowedRoles.Select(r => r.ToString()).ToArray());
            _cmbRole.SelectedIndex = 0;

            var lblTenant = new Label { Text = "Tenant name", Left = 20, Top = 160, AutoSize = true, Visible = creatingTenant };
            _txtTenantName.Left = 150; _txtTenantName.Top = 158; _txtTenantName.Width = 210; _txtTenantName.Visible = creatingTenant;
            _txtTenantName.PlaceholderText = "For example, Tenant A";

            var btnSave = new Button { Text = "Create Account", Left = 20, Top = 195, Width = 160, Height = 35, BackColor = Color.SeaGreen, ForeColor = Color.White };
            btnSave.Click += BtnSave_Click;
            var btnCancel = new Button { Text = "Cancel", Left = 190, Top = 195, Width = 90, Height = 35 };
            btnCancel.Click += (_, _) => Close();

            _lblStatus.Left = 20; _lblStatus.Top = 240; _lblStatus.Width = 340; _lblStatus.ForeColor = Color.Firebrick;

            Controls.AddRange(new Control[] { lblName, _txtFullName, lblEmail, _txtEmail, lblPassword, _txtPassword, lblRole, _cmbRole, lblTenant, _txtTenantName, btnSave, btnCancel, _lblStatus });
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var role = Enum.Parse<UserRole>(_cmbRole.SelectedItem!.ToString()!);
            var result = _authController.CreateUser(_txtEmail.Text, _txtPassword.Text, _txtFullName.Text, role, _txtTenantName.Text);

            if (!result.Success) { _lblStatus.Text = result.Message; return; }

            MessageBox.Show(result.Message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
