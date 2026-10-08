using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using CleanWaveCRM.Desktop;

namespace CleanWaveCRM.Desktop.Forms;

public sealed class SuperAdminUserManagementView : UserControl
{
    private readonly LaundryShopDbContext _centralDb;
    private readonly IReadOnlyList<TenantDatabaseConfig> _tenants;
    private readonly ComboBox _tenantPicker = new();
    private readonly DataGridView _grid = new();
    private readonly ComboBox _rolePicker = new();
    private readonly Label _message = new();

    public SuperAdminUserManagementView(LaundryShopDbContext centralDb, IReadOnlyList<TenantDatabaseConfig> tenants)
    {
        _centralDb = centralDb;
        _tenants = tenants;
        BuildUi();
        if (_tenants.Count > 0)
        {
            _tenantPicker.SelectedIndex = 0;
            LoadUsers();
        }
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(10) };
        _tenantPicker.Left = 10; _tenantPicker.Top = 10; _tenantPicker.Width = 190;
        _tenantPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _tenantPicker.DisplayMember = nameof(TenantDatabaseConfig.Name);
        _tenantPicker.Items.AddRange(_tenants.Cast<object>().ToArray());
        _tenantPicker.SelectedIndexChanged += (_, _) => LoadUsers();
        var refresh = new Button { Text = "Refresh", Left = 210, Top = 9, Width = 90, Height = 28 };
        refresh.Click += (_, _) => LoadUsers();
        top.Controls.AddRange(new Control[] { _tenantPicker, refresh });

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(8) };
        var reset = new Button { Text = "Reset Password", Left = 8, Top = 10, Width = 120, Height = 28 };
        reset.Click += (_, _) => ResetPassword();
        _rolePicker.Left = 138; _rolePicker.Top = 10; _rolePicker.Width = 145;
        _rolePicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _rolePicker.Items.AddRange(new object[] { UserRole.AdminOwner, UserRole.Manager, UserRole.FrontDeskStaff });
        _rolePicker.SelectedIndex = 0;
        var assign = new Button { Text = "Assign Role", Left = 290, Top = 10, Width = 100, Height = 28 };
        assign.Click += (_, _) => AssignRole();
        var toggle = new Button { Text = "Lock / Unlock", Left = 400, Top = 10, Width = 110, Height = 28 };
        toggle.Click += (_, _) => ToggleLock();
        _message.Left = 525; _message.Top = 15; _message.AutoSize = true;
        bottom.Controls.AddRange(new Control[] { reset, _rolePicker, assign, toggle, _message });

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(top);
    }

    private void LoadUsers()
    {
        if (_tenantPicker.SelectedItem is not TenantDatabaseConfig tenant) return;
        try
        {
            var plan = _centralDb.TenantRegistries.FirstOrDefault(t => t.TenantKey == tenant.Key);
            _rolePicker.Items.Clear();
            _rolePicker.Items.Add(UserRole.AdminOwner);
            if (plan?.AllowManagerAccounts == true) _rolePicker.Items.Add(UserRole.Manager);
            if (plan?.AllowStaffAccounts == true) _rolePicker.Items.Add(UserRole.FrontDeskStaff);
            _rolePicker.SelectedIndex = 0;

            using var db = Program.CreateDbContext(tenant.ConnectionString);
            SeedData.EnsureTenantSchemaColumns(db);
            _grid.DataSource = db.Users
                .Where(u => u.Role != UserRole.SuperAdmin)
                .OrderBy(u => u.FullName)
                .ToList()
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    Role = u.Role.ToString(),
                    Status = u.IsActive ? "Unlocked" : "Locked"
                })
                .ToList();
            _message.Text = $"{tenant.Name}: users loaded.";
        }
        catch (Exception ex)
        {
            _grid.DataSource = null;
            _message.Text = $"Could not load {tenant.Name}: {ex.Message}";
        }
    }

    private string? SelectedUserId() => _grid.CurrentRow?.Cells["Id"].Value?.ToString();

    private void ResetPassword()
    {
        if (!TryGetSelection(out var tenant, out var userId)) return;
        using var dialog = new Form
        {
            Text = "Reset Tenant User Password",
            Width = 390,
            Height = 175,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var label = new Label { Text = "New password (at least 8 characters)", Left = 15, Top = 15, AutoSize = true };
        var password = new TextBox { Left = 15, Top = 42, Width = 340, UseSystemPasswordChar = true };
        var save = new Button { Text = "Reset", Left = 270, Top = 82, Width = 85, DialogResult = DialogResult.OK };
        dialog.Controls.AddRange(new Control[] { label, password, save });
        dialog.AcceptButton = save;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var db = Program.CreateDbContext(tenant.ConnectionString);
            var result = new UserController(db).ResetPassword(userId, password.Text);
            _message.Text = result.Message;
        }
        catch (Exception ex) { _message.Text = $"Password reset failed: {ex.Message}"; }
    }

    private void AssignRole()
    {
        if (!TryGetSelection(out var tenant, out var userId) || _rolePicker.SelectedItem is not UserRole role) return;
        var plan = _centralDb.TenantRegistries.FirstOrDefault(t => t.TenantKey == tenant.Key);
        if (role == UserRole.Manager && plan?.AllowManagerAccounts != true
            || role == UserRole.FrontDeskStaff && plan?.AllowStaffAccounts != true)
        {
            _message.Text = "That role is not included in this tenant's current plan.";
            return;
        }
        try
        {
            using var db = Program.CreateDbContext(tenant.ConnectionString);
            var result = new UserController(db).ChangeRole(userId, role);
            _message.Text = result.Message;
            LoadUsers();
        }
        catch (Exception ex) { _message.Text = $"Role update failed: {ex.Message}"; }
    }

    private void ToggleLock()
    {
        if (!TryGetSelection(out var tenant, out var userId)) return;
        var currentStatus = _grid.CurrentRow!.Cells["Status"].Value?.ToString();
        try
        {
            using var db = Program.CreateDbContext(tenant.ConnectionString);
            var result = new UserController(db).SetActive(userId, currentStatus != "Unlocked");
            _message.Text = result.Success
                ? (currentStatus == "Unlocked" ? "Account locked." : "Account unlocked.")
                : result.Message;
            LoadUsers();
        }
        catch (Exception ex) { _message.Text = $"Account update failed: {ex.Message}"; }
    }

    private bool TryGetSelection(out TenantDatabaseConfig tenant, out string userId)
    {
        if (_tenantPicker.SelectedItem is TenantDatabaseConfig selectedTenant && !string.IsNullOrWhiteSpace(SelectedUserId()))
        {
            tenant = selectedTenant;
            userId = SelectedUserId()!;
            return true;
        }

        tenant = null!;
        userId = string.Empty;
        _message.Text = "Select a tenant user first.";
        return false;
    }
}
