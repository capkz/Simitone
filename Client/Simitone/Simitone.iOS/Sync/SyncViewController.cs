using CoreGraphics;
using Foundation;
using UIKit;

namespace Simitone.iOS.Sync;

/// <summary>
/// First-run screen: finds PC Companion instances on the LAN, lets the user pick
/// one and enter its PIN, then downloads the game files. Built entirely in code
/// (no storyboard) to keep the project buildable without Xcode's Interface
/// Builder being part of the loop.
/// </summary>
public sealed class SyncViewController : UIViewController
{
    public event Action<string>? SyncCompleted;

    private readonly BonjourBrowser _browser = new();
    private readonly List<DiscoveredPc> _discovered = new();
    private UITableView _table = null!;
    private UILabel _statusLabel = null!;
    private UIProgressView _progressView = null!;
    private bool _syncing;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View!.BackgroundColor = UIColor.SystemBackgroundColor;

        var title = new UILabel
        {
            Text = "Find your PC",
            Font = UIFont.BoldSystemFontOfSize(28),
            TranslatesAutoresizingMaskIntoConstraints = false,
        };

        _statusLabel = new UILabel
        {
            Text = "Looking for the PC Companion tool on your Wi-Fi network...",
            Font = UIFont.SystemFontOfSize(16),
            TextColor = UIColor.SecondaryLabelColor,
            Lines = 0,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };

        _table = new UITableView(CGRect.Empty, UITableViewStyle.Plain)
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        _table.RegisterClassForCellReuse(typeof(UITableViewCell), "pc");
        _table.Source = new PcTableSource(this);

        _progressView = new UIProgressView(UIProgressViewStyle.Default)
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            Hidden = true,
        };

        View.AddSubviews(title, _statusLabel, _table, _progressView);

        NSLayoutConstraint.ActivateConstraints(
        [
            title.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 24),
            title.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, 24),

            _statusLabel.TopAnchor.ConstraintEqualTo(title.BottomAnchor, 8),
            _statusLabel.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, 24),
            _statusLabel.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor, -24),

            _table.TopAnchor.ConstraintEqualTo(_statusLabel.BottomAnchor, 16),
            _table.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            _table.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
            _table.BottomAnchor.ConstraintEqualTo(_progressView.TopAnchor, -16),

            _progressView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, 24),
            _progressView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor, -24),
            _progressView.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor, -24),
        ]);

        _browser.PcDiscovered += pc => InvokeOnMainThread(() => { _discovered.Add(pc); _table.ReloadData(); });
        _browser.PcUpdated += _ => InvokeOnMainThread(() => _table.ReloadData());
        _browser.PcLost += pc => InvokeOnMainThread(() => { _discovered.Remove(pc); _table.ReloadData(); });
        _browser.Start();
    }

    private void OnPcSelected(DiscoveredPc pc)
    {
        if (_syncing || !pc.IsResolved || pc.HostName is null)
            return;

        var alert = UIAlertController.Create("Enter PIN", $"Shown on {pc.Name}'s screen", UIAlertControllerStyle.Alert);
        alert.AddTextField(field =>
        {
            field.Placeholder = "123456";
            field.KeyboardType = UIKeyboardType.NumberPad;
        });
        alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
        alert.AddAction(UIAlertAction.Create("Connect", UIAlertActionStyle.Default, _ =>
        {
            var pin = alert.TextFields![0].Text ?? "";
            _ = StartSyncAsync(pc, pin);
        }));

        PresentViewController(alert, true, null);
    }

    private async Task StartSyncAsync(DiscoveredPc pc, string pin)
    {
        _syncing = true;
        _browser.Stop();
        _progressView.Hidden = false;
        _progressView.Progress = 0;
        _statusLabel.Text = $"Connecting to {pc.Name}...";

        try
        {
            var client = new SyncClient(pc.HostName!, pc.Port, pin);
            var progress = new Progress<SyncProgress>(p =>
            {
                _statusLabel.Text = p.FilesDone < p.FilesTotal
                    ? $"Downloading {p.CurrentFile} ({p.FilesDone}/{p.FilesTotal})"
                    : "Done!";
                _progressView.Progress = p.BytesTotal > 0 ? (float)p.BytesDone / p.BytesTotal : 1f;
            });

            await client.SyncAsync(progress, CancellationToken.None);

            SyncCompleted?.Invoke(GameFilesStore.RootPath);
        }
        catch (Exception ex)
        {
            _syncing = false;
            _progressView.Hidden = true;
            _statusLabel.Text = $"Sync failed: {ex.Message}. Make sure the PIN was correct and try again.";
            _browser.Start();
        }
    }

    private sealed class PcTableSource(SyncViewController owner) : UITableViewSource
    {
        public override nint RowsInSection(UITableView tableView, nint section) => owner._discovered.Count;

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell("pc", indexPath);
            var pc = owner._discovered[(int)indexPath.Row];

            // Cells here are class-registered with the default UITableViewCell
            // style, whose DetailTextLabel is null (only .Subtitle/.Value1/.Value2
            // populate it) - putting everything in TextLabel avoids that trap.
            var detail = pc.IsResolved
                ? $"{pc.FileCount:N0} files, {pc.TotalBytes / 1_000_000_000.0:F2} GB"
                : "Resolving...";
            cell.TextLabel!.Text = $"{pc.Name}\n{detail}";
            cell.TextLabel.Lines = 2;
            cell.TextLabel.Font = UIFont.SystemFontOfSize(15);

            return cell;
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);
            owner.OnPcSelected(owner._discovered[(int)indexPath.Row]);
        }
    }
}
