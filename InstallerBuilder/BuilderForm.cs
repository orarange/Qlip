using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QlipInstallerBuilder;

internal sealed class BuilderForm : Form
{
    private readonly Button _btnBuild;
    private readonly ProgressBar _progress;
    private readonly TextBox _txtLog;
    private readonly ComboBox _cmbConfig;
    private readonly TextBox _txtQlipSrc;
    private readonly Button _btnBrowseQlipSrc;

    private CancellationTokenSource? _cts;

    public BuilderForm()
    {
        Text = "Qlip Installer Builder";
        Width = 900;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;

        var lblSrc = new Label { Text = "Qlip-src:", Left = 12, Top = 14, AutoSize = true };
        _txtQlipSrc = new TextBox { Left = 70, Top = 10, Width = 520 };
        _btnBrowseQlipSrc = new Button { Text = "参照...", Left = 600, Top = 9, Width = 80, Height = 26 };
        _btnBrowseQlipSrc.Click += (_, __) => BrowseQlipSrc();

        var lblConfig = new Label { Text = "構成:", Left = 12, Top = 14, AutoSize = true };
        lblConfig.Left = 700;
        lblConfig.Top = 14;

        _cmbConfig = new ComboBox { Left = 748, Top = 10, Width = 114, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbConfig.Items.AddRange(new object[] { "Release", "Debug" });
        _cmbConfig.SelectedIndex = 0;

        _btnBuild = new Button { Text = "ビルド開始", Left = 12, Top = 40, Width = 120, Height = 26 };
        _btnBuild.Click += async (_, __) => await BuildAsync();

        _progress = new ProgressBar { Left = 150, Top = 46, Width = 712, Height = 16, Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100 };

        _txtLog = new TextBox
        {
            Left = 12,
            Top = 74,
            Width = 850,
            Height = 466,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            WordWrap = false,
        };

        Controls.Add(lblSrc);
        Controls.Add(_txtQlipSrc);
        Controls.Add(_btnBrowseQlipSrc);

        Controls.Add(lblConfig);
        Controls.Add(_cmbConfig);
        Controls.Add(_btnBuild);
        Controls.Add(_progress);
        Controls.Add(_txtLog);

        // Best-effort default: env var or sibling folder
        string? env = Environment.GetEnvironmentVariable("QLIP_SRC_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            _txtQlipSrc.Text = env;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }

    private async Task BuildAsync()
    {
        if (_cts != null)
            return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            ToggleUi(isBuilding: true);
            SetProgress(0);
            _txtLog.Clear();

            string config = (string)_cmbConfig.SelectedItem!;

            var core = new BuilderCore(Log)
            {
                QlipSrcDir = string.IsNullOrWhiteSpace(_txtQlipSrc.Text) ? null : _txtQlipSrc.Text.Trim(),
            };

            // Rough progress steps (build itself is streamed to log)
            SetProgress(5);
            await core.BuildAsync(config, ct);
            SetProgress(100);

            MessageBox.Show(this,
                "完了:\n- installer\\publish にステージング\n- installer\\dist\\Qlip_win-x64.zip を生成\nInno Setup 6 が入っていれば installer\\dist にセットアップEXE（Webインストーラ）が生成されています。\n\nGitHub Releases へ Qlip_win-x64.zip をアセットとしてアップロードしてください。",
                "Qlip",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            Log("キャンセルしました。");
        }
        catch (Exception ex)
        {
            Log("エラー: " + ex);
            MessageBox.Show(this, ex.Message, "Qlip", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            ToggleUi(isBuilding: false);
        }
    }

    private void ToggleUi(bool isBuilding)
    {
        _btnBuild.Enabled = !isBuilding;
        _cmbConfig.Enabled = !isBuilding;
        _txtQlipSrc.Enabled = !isBuilding;
        _btnBrowseQlipSrc.Enabled = !isBuilding;
        UseWaitCursor = isBuilding;
    }

    private void BrowseQlipSrc()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "orarange/Qlip-src のフォルダを選択してください（Qlip.csprojがある場所）",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtQlipSrc.Text = dlg.SelectedPath;
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(Log), message);
            return;
        }
        _txtLog.AppendText(message + Environment.NewLine);
    }

    private void SetProgress(int value)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<int>(SetProgress), value);
            return;
        }
        _progress.Value = Math.Clamp(value, 0, 100);
    }
}
