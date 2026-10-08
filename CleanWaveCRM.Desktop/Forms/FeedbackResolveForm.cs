using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class FeedbackResolveForm : Form
    {
        private readonly FeedbackController _controller;
        private readonly int _feedbackId;

        private readonly Label _lblInfo = new();
        private readonly TextBox _txtMessage = new();
        private readonly TextBox _txtResolution = new();
        private readonly Button _btnEscalate = new();
        private readonly Button _btnResolve = new();
        private readonly Label _lblStatus = new();

        public FeedbackResolveForm(LaundryShopDbContext db, int feedbackId)
        {
            _controller = new FeedbackController(db);
            _feedbackId = feedbackId;
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Text = "Feedback Details";
            Width = 460;
            Height = 400;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _lblInfo.Left = 20; _lblInfo.Top = 20; _lblInfo.AutoSize = true; _lblInfo.Font = new Font("Segoe UI", 10, FontStyle.Bold);

            var lblMessage = new Label { Text = "Customer message:", Left = 20, Top = 55, AutoSize = true };
            _txtMessage.Left = 20; _txtMessage.Top = 75; _txtMessage.Width = 400; _txtMessage.Height = 80;
            _txtMessage.Multiline = true; _txtMessage.ReadOnly = true;

            var lblResolution = new Label { Text = "Resolution notes:", Left = 20, Top = 165, AutoSize = true };
            _txtResolution.Left = 20; _txtResolution.Top = 185; _txtResolution.Width = 400; _txtResolution.Height = 80;
            _txtResolution.Multiline = true;

            _btnEscalate.Text = "Escalate to Manager"; _btnEscalate.Left = 20; _btnEscalate.Top = 280; _btnEscalate.Width = 170; _btnEscalate.Height = 35;
            _btnEscalate.BackColor = Color.DarkOrange; _btnEscalate.ForeColor = Color.White;
            _btnEscalate.Click += (_, _) => Escalate();

            _btnResolve.Text = "Mark Resolved"; _btnResolve.Left = 200; _btnResolve.Top = 280; _btnResolve.Width = 150; _btnResolve.Height = 35;
            _btnResolve.BackColor = Color.SeaGreen; _btnResolve.ForeColor = Color.White;
            _btnResolve.Click += (_, _) => Resolve();

            var btnClose = new Button { Text = "Close", Left = 360, Top = 280, Width = 60, Height = 35 };
            btnClose.Click += (_, _) => Close();

            _lblStatus.Left = 20; _lblStatus.Top = 325; _lblStatus.Width = 400; _lblStatus.ForeColor = Color.SeaGreen;

            Controls.AddRange(new Control[] { _lblInfo, lblMessage, _txtMessage, lblResolution, _txtResolution, _btnEscalate, _btnResolve, btnClose, _lblStatus });
        }

        private void LoadData()
        {
            var fb = _controller.GetAll().FirstOrDefault(f => f.Id == _feedbackId);
            if (fb == null) { _lblInfo.Text = "Not found."; return; }

            _lblInfo.Text = $"{fb.Customer?.FullName} - {fb.Type} - {fb.Status}";
            _txtMessage.Text = fb.Message;
            _txtResolution.Text = fb.ResolutionNotes;

            // Handling feedback (resolving/escalating) is a Manager+ responsibility.
            // Front Desk Staff can log and view it, but not act on it.
            var role = AuthController.CurrentUser?.Role;
            var canHandle = role is UserRole.Manager or UserRole.AdminOwner or UserRole.SuperAdmin;
            _btnEscalate.Enabled = canHandle;
            _btnResolve.Enabled = canHandle;
            _txtResolution.ReadOnly = !canHandle;
        }

        private void Escalate()
        {
            var result = _controller.Escalate(_feedbackId);
            _lblStatus.Text = result.Message;
            LoadData();
        }

        private void Resolve()
        {
            var userId = AuthController.CurrentUser?.Id ?? string.Empty;
            var result = _controller.Resolve(_feedbackId, userId, _txtResolution.Text);
            _lblStatus.Text = result.Message;
            LoadData();
        }
    }
}
