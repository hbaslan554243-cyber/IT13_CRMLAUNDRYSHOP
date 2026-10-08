using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms;

/// <summary>Tenant C's branch list. Each branch is its own CompanyId data scope.</summary>
public sealed class BranchManagementView : UserControl
{
    private readonly LaundryShopDbContext _db;
    private readonly int _rootCompanyId;
    private readonly int _branchLimit;
    private readonly DataGridView _grid = new();
    private readonly Label _message = new();

    public BranchManagementView(LaundryShopDbContext db, int rootCompanyId, int branchLimit)
    {
        _db = db;
        _rootCompanyId = rootCompanyId;
        _branchLimit = Math.Max(1, branchLimit);
        BuildUi();
        LoadBranches();
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(12) };
        top.Controls.Add(new Label
        {
            Text = $"Tenant C Branches — {_branchLimit} total location(s) available, including the main branch.",
            AutoSize = true,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Left = 12,
            Top = 12
        });

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(10) };
        var add = new Button { Text = "Add Branch", Left = 10, Top = 10, Width = 100, Height = 30 };
        add.Click += (_, _) => AddBranch();
        var rename = new Button { Text = "Rename", Left = 120, Top = 10, Width = 90, Height = 30 };
        rename.Click += (_, _) => RenameBranch();
        var archive = new Button { Text = "Archive / Restore", Left = 220, Top = 10, Width = 130, Height = 30 };
        archive.Click += (_, _) => ToggleArchive();
        _message.Left = 365; _message.Top = 17; _message.AutoSize = true;
        bottom.Controls.AddRange(new Control[] { add, rename, archive, _message });

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(top);
    }

    private void LoadBranches()
    {
        var companies = _db.Companies
            .Where(c => c.Id == _rootCompanyId || c.ParentCompanyId == _rootCompanyId)
            .OrderBy(c => c.ParentCompanyId.HasValue)
            .ThenBy(c => c.ShopName)
            .ToList();
        var locations = companies.Select(c => new
        {
            c.Id,
            Branch = c.ParentCompanyId == null ? $"{c.ShopName} (Main)" : c.ShopName,
            Current = c.Id == (AuthController.CurrentCompanyId ?? _rootCompanyId) ? "● Current" : string.Empty,
            Status = c.IsActive ? "Active" : "Archived"
        }).ToList();
        _grid.DataSource = locations;
    }

    private void AddBranch()
    {
        var activeLocations = _db.Companies.Count(c => c.Id == _rootCompanyId && c.IsActive)
            + _db.Companies.Count(c => c.ParentCompanyId == _rootCompanyId && c.IsActive);
        if (activeLocations >= _branchLimit)
        {
            _message.Text = "The Super Admin's branch limit has been reached.";
            return;
        }

        var name = PromptForName("Add Branch", "Branch name:");
        if (name == null) return;
        if (name.Length > 150 || _db.Companies.Any(c => c.ParentCompanyId == _rootCompanyId && c.ShopName == name))
        {
            _message.Text = "Use a unique branch name up to 150 characters.";
            return;
        }

        var root = _db.Companies.First(c => c.Id == _rootCompanyId);
        var branch = new Company
        {
            OwnerUserId = root.OwnerUserId,
            ParentCompanyId = root.Id,
            ShopName = name,
            Address = root.Address,
            ContactNumber = root.ContactNumber,
            AllowStaffAccounts = root.AllowStaffAccounts,
            AllowManagerAccounts = root.AllowManagerAccounts,
            FrequentOrderThreshold = root.FrequentOrderThreshold,
            InactiveDaysThreshold = root.InactiveDaysThreshold,
            HighValueSpendThreshold = root.HighValueSpendThreshold,
            PesosPerPoint = root.PesosPerPoint,
            TermsAndConditions = root.TermsAndConditions,
            IsActive = true
        };
        _db.Companies.Add(branch);
        _db.SaveChanges();
        TenantDefaults.SeedFor(_db, branch.Id);
        try
        {
            DemoDataSeeder.SeedIfNoOrders(_db, branch.Id, orderCount: 120);
            _message.Text = $"{name} added and seeded with demo data. Switch to it to manage its users and reports.";
        }
        catch (Exception ex)
        {
            _message.Text = $"{name} was added, but demo data seeding failed: {ex.Message}";
        }
        LoadBranches();
    }

    private void RenameBranch()
    {
        if (!TryGetBranch(out var branch) || !branch.ParentCompanyId.HasValue) return;
        var name = PromptForName("Rename Branch", "New branch name:", branch.ShopName);
        if (name == null) return;
        if (name.Length > 150 || _db.Companies.Any(c => c.Id != branch.Id && c.ParentCompanyId == _rootCompanyId && c.ShopName == name))
        {
            _message.Text = "Use a unique branch name up to 150 characters.";
            return;
        }
        branch.ShopName = name;
        branch.UpdatedAt = DateTime.UtcNow;
        _db.SaveChanges();
        _message.Text = "Branch name updated.";
        LoadBranches();
    }

    private void ToggleArchive()
    {
        if (!TryGetBranch(out var branch) || !branch.ParentCompanyId.HasValue) return;
        if (!branch.IsActive)
        {
            var activeLocations = _db.Companies.Count(c => c.Id == _rootCompanyId && c.IsActive)
                + _db.Companies.Count(c => c.ParentCompanyId == _rootCompanyId && c.IsActive);
            if (activeLocations >= _branchLimit)
            {
                _message.Text = "The Super Admin's branch limit has been reached.";
                return;
            }
        }
        branch.IsActive = !branch.IsActive;
        branch.UpdatedAt = DateTime.UtcNow;
        _db.SaveChanges();
        _message.Text = branch.IsActive ? "Branch restored." : "Branch archived; its records are retained.";
        LoadBranches();
    }

    private bool TryGetBranch(out Company branch)
    {
        if (_grid.CurrentRow?.Cells["Id"].Value is int id && (branch = _db.Companies.FirstOrDefault(c => c.Id == id)!) != null)
            return true;
        branch = null!;
        _message.Text = "Select a branch first.";
        return false;
    }

    private string? PromptForName(string title, string labelText, string initial = "")
    {
        using var dialog = new Form { Text = title, Width = 390, Height = 155, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var label = new Label { Text = labelText, Left = 15, Top = 15, AutoSize = true };
        var input = new TextBox { Left = 15, Top = 42, Width = 340, Text = initial, MaxLength = 150 };
        var save = new Button { Text = "Save", Left = 270, Top = 78, Width = 85, DialogResult = DialogResult.OK };
        dialog.Controls.AddRange(new Control[] { label, input, save });
        dialog.AcceptButton = save;
        return dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(input.Text)
            ? input.Text.Trim()
            : null;
    }
}
