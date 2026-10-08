using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    /// <summary>
    /// Admin/Owner's "Manage Users" screen. Only ever creates Front Desk
    /// Staff and Manager accounts - Admin/SuperAdmin accounts are handled
    /// separately (see SuperAdminHomeView). Which roles are offered here
    /// depends on what this shop has chosen to avail in Shop Profile
    /// (AllowStaffAccounts / AllowManagerAccounts) - a one-counter shop
    /// where the Owner does everything might turn both off.
    /// </summary>
    public class UserManagementView : UserControl
    {
        private readonly UserController _controller;
        private readonly SettingsController _settingsController;
        private readonly LaundryShopDbContext _db;
        private readonly DataGridView _grid = new();
        private readonly Label _lblMessage = new();

        public UserManagementView(LaundryShopDbContext db)
        {
            _db = db;
            _controller = new UserController(db);
            _settingsController = new SettingsController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var top = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var lblTitle = new Label { Text = "Manage Users", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };

            var btnNew = new Button { Text = "+ New Staff/Manager", Location = new Point(200, 10), Width = 170, Height = 30, BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnNew.Click += (_, _) => OpenCreate();

            var btnToggle = new Button { Text = "Toggle Active", Location = new Point(380, 10), Width = 130, Height = 30, FlatStyle = FlatStyle.Flat };
            btnToggle.Click += (_, _) => ToggleActive();

            var btnEdit = new Button { Text = "Edit Selected", Location = new Point(520, 10), Width = 110, Height = 30, FlatStyle = FlatStyle.Flat };
            btnEdit.Click += (_, _) => EditSelected();
            var btnRemove = new Button { Text = "Remove Selected", Location = new Point(640, 10), Width = 125, Height = 30, FlatStyle = FlatStyle.Flat };
            btnRemove.Click += (_, _) => RemoveSelected();

            _lblMessage.Location = new Point(780, 15);
            _lblMessage.AutoSize = true;
            _lblMessage.ForeColor = Color.SeaGreen;

            top.Controls.AddRange(new Control[] { lblTitle, btnNew, btnToggle, btnEdit, btnRemove, _lblMessage });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(_grid);
            Controls.Add(top);
        }

        private void LoadData()
        {
            // Only this company's own Staff/Manager - other shops' accounts are invisible here.
            var users = _controller.GetForMyCompany()
                .Where(u => u.Role is UserRole.FrontDeskStaff or UserRole.Manager)
                .ToList();

            _grid.DataSource = users.Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Role,
                Status = u.IsActive ? "Active" : "Inactive",
                LastLogin = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToLocalTime().ToString("g") : "-"
            }).ToList();
        }

        private void OpenCreate()
        {
            var company = _settingsController.GetMyCompany();
            var allowedRoles = new List<UserRole>();
            if (company.AllowStaffAccounts) allowedRoles.Add(UserRole.FrontDeskStaff);
            if (company.AllowManagerAccounts) allowedRoles.Add(UserRole.Manager);

            if (allowedRoles.Count == 0)
            {
                MessageBox.Show(
                    "Staff and Manager accounts are both turned off in Shop Profile. Enable at least one there first.",
                    "Nothing to Create", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new UserCreateForm(_db, allowedRoles);
            if (form.ShowDialog() == DialogResult.OK) LoadData();
        }

        private void ToggleActive()
        {
            if (_grid.CurrentRow == null) return;
            var id = _grid.CurrentRow.Cells["Id"].Value?.ToString();
            var status = _grid.CurrentRow.Cells["Status"].Value?.ToString();
            if (id == null) return;

            var result = _controller.SetActive(id, status != "Active");
            _lblMessage.Text = result.Message;
            LoadData();
        }

        private void EditSelected()
        {
            if (_grid.CurrentRow == null) return;
            var id = _grid.CurrentRow.Cells["Id"].Value?.ToString();
            if (string.IsNullOrWhiteSpace(id)) return;
            var user = _controller.GetForMyCompany().FirstOrDefault(u => u.Id == id);
            if (user == null || user.Role is not (UserRole.FrontDeskStaff or UserRole.Manager)) return;

            var company = _settingsController.GetMyCompany();
            var roles = new List<UserRole>();
            if (company.AllowStaffAccounts) roles.Add(UserRole.FrontDeskStaff);
            if (company.AllowManagerAccounts) roles.Add(UserRole.Manager);
            if (roles.Count == 0) return;

            using var dialog = new Form { Text = "Edit Staff / Manager", Width = 400, Height = 235, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var nameLabel = new Label { Text = "Full name", Left = 15, Top = 18, AutoSize = true };
            var name = new TextBox { Left = 120, Top = 15, Width = 240, Text = user.FullName };
            var emailLabel = new Label { Text = "Email", Left = 15, Top = 55, AutoSize = true };
            var email = new TextBox { Left = 120, Top = 52, Width = 240, Text = user.Email ?? string.Empty };
            var roleLabel = new Label { Text = "Role", Left = 15, Top = 92, AutoSize = true };
            var role = new ComboBox { Left = 120, Top = 89, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            role.Items.AddRange(roles.Cast<object>().ToArray());
            role.SelectedItem = roles.Contains(user.Role) ? user.Role : roles[0];
            var save = new Button { Text = "Save", Left = 275, Top = 135, Width = 85, DialogResult = DialogResult.OK };
            dialog.Controls.AddRange(new Control[] { nameLabel, name, emailLabel, email, roleLabel, role, save });
            dialog.AcceptButton = save;
            if (dialog.ShowDialog(this) != DialogResult.OK || role.SelectedItem is not UserRole selectedRole) return;

            var result = _controller.UpdateStaffProfile(id, name.Text, email.Text, selectedRole);
            _lblMessage.Text = result.Message;
            LoadData();
        }

        private void RemoveSelected()
        {
            if (_grid.CurrentRow == null) return;
            var id = _grid.CurrentRow.Cells["Id"].Value?.ToString();
            var name = _grid.CurrentRow.Cells["FullName"].Value?.ToString();
            if (string.IsNullOrWhiteSpace(id)) return;
            if (MessageBox.Show($"Deactivate {name ?? "this user"}? Their account will no longer be able to sign in, and their history will be retained.",
                "Remove User", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var result = _controller.SetActive(id, false);
            _lblMessage.Text = result.Success ? "User removed from active staff. History retained." : result.Message;
            LoadData();
        }
    }
}
