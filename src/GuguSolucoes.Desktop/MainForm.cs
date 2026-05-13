
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GuguSolucoes.Desktop.Core;
using GuguSolucoes.Desktop.Infrastructure;
using GuguSolucoes.Desktop.Modules.LimpaCache;
using GuguSolucoes.Desktop.Modules.PdfTools;
using GuguSolucoes.Desktop.UI;

namespace GuguSolucoes.Desktop;

public sealed class MainForm : Form
{
    private const string UiFontFamily = StyleManager.FontFamily;
    private const uint FlashwAll = 3;
    private const uint FlashwTimerNoForeground = 12;

    private enum ModuleView
    {
        VoltaGov,
        LimpaCache,
        JuntarPdf
    }

    private const int CleanupIntervalStepMinutes = 5;
    private const int CleanupIntervalMinMinutes = 5;
    private const int CleanupIntervalMaxMinutes = 1440;

    private static readonly ThemePalette DarkTheme = StyleManager.DarkTheme;
    private static readonly ThemePalette LightTheme = StyleManager.LightTheme;
    private static readonly Color AppBackground = DarkTheme.Background;
    private static readonly Color AppSurface = DarkTheme.Surface;
    private static readonly Color AppSurfaceSoft = DarkTheme.SurfaceSoft;
    private static readonly Color AppBorder = DarkTheme.Border;
    private static readonly Color TextPrimary = DarkTheme.TextPrimary;
    private static readonly Color TextSecondary = DarkTheme.TextSecondary;
    private static readonly Color TextReadableMuted = DarkTheme.TextMuted;
    private static readonly Color AccentBlue = StyleManager.AccentCyan;
    private static readonly Color AccentGreen = StyleManager.AccentMint;
    private static readonly Color AccentOrange = StyleManager.AccentBlue;

    private readonly AppPaths _appPaths;
    private readonly AppLogger _logger;
    private readonly SettingsStore _settingsStore;
    private readonly StartupRegistration _startupRegistration;
    private readonly RepairService _repairService;
    private readonly UpdateService _updateService;
    private readonly System.Windows.Forms.Timer _updateCheckTimer;
    private readonly Icon _appIcon;
    private readonly bool _startInTray;
    private readonly bool _launchedAfterUpdate;

    private readonly AppSettings _settings;

    private readonly CleanupLogWriter _cleanupLog;
    private readonly CleanupConfigStore _cleanupConfigStore;
    private readonly CleanupStateStore _cleanupStateStore;
    private readonly TempCleanupService _cleanupService;
    private readonly PdfMergeService _pdfMergeService;
    private readonly PdfToolService _pdfToolService;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu;

    private ModuleView _currentModule;
    private Button _navVoltaGovButton = null!;
    private Button _navLimpaCacheButton = null!;
    private Button _navJuntarPdfButton = null!;
    private CheckBox _themeToggle = null!;
    private Label _versionLabel = null!;
    private Label _updateStatusLabel = null!;

    private Panel _voltaGovView = null!;
    private Panel _limpaCacheView = null!;
    private Panel _juntarPdfView = null!;
    private Panel _headerPanel = null!;
    private Panel _sidebarPanel = null!;
    private Panel _contentHostPanel = null!;
    private Panel _workspacePanel = null!;
    private TableLayoutPanel _rootLayout = null!;
    private Panel _activeModulePanel = null!;
    private PdfSuiteForm? _embeddedPdfSuite;

    private Button _runRepairButton = null!;
    private Button _openGovLogsButton = null!;
    private Button _clearGovLogButton = null!;
    private ThemedProgressBar _overallProgress = null!;
    private Label _statusLabel = null!;
    private RichTextBox _outputBox = null!;

    private CheckBox _cleanupAutoEnabledCheck = null!;
    private TrackBar _cleanupIntervalSlider = null!;
    private Label _cleanupIntervalValueLabel = null!;
    private Label _cleanupIntervalCaptionLabel = null!;
    private CheckBox _cleanupWindowsTempCheck = null!;
    private CheckBox _cleanupUsersTempCheck = null!;
    private Button _saveCleanupConfigButton = null!;
    private Button _runCleanupNowButton = null!;
    private Button _openCleanupLogsButton = null!;
    private Label _cleanupIdentityLabel = null!;
    private Label _cleanupModeLabel = null!;
    private Label _cleanupLastRunLabel = null!;
    private Label _cleanupFreedLabel = null!;
    private Label _cleanupDeletedItemsLabel = null!;
    private Label _cleanupSummaryLabel = null!;
    private Label _cleanupFeedbackLabel = null!;

    private ListBox _pdfMergeListBox = null!;
    private TextBox _pdfMergeOutputPathText = null!;
    private Button _pdfMergeAddFilesButton = null!;
    private Button _pdfMergeMoveUpButton = null!;
    private Button _pdfMergeMoveDownButton = null!;
    private Button _pdfMergeRemoveSelectedButton = null!;
    private Button _pdfMergeClearListButton = null!;
    private Button _pdfMergeBrowseOutputButton = null!;
    private Button _pdfMergeRunButton = null!;
    private Button _pdfMergeOpenOutputButton = null!;
    private Button _pdfMergeClearLogButton = null!;
    private ProgressBar _pdfMergeProgressBar = null!;
    private Label _pdfMergeStatusLabel = null!;
    private RichTextBox _pdfMergeLogBox = null!;

    private bool _isRepairRunning;
    private bool _isCleanupRunning;
    private bool _isPdfMergeRunning;
    private bool _isUpdateCheckRunning;
    private bool _cleanupFeedbackIsError;
    private bool _allowClose;
    private string _lastPromptedUpdateTag = string.Empty;
    private Color _panelBorderColor = AppBorder;
    private Color _headerBorderColor = Color.FromArgb(58, 74, 98);

    public MainForm(bool startInTray = false, bool launchedAfterUpdate = false)
    {
        _startInTray = startInTray;
        _launchedAfterUpdate = launchedAfterUpdate;

        _appPaths = new AppPaths();
        _logger = new AppLogger(_appPaths);
        _settingsStore = new SettingsStore(_appPaths, _logger);
        _startupRegistration = new StartupRegistration();
        _repairService = new RepairService(_logger);
        _updateService = new UpdateService(_appPaths, _logger);
        _updateCheckTimer = new System.Windows.Forms.Timer();
        _updateCheckTimer.Tick += async (_, _) => await CheckForUpdatesAsync(userInitiated: false);
        _settings = _settingsStore.Load();
        _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();

        _cleanupLog = new CleanupLogWriter();
        _cleanupConfigStore = new CleanupConfigStore(CleanupPaths.ConfigFilePath, _cleanupLog);
        _cleanupStateStore = new CleanupStateStore(CleanupPaths.StateFilePath, _cleanupLog);
        _cleanupService = new TempCleanupService(_cleanupLog);
        _pdfToolService = new PdfToolService(_logger);
        _pdfMergeService = new PdfMergeService(_logger);

        Text = "Gugu Soluções - Rotinas de TI";
        Icon = _appIcon;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1240, 810);
        MinimumSize = new Size(920, 620);
        Font = new Font(UiFontFamily, 10F, FontStyle.Regular);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AppBackground;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;

        BuildNewInterface();
        StyleManager.EnableDoubleBufferingRecursive(
            this,
            static control => control is Panel or TableLayoutPanel or FlowLayoutPanel or ListView);
        ApplyTheme();

        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Abrir Painel", null, (_, _) => ShowFromTray());
        _trayMenu.Items.Add("Executar Reparo VoltaGov", null, async (_, _) => await RunRepairAsync());
        _trayMenu.Items.Add("Executar Limpeza de Temp", null, async (_, _) => await RunCleanupNowAsync());
        _trayMenu.Items.Add("Verificar Atualizações", null, async (_, _) => await CheckForUpdatesAsync(userInitiated: true));
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Sair", null, (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Text = "Gugu Soluções",
            Icon = _appIcon,
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        };

        FormClosing += OnFormClosing;
        Shown += async (_, _) => await OnShownAsync();

        _logger.LineLogged += OnLogLine;

        try
        {
            _startupRegistration.Ensure(_settings.LaunchAtStartup);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Falha ao registrar inicialização automática: {ex.Message}");
        }
    }

    private void BuildNewInterface()
    {
        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 12, 16, 16)
        };
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _rootLayout.Controls.Add(BuildHeaderPanel(), 0, 0);
        _rootLayout.Controls.Add(BuildMainWorkspace(), 0, 1);

        Controls.Add(_rootLayout);
    }

    private Control BuildHeaderPanel()
    {
        _headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(20, 12, 20, 12),
            BackColor = Color.FromArgb(13, 19, 28)
        };

        _headerPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = _headerPanel.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;

            using var shadowPath = StyleManager.CreateRoundedPath(new Rectangle(rect.X, rect.Y + 1, rect.Width, rect.Height), 12);
            using var shadowBrush = new SolidBrush(Color.FromArgb(42, 0, 0, 0));
            e.Graphics.FillPath(shadowBrush, shadowPath);

            using var surfacePath = StyleManager.CreateRoundedPath(rect, 12);
            using var fillBrush = new SolidBrush(_headerPanel.BackColor);
            e.Graphics.FillPath(fillBrush, surfacePath);

            if (_headerBorderColor.A > 0)
            {
                using var borderPen = new Pen(_headerBorderColor, 1F);
                e.Graphics.DrawPath(borderPen, surfacePath);
            }
        };

        _headerPanel.Controls.Add(new Label
        {
            Text = "Gugu Soluções",
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font(UiFontFamily, 21F, FontStyle.Bold),
            Location = new Point(16, 8),
            Tag = "title"
        });

        _headerPanel.Controls.Add(new Label
        {
            Text = "Plataforma de suporte TI para VoltaGov, LimpaCache e Juntar PDF",
            AutoSize = true,
            ForeColor = Color.FromArgb(187, 202, 230),
            BackColor = Color.Transparent,
            Font = new Font(UiFontFamily, 10F, FontStyle.Regular),
            Location = new Point(18, 50),
            Tag = "secondary"
        });

        _themeToggle = new CheckBox
        {
            Text = "Tema Claro",
            AutoSize = true,
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Font = new Font(UiFontFamily, 10F, FontStyle.Bold),
            Tag = "theme-toggle",
            Checked = _settings.UseLightTheme
        };
        _themeToggle.CheckedChanged += (_, _) => OnThemeToggleChanged();
        _headerPanel.Controls.Add(_themeToggle);

        _versionLabel = new Label
        {
            Text = $"v{UpdateService.FormatVersion(_updateService.CurrentVersion)}",
            AutoSize = true,
            ForeColor = TextReadableMuted,
            BackColor = Color.Transparent,
            Font = new Font(UiFontFamily, 9F, FontStyle.Bold),
            Tag = "muted"
        };
        _headerPanel.Controls.Add(_versionLabel);

        _updateStatusLabel = new Label
        {
            Text = "Update: aguardando",
            AutoSize = true,
            ForeColor = TextReadableMuted,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Font = new Font(UiFontFamily, 9F, FontStyle.Regular),
            Tag = "muted"
        };
        _updateStatusLabel.Click += async (_, _) => await CheckForUpdatesAsync(userInitiated: true);
        _headerPanel.Controls.Add(_updateStatusLabel);

        _headerPanel.Resize += (_, _) =>
        {
            StyleManager.ApplyRoundedRegion(_headerPanel, 12);
            PositionHeaderToggle();
        };
        StyleManager.ApplyRoundedRegion(_headerPanel, 12);
        PositionHeaderToggle();

        return _headerPanel;
    }

    private Control BuildMainWorkspace()
    {
        _workspacePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var workspaceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        workspaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        workspaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _sidebarPanel = (Panel)BuildSidebar();
        workspaceLayout.Controls.Add(_sidebarPanel, 0, 0);

        _contentHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            Padding = new Padding(10, 0, 0, 0)
        };

        _voltaGovView = BuildVoltaGovView();
        _limpaCacheView = BuildLimpaCacheView();
        _juntarPdfView = BuildJuntarPdfView();
        _voltaGovView.Visible = false;
        _limpaCacheView.Visible = false;
        _juntarPdfView.Visible = false;

        _contentHostPanel.Controls.Add(_voltaGovView);
        _contentHostPanel.Controls.Add(_limpaCacheView);
        _contentHostPanel.Controls.Add(_juntarPdfView);

        workspaceLayout.Controls.Add(_contentHostPanel, 1, 0);
        SwitchModule(ModuleView.VoltaGov);

        _workspacePanel.Controls.Add(workspaceLayout);
        return _workspacePanel;
    }
    private Control BuildSidebar()
    {
        var sidebar = CreateGlassPanel();
        sidebar.Dock = DockStyle.Fill;
        sidebar.Padding = new Padding(14, 16, 14, 16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Módulos",
            AutoSize = true,
            ForeColor = TextPrimary,
            Font = new Font(UiFontFamily, 13F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10),
            Tag = "title"
        }, 0, 0);

        _navVoltaGovButton = CreateNavButton("VoltaGov", AccentBlue);
        _navVoltaGovButton.Click += (_, _) => SwitchModule(ModuleView.VoltaGov);
        layout.Controls.Add(_navVoltaGovButton, 0, 1);

        _navLimpaCacheButton = CreateNavButton("LimpaCache", AccentGreen);
        _navLimpaCacheButton.Click += (_, _) => SwitchModule(ModuleView.LimpaCache);
        layout.Controls.Add(_navLimpaCacheButton, 0, 2);

        _navJuntarPdfButton = new Button { Visible = false };

        layout.Controls.Add(new Label
        {
            Text = "Atalhos",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 18, 0, 8),
            Tag = "secondary"
        }, 0, 3);

        var shortcuts = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        shortcuts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        shortcuts.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shortcuts.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var openGovLogsFromSidebar = CreateSecondaryActionButton("Logs VoltaGov");
        openGovLogsFromSidebar.Click += (_, _) => OpenFolder(_appPaths.LogsDirectory, _logger, "logs do VoltaGov");
        shortcuts.Controls.Add(openGovLogsFromSidebar, 0, 0);

        var openCleanupLogsFromSidebar = CreateSecondaryActionButton("Logs LimpaCache");
        openCleanupLogsFromSidebar.Click += (_, _) => OpenFolder(CleanupPaths.LogDirectory, _cleanupLog, "logs do LimpaCache");
        shortcuts.Controls.Add(openCleanupLogsFromSidebar, 0, 1);

        layout.Controls.Add(shortcuts, 0, 4);

        layout.Controls.Add(new Label
        {
            Text = "Aplicativo residente na bandeja",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 10, 0, 0),
            Tag = "secondary"
        }, 0, 5);

        sidebar.Controls.Add(layout);
        return sidebar;
    }

    private Panel BuildVoltaGovView()
    {
        var view = CreateGlassPanel();
        view.Dock = DockStyle.Fill;
        view.Padding = new Padding(18, 16, 18, 16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "VoltaGov",
            AutoSize = true,
            ForeColor = TextPrimary,
            Font = new Font(UiFontFamily, 20F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2),
            Tag = "title"
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "Diagnóstico de conectividade, reparo automático e acompanhamento em tempo real.",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 0, 0, 14),
            Tag = "secondary"
        }, 0, 1);

        var actionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _runRepairButton = CreatePrimaryActionButton("Executar Reparo Agora", 240, AccentBlue);
        _runRepairButton.Click += async (_, _) => await RunRepairAsync();
        actionRow.Controls.Add(_runRepairButton, 0, 0);

        _statusLabel = new Label
        {
            Text = "Pronto para executar.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 44,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextPrimary,
            Margin = new Padding(14, 0, 0, 0),
            Tag = "primary"
        };
        actionRow.Controls.Add(_statusLabel, 1, 0);

        layout.Controls.Add(actionRow, 0, 2);

        layout.Controls.Add(new Label
        {
            Text = "Progresso",
            AutoSize = true,
            ForeColor = TextPrimary,
            Font = new Font(UiFontFamily, 10.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
            Tag = "title"
        }, 0, 3);

        _overallProgress = new ThemedProgressBar
        {
            Dock = DockStyle.Top,
            Height = 8,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            TrackColor = DarkTheme.ProgressTrack,
            FillColor = DarkTheme.ProgressFill,
            BorderColor = DarkTheme.LogBorder,
            Margin = new Padding(0, 0, 0, 14)
        };
        layout.Controls.Add(_overallProgress, 0, 4);

        var toolRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };

        _openGovLogsButton = CreateSecondaryActionButton("Abrir Pasta de Logs", 190);
        _openGovLogsButton.Click += (_, _) => OpenFolder(_appPaths.LogsDirectory, _logger, "logs do VoltaGov");
        toolRow.Controls.Add(_openGovLogsButton);

        _clearGovLogButton = CreateSecondaryActionButton("Limpar Visor", 130);
        _clearGovLogButton.Click += (_, _) => _outputBox.Clear();
        toolRow.Controls.Add(_clearGovLogButton);

        layout.Controls.Add(toolRow, 0, 5);

        _outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            DetectUrls = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = DarkTheme.LogBackground,
            ForeColor = TextPrimary,
            Font = new Font("Consolas", 9.75F, FontStyle.Regular),
            Margin = new Padding(0)
        };
        layout.Controls.Add(_outputBox, 0, 6);

        layout.Controls.Add(new Label
        {
            Text = "feito por Gugu",
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 10, 0, 0),
            Tag = "secondary"
        }, 0, 7);

        view.Controls.Add(layout);
        return view;
    }
    private Panel BuildLimpaCacheView()
    {
        var view = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));

        var statusCard = CreateGlassPanel();
        statusCard.Dock = DockStyle.Fill;
        statusCard.Margin = new Padding(0, 0, 8, 0);
        statusCard.Padding = new Padding(18, 16, 18, 16);

        var configCard = CreateGlassPanel();
        configCard.Dock = DockStyle.Fill;
        configCard.Margin = new Padding(8, 0, 0, 0);
        configCard.Padding = new Padding(18, 16, 18, 16);

        root.Controls.Add(statusCard, 0, 0);
        root.Controls.Add(configCard, 1, 0);

        BuildLimpaCacheStatus(statusCard);
        BuildLimpaCacheConfig(configCard);

        view.Controls.Add(root);
        return view;
    }

    private void BuildLimpaCacheStatus(Control host)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        host.Controls.Add(layout);

        var cleanupTitle = new Label
        {
            Text = "LimpaCache",
            AutoSize = true,
            Font = new Font(UiFontFamily, 19F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 6),
            Tag = "title"
        };
        layout.Controls.Add(cleanupTitle, 0, 0);
        layout.SetColumnSpan(cleanupTitle, 2);

        var cleanupSubtitle = new Label
        {
            Text = "Monitoramento de limpeza e execução manual de temporários.",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 0, 0, 12),
            Tag = "secondary"
        };
        layout.Controls.Add(cleanupSubtitle, 0, 1);
        layout.SetColumnSpan(cleanupSubtitle, 2);

        layout.Controls.Add(CreateInfoLabel("Modo"), 0, 2);
        _cleanupModeLabel = CreateInfoValueLabel("-");
        layout.Controls.Add(_cleanupModeLabel, 1, 2);

        layout.Controls.Add(CreateInfoLabel("Conta ativa"), 0, 3);
        _cleanupIdentityLabel = CreateInfoValueLabel("-");
        layout.Controls.Add(_cleanupIdentityLabel, 1, 3);

        layout.Controls.Add(CreateInfoLabel("Última execução"), 0, 4);
        _cleanupLastRunLabel = CreateInfoValueLabel("-");
        layout.Controls.Add(_cleanupLastRunLabel, 1, 4);

        layout.Controls.Add(CreateInfoLabel("Espaco liberado"), 0, 5);
        _cleanupFreedLabel = CreateInfoValueLabel("-");
        layout.Controls.Add(_cleanupFreedLabel, 1, 5);

        layout.Controls.Add(CreateInfoLabel("Itens removidos"), 0, 6);
        _cleanupDeletedItemsLabel = CreateInfoValueLabel("-");
        layout.Controls.Add(_cleanupDeletedItemsLabel, 1, 6);

        _cleanupSummaryLabel = new Label
        {
            Text = "Nenhuma limpeza registrada ainda.",
            Dock = DockStyle.Fill,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 14, 0, 14),
            Tag = "secondary"
        };
        layout.Controls.Add(_cleanupSummaryLabel, 0, 7);
        layout.SetColumnSpan(_cleanupSummaryLabel, 2);

        var footerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0)
        };

        _runCleanupNowButton = CreatePrimaryActionButton("Limpar Agora", 160, AccentGreen);
        _runCleanupNowButton.Click += async (_, _) => await RunCleanupNowAsync();
        footerActions.Controls.Add(_runCleanupNowButton);

        _openCleanupLogsButton = CreateSecondaryActionButton("Abrir Logs", 130);
        _openCleanupLogsButton.Click += (_, _) => OpenFolder(CleanupPaths.LogDirectory, _cleanupLog, "logs do LimpaCache");
        footerActions.Controls.Add(_openCleanupLogsButton);

        layout.Controls.Add(footerActions, 0, 8);
        layout.SetColumnSpan(footerActions, 2);
    }

    private void BuildLimpaCacheConfig(Control host)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        host.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text = "Configuração",
            AutoSize = true,
            Font = new Font(UiFontFamily, 16F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 8),
            Tag = "title"
        }, 0, 0);

        _cleanupAutoEnabledCheck = new CheckBox
        {
            Text = "Ativar limpeza automática por intervalo",
            AutoSize = true,
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 10),
            Tag = "primary"
        };
        _cleanupAutoEnabledCheck.CheckedChanged += (_, _) => UpdateCleanupAutomationUiState();
        layout.Controls.Add(_cleanupAutoEnabledCheck, 0, 1);

        _cleanupIntervalCaptionLabel = new Label
        {
            Text = "Intervalo automático (minutos)",
            AutoSize = true,
            ForeColor = TextReadableMuted,
            Margin = new Padding(0, 0, 0, 4),
            Tag = "muted"
        };
        layout.Controls.Add(_cleanupIntervalCaptionLabel, 0, 2);

        _cleanupIntervalSlider = new TrackBar
        {
            Minimum = 1,
            Maximum = CleanupIntervalMaxMinutes / CleanupIntervalStepMinutes,
            TickFrequency = 12,
            LargeChange = 12,
            SmallChange = 1,
            Value = 12,
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = Color.FromArgb(22, 31, 48)
        };
        _cleanupIntervalSlider.ValueChanged += (_, _) => UpdateCleanupIntervalText();
        layout.Controls.Add(_cleanupIntervalSlider, 0, 3);

        var sliderHints = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        sliderHints.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        sliderHints.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        sliderHints.Controls.Add(new Label
        {
            Text = "5 min",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextSecondary,
            Tag = "secondary"
        }, 0, 0);

        sliderHints.Controls.Add(new Label
        {
            Text = "24 h",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = TextSecondary,
            Tag = "secondary"
        }, 1, 0);

        layout.Controls.Add(sliderHints, 0, 4);

        _cleanupIntervalValueLabel = new Label
        {
            Text = "Intervalo atual: 60 min",
            AutoSize = true,
            ForeColor = TextPrimary,
            Font = new Font(UiFontFamily, 10.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12),
            Tag = "muted"
        };
        layout.Controls.Add(_cleanupIntervalValueLabel, 0, 5);

        _cleanupWindowsTempCheck = new CheckBox
        {
            Text = "Limpar C:\\Windows\\Temp",
            AutoSize = true,
            Checked = true,
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 6),
            Tag = "primary"
        };
        layout.Controls.Add(_cleanupWindowsTempCheck, 0, 6);

        _cleanupUsersTempCheck = new CheckBox
        {
            Text = "Limpar %TEMP% de perfis de usuário",
            AutoSize = true,
            Checked = true,
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 14),
            Tag = "primary"
        };
        layout.Controls.Add(_cleanupUsersTempCheck, 0, 7);

        _saveCleanupConfigButton = CreatePrimaryActionButton("Salvar Configuração", 210, AccentBlue);
        _saveCleanupConfigButton.Click += (_, _) => SaveCleanupConfiguration();
        layout.Controls.Add(_saveCleanupConfigButton, 0, 8);

        _cleanupFeedbackLabel = new Label
        {
            Text = "Limpeza automática desativada por padrão. Marque a opção para ativar.",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 10, 0, 0),
            Tag = "secondary"
        };
        layout.Controls.Add(_cleanupFeedbackLabel, 0, 9);
    }

    private Panel BuildJuntarPdfView()
    {
        var view = CreateGlassPanel();
        view.Dock = DockStyle.Fill;
        view.Controls.Clear();
        view.Padding = new Padding(10);

        var suiteHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };

        _embeddedPdfSuite = new PdfSuiteForm(_pdfToolService)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill
        };
        _embeddedPdfSuite.SetThemeToggleVisible(false);
        _embeddedPdfSuite.SetTheme(_themeToggle is not null && _themeToggle.Checked);
        suiteHost.Controls.Add(_embeddedPdfSuite);
        _embeddedPdfSuite.Show();

        view.Controls.Add(suiteHost);
        return view;
    }

    private void AddPdfFilesToMergeList()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Selecione os PDFs para juntar",
            Filter = "Arquivos PDF (*.pdf)|*.pdf",
            Multiselect = true,
            CheckFileExists = true
        };

        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var existing = _pdfMergeListBox.Items.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var filePath in picker.FileNames)
        {
            if (existing.Add(filePath))
            {
                _pdfMergeListBox.Items.Add(filePath);
                added++;
            }
        }

        if (added > 0)
        {
            EnsurePdfMergeOutputPathFromInputs();
            LogPdfMerge($"[{DateTime.Now:HH:mm:ss}] {added} arquivo(s) adicionado(s) para merge.");
        }

        UpdatePdfMergeUiState();
    }

    private void MoveSelectedPdfMergeItem(int direction)
    {
        var selectedIndex = _pdfMergeListBox.SelectedIndex;
        if (selectedIndex < 0)
        {
            return;
        }

        var targetIndex = selectedIndex + direction;
        if (targetIndex < 0 || targetIndex >= _pdfMergeListBox.Items.Count)
        {
            return;
        }

        var selected = _pdfMergeListBox.Items[selectedIndex];
        _pdfMergeListBox.Items.RemoveAt(selectedIndex);
        _pdfMergeListBox.Items.Insert(targetIndex, selected);
        _pdfMergeListBox.SelectedIndex = targetIndex;
        UpdatePdfMergeUiState();
    }

    private void RemoveSelectedPdfMergeItem()
    {
        var selectedIndex = _pdfMergeListBox.SelectedIndex;
        if (selectedIndex < 0)
        {
            return;
        }

        var removedName = Path.GetFileName(_pdfMergeListBox.Items[selectedIndex].ToString() ?? string.Empty);
        _pdfMergeListBox.Items.RemoveAt(selectedIndex);
        LogPdfMerge($"[{DateTime.Now:HH:mm:ss}] Arquivo removido: {removedName}");
        UpdatePdfMergeUiState();
    }

    private void ClearPdfMergeList()
    {
        if (_pdfMergeListBox.Items.Count == 0)
        {
            return;
        }

        _pdfMergeListBox.Items.Clear();
        _pdfMergeOutputPathText.Clear();
        _pdfMergeProgressBar.Value = 0;
        _pdfMergeStatusLabel.Text = "Lista limpa. Selecione os PDFs para iniciar.";
        LogPdfMerge($"[{DateTime.Now:HH:mm:ss}] Lista de merge foi limpa.");
        UpdatePdfMergeUiState();
    }

    private void BrowsePdfMergeOutputPath()
    {
        var suggested = BuildDefaultPdfMergeOutputPath();

        using var picker = new SaveFileDialog
        {
            Title = "Defina o arquivo de saída",
            Filter = "Arquivos PDF (*.pdf)|*.pdf",
            FileName = Path.GetFileName(suggested),
            InitialDirectory = Path.GetDirectoryName(suggested) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            AddExtension = true,
            DefaultExt = "pdf",
            OverwritePrompt = true
        };

        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _pdfMergeOutputPathText.Text = picker.FileName;
    }

    private string BuildDefaultPdfMergeOutputPath()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (_pdfMergeListBox.Items.Count > 0)
        {
            var first = _pdfMergeListBox.Items[0].ToString() ?? string.Empty;
            var firstDirectory = Path.GetDirectoryName(first);
            var firstFile = Path.GetFileNameWithoutExtension(first);
            var outputDirectory = string.IsNullOrWhiteSpace(firstDirectory) ? desktop : firstDirectory;
            var stem = string.IsNullOrWhiteSpace(firstFile) ? "documentos" : firstFile;
            return Path.Combine(outputDirectory, $"{stem}-unido.pdf");
        }

        return Path.Combine(desktop, "documentos-unidos.pdf");
    }

    private void EnsurePdfMergeOutputPathFromInputs()
    {
        if (!string.IsNullOrWhiteSpace(_pdfMergeOutputPathText.Text))
        {
            return;
        }

        _pdfMergeOutputPathText.Text = BuildDefaultPdfMergeOutputPath();
    }

    private async Task RunPdfMergeAsync()
    {
        if (_isPdfMergeRunning)
        {
            return;
        }

        var inputPaths = _pdfMergeListBox.Items.Cast<string>().ToList();
        if (inputPaths.Count < 2)
        {
            MessageBox.Show(this, "Selecione ao menos dois arquivos PDF.", "Juntar PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        EnsurePdfMergeOutputPathFromInputs();
        var outputPath = _pdfMergeOutputPathText.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            MessageBox.Show(this, "Defina um arquivo de saída para concluir o merge.", "Juntar PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _isPdfMergeRunning = true;
        UpdatePdfMergeUiState();
        _pdfMergeProgressBar.Value = 0;
        _pdfMergeStatusLabel.Text = "Unindo PDFs...";
        LogPdfMerge($"[{DateTime.Now:HH:mm:ss}] Inicio do merge de {inputPaths.Count} arquivo(s).");

        try
        {
            var progress = new Progress<PdfMergeProgress>(entry =>
            {
                _pdfMergeProgressBar.Value = Math.Clamp(entry.Percent, 0, 100);
                _pdfMergeStatusLabel.Text = entry.Message;
                LogPdfMerge($"[{DateTime.Now:HH:mm:ss}] {entry.Message}");
            });

            var result = await _pdfMergeService.MergeAsync(inputPaths, outputPath, progress, CancellationToken.None);
            _pdfMergeProgressBar.Value = 100;
            _pdfMergeStatusLabel.Text = result.Summary;
            LogPdfMerge($"[{DateTime.Now:HH:mm:ss}] {result.Summary}");

            _notifyIcon.ShowBalloonTip(2500, "Juntar PDF", "PDF final gerado com sucesso.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _logger.Error("Juntar PDF falhou: " + ex.Message);
            _pdfMergeStatusLabel.Text = "Falha ao juntar os arquivos.";
            LogPdfMerge($"[{DateTime.Now:HH:mm:ss}] Erro: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Juntar PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isPdfMergeRunning = false;
            UpdatePdfMergeUiState();
        }
    }

    private void OpenPdfMergeOutputFolder()
    {
        var outputPath = _pdfMergeOutputPathText.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            MessageBox.Show(this, "Defina um caminho de saída para abrir a pasta.", "Juntar PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        OpenFolder(outputDirectory, _logger, "pasta de saída do Juntar PDF");
    }

    private void OpenPdfSuiteForm()
    {
        using var suite = new PdfSuiteForm(_pdfToolService);
        suite.ShowDialog(this);
    }

    private void UpdatePdfMergeUiState()
    {
        var hasFiles = _pdfMergeListBox.Items.Count > 0;
        var hasSelection = _pdfMergeListBox.SelectedIndex >= 0;
        var hasEnoughFiles = _pdfMergeListBox.Items.Count >= 2;
        var hasOutputPath = !string.IsNullOrWhiteSpace(_pdfMergeOutputPathText.Text);

        _pdfMergeAddFilesButton.Enabled = !_isPdfMergeRunning;
        _pdfMergeMoveUpButton.Enabled = !_isPdfMergeRunning && hasSelection && _pdfMergeListBox.SelectedIndex > 0;
        _pdfMergeMoveDownButton.Enabled = !_isPdfMergeRunning && hasSelection && _pdfMergeListBox.SelectedIndex >= 0 && _pdfMergeListBox.SelectedIndex < _pdfMergeListBox.Items.Count - 1;
        _pdfMergeRemoveSelectedButton.Enabled = !_isPdfMergeRunning && hasSelection;
        _pdfMergeClearListButton.Enabled = !_isPdfMergeRunning && hasFiles;
        _pdfMergeBrowseOutputButton.Enabled = !_isPdfMergeRunning;
        _pdfMergeRunButton.Enabled = !_isPdfMergeRunning && hasEnoughFiles && hasOutputPath;
        _pdfMergeOpenOutputButton.Enabled = !_isPdfMergeRunning && hasOutputPath;
        _pdfMergeClearLogButton.Enabled = !_isPdfMergeRunning && _pdfMergeLogBox.TextLength > 0;
        _pdfMergeOutputPathText.Enabled = !_isPdfMergeRunning;
    }

    private void LogPdfMerge(string line)
    {
        if (_pdfMergeLogBox.Lines.Length > 500)
        {
            var lines = _pdfMergeLogBox.Lines;
            var start = Math.Max(0, lines.Length - 380);
            var trimmed = new string[lines.Length - start];
            Array.Copy(lines, start, trimmed, 0, trimmed.Length);
            _pdfMergeLogBox.Lines = trimmed;
        }

        _pdfMergeLogBox.AppendText(line + Environment.NewLine);
        _pdfMergeLogBox.SelectionStart = _pdfMergeLogBox.TextLength;
        _pdfMergeLogBox.ScrollToCaret();
        UpdatePdfMergeUiState();
    }

    private Panel CreateGlassPanel()
    {
        var panel = new Panel
        {
            BackColor = AppSurface,
            Tag = "surface"
        };
        StyleManager.ConfigureSurfacePanel(panel, () => _panelBorderColor, radius: 12, shadowAlpha: 30);
        return panel;
    }

    private Button CreateNavButton(string text, Color accent)
    {
        var button = new Button
        {
            Text = text,
            Height = 46,
            BackColor = AppSurface,
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(UiFontFamily, 10.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Tag = accent,
            Dock = DockStyle.Top
        };
        StyleManager.ConfigureNavigationButton(button, GetActiveTheme(), false, accent);
        button.Resize += (_, _) => StyleManager.ApplyRoundedRegion(button, 9);
        StyleManager.ApplyRoundedRegion(button, 9);
        return button;
    }

    private Button CreatePrimaryActionButton(string text, int width, Color accent)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 42,
            BackColor = accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(UiFontFamily, 10F, FontStyle.Bold),
            Margin = new Padding(0, 0, 10, 8),
            Tag = "primary-button"
        };
        StyleManager.ConfigurePrimaryButton(button, GetActiveTheme(), accent);
        button.Resize += (_, _) => StyleManager.ApplyRoundedRegion(button, 9);
        StyleManager.ApplyRoundedRegion(button, 9);
        return button;
    }

    private Button CreateSecondaryActionButton(string text, int width = 0)
    {
        var button = new Button
        {
            Text = text,
            Height = 40,
            BackColor = AppSurfaceSoft,
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(UiFontFamily, 9.75F, FontStyle.Bold),
            Tag = "secondary-button"
        };

        if (width > 0)
        {
            button.Width = width;
            button.Margin = new Padding(0, 0, 10, 8);
        }
        else
        {
            button.Dock = DockStyle.Top;
            button.Margin = new Padding(0, 0, 0, 8);
        }

        StyleManager.ConfigureSecondaryButton(button, GetActiveTheme());
        button.Resize += (_, _) => StyleManager.ApplyRoundedRegion(button, 9);
        StyleManager.ApplyRoundedRegion(button, 9);
        return button;
    }

    private static Label CreateInfoLabel(string text)
    {
        return new Label
        {
            Text = text + ":",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 6, 10, 6),
            Tag = "secondary"
        };
    }

    private static Label CreateInfoValueLabel(string value)
    {
        return new Label
        {
            Text = value,
            AutoSize = true,
            ForeColor = TextPrimary,
            Font = new Font(UiFontFamily, 10.5F, FontStyle.Bold),
            Margin = new Padding(0, 6, 0, 6),
            Tag = "primary"
        };
    }

    private ThemePalette GetActiveTheme()
    {
        return StyleManager.ResolveTheme(_themeToggle is not null && _themeToggle.Checked);
    }

    private void OnThemeToggleChanged()
    {
        _settings.UseLightTheme = _themeToggle.Checked;

        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Falha ao salvar preferência de tema: {ex.Message}");
        }

        ApplyTheme();
    }

    private void PositionHeaderToggle()
    {
        if (_themeToggle is null || _headerPanel is null)
        {
            return;
        }

        var right = _headerPanel.ClientSize.Width - 20;
        var themeX = Math.Max(16, right - _themeToggle.Width);
        _themeToggle.Location = new Point(themeX, 14);

        if (_updateStatusLabel is not null)
        {
            var statusX = Math.Max(16, right - _updateStatusLabel.Width);
            _updateStatusLabel.Location = new Point(statusX, 50);
        }

        if (_versionLabel is not null)
        {
            var statusLeft = _updateStatusLabel?.Left ?? right;
            var versionX = Math.Max(16, statusLeft - _versionLabel.Width - 18);
            _versionLabel.Location = new Point(versionX, 50);
        }
    }

    private void ApplyTheme()
    {
        SuspendLayout();
        _rootLayout?.SuspendLayout();
        _workspacePanel?.SuspendLayout();
        _contentHostPanel?.SuspendLayout();

        var theme = GetActiveTheme();
        try
        {
            BackColor = theme.Background;
            _panelBorderColor = theme.Border;
            _headerBorderColor = theme.HeaderBorder;

            if (_headerPanel is not null)
            {
                _headerPanel.BackColor = theme.HeaderBackground;
                _headerPanel.Invalidate();
                PositionHeaderToggle();
            }

            if (_contentHostPanel is not null)
            {
                _contentHostPanel.BackColor = theme.Background;
            }

            ApplyThemeRecursive(this, theme);
            SwitchModule(_currentModule);
            UpdateCleanupAutomationUiState();

            if (_cleanupModeLabel is not null)
            {
                _cleanupModeLabel.ForeColor = CleanupSecurityContext.IsElevated() ? theme.Success : theme.Error;
            }

            if (_cleanupFeedbackLabel is not null)
            {
                _cleanupFeedbackLabel.ForeColor = _cleanupFeedbackIsError ? theme.Error : theme.TextSecondary;
            }

            if (_embeddedPdfSuite is not null)
            {
                _embeddedPdfSuite.SetTheme(_themeToggle.Checked);
            }
        }
        finally
        {
            _contentHostPanel?.ResumeLayout(performLayout: true);
            _workspacePanel?.ResumeLayout(performLayout: true);
            _rootLayout?.ResumeLayout(performLayout: true);
            ResumeLayout(performLayout: true);
        }
    }

    private void ApplyThemeRecursive(Control root, ThemePalette theme)
    {
        foreach (Control control in root.Controls)
        {
            if (control is PdfSuiteForm)
            {
                continue;
            }

            switch (control)
            {
                case Label label:
                {
                    var role = label.Tag as string;
                    label.BackColor = Color.Transparent;
                    label.ForeColor = role switch
                    {
                        "secondary" => theme.TextSecondary,
                        "muted" => theme.TextMuted,
                        _ => theme.TextPrimary
                    };
                    break;
                }
                case CheckBox checkBox:
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = theme.TextPrimary;
                    break;
                case TextBox textBox:
                    textBox.BackColor = textBox.Focused ? theme.InputFocusBackground : theme.InputBackground;
                    textBox.ForeColor = theme.TextPrimary;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case RichTextBox richTextBox:
                    richTextBox.BackColor = richTextBox.Focused ? theme.InputFocusBackground : theme.LogBackground;
                    richTextBox.ForeColor = theme.TextPrimary;
                    richTextBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ListBox listBox:
                    listBox.BackColor = theme.InputBackground;
                    listBox.ForeColor = theme.TextPrimary;
                    break;
                case ThemedProgressBar progressBar:
                    progressBar.TrackColor = theme.ProgressTrack;
                    progressBar.FillColor = theme.ProgressFill;
                    progressBar.BorderColor = theme.LogBorder;
                    break;
                case TrackBar trackBar:
                    trackBar.BackColor = theme.SurfaceSoft;
                    break;
                case Button button:
                    ApplyButtonTheme(button, theme);
                    break;
                case TableLayoutPanel:
                case FlowLayoutPanel:
                    control.BackColor = Color.Transparent;
                    break;
                case Panel panel:
                    if (ReferenceEquals(panel, _headerPanel))
                    {
                        panel.BackColor = theme.HeaderBackground;
                    }
                    else if (ReferenceEquals(panel, _contentHostPanel))
                    {
                        panel.BackColor = theme.Background;
                    }
                    else if (ReferenceEquals(panel, _workspacePanel))
                    {
                        panel.BackColor = Color.Transparent;
                    }
                    else if (panel.Tag as string == "surface")
                    {
                        panel.BackColor = theme.Surface;
                        panel.Invalidate();
                    }
                    else if (!ReferenceEquals(panel, _juntarPdfView))
                    {
                        panel.BackColor = Color.Transparent;
                    }
                    break;
            }

            if (control.HasChildren)
            {
                ApplyThemeRecursive(control, theme);
            }
        }
    }

    private void ApplyButtonTheme(Button button, ThemePalette theme)
    {
        if (ReferenceEquals(button, _navVoltaGovButton) ||
            ReferenceEquals(button, _navLimpaCacheButton) ||
            ReferenceEquals(button, _navJuntarPdfButton))
        {
            var isActive = (ReferenceEquals(button, _navVoltaGovButton) && _currentModule == ModuleView.VoltaGov) ||
                           (ReferenceEquals(button, _navLimpaCacheButton) && _currentModule == ModuleView.LimpaCache) ||
                           (ReferenceEquals(button, _navJuntarPdfButton) && _currentModule == ModuleView.JuntarPdf);
            var accent = button.Tag is Color accentColor ? accentColor : AccentBlue;
            StyleManager.ConfigureNavigationButton(button, theme, isActive, accent);
            return;
        }

        var style = button.Tag as string;
        if (style == "secondary-button")
        {
            StyleManager.ConfigureSecondaryButton(button, theme);
            return;
        }

        if (style == "primary-button")
        {
            var accent = button.BackColor;
            StyleManager.ConfigurePrimaryButton(button, theme, accent);
        }
    }

    private void SwitchModule(ModuleView module)
    {
        if (module == ModuleView.JuntarPdf)
        {
            module = ModuleView.VoltaGov;
        }

        _currentModule = module;
        var targetPanel = module switch
        {
            ModuleView.LimpaCache => _limpaCacheView,
            ModuleView.JuntarPdf => _juntarPdfView,
            _ => _voltaGovView
        };

        if (!ReferenceEquals(_activeModulePanel, targetPanel) && _contentHostPanel is not null)
        {
            _contentHostPanel.SuspendLayout();
            try
            {
                if (_activeModulePanel is not null && !_activeModulePanel.IsDisposed)
                {
                    _activeModulePanel.Visible = false;
                }

                targetPanel.Visible = true;
                targetPanel.BringToFront();
                _activeModulePanel = targetPanel;
            }
            finally
            {
                _contentHostPanel.ResumeLayout(performLayout: true);
            }
        }

        var theme = GetActiveTheme();
        ApplyNavState(_navVoltaGovButton, module == ModuleView.VoltaGov, theme);
        ApplyNavState(_navLimpaCacheButton, module == ModuleView.LimpaCache, theme);
        ApplyNavState(_navJuntarPdfButton, module == ModuleView.JuntarPdf, theme);
    }

    private static void ApplyNavState(Button button, bool active, ThemePalette theme)
    {
        var accent = button.Tag is Color color ? color : AccentBlue;
        StyleManager.ConfigureNavigationButton(button, theme, active, accent);
    }

    private async Task OnShownAsync()
    {
        Log("Gugu Soluções iniciado como aplicativo residente.");
        _statusLabel.Text = "Aplicativo ativo. VoltaGov pronto para diagnóstico.";

        LoadCleanupConfiguration();
        LoadCleanupState();
        UpdateCleanupIdentity();
        StartUpdateMonitor();

        if (_startInTray)
        {
            BeginInvoke(new Action(() => HideToTray(showBalloon: false)));
        }
        else if (_launchedAfterUpdate)
        {
            BeginInvoke(new Action(ShowUpdatedAppReady));
        }

        await CheckForUpdatesAsync(userInitiated: false);
    }

    private void StartUpdateMonitor()
    {
        if (!_settings.EnableAutoUpdate)
        {
            SetUpdateStatus("Update: desativado", isImportant: false);
            return;
        }

        var minutes = Math.Clamp(_settings.UpdateCheckIntervalMinutes, 1, 1440);
        _updateCheckTimer.Interval = (int)TimeSpan.FromMinutes(minutes).TotalMilliseconds;
        _updateCheckTimer.Start();
        SetUpdateStatus("Update: verificando...", isImportant: false);
    }

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (_isUpdateCheckRunning)
        {
            return;
        }

        if (!_settings.EnableAutoUpdate)
        {
            SetUpdateStatus("Update: desativado", isImportant: false);
            return;
        }

        _isUpdateCheckRunning = true;
        SetUpdateStatus("Update: verificando...", isImportant: false);

        try
        {
            var result = await _updateService.CheckForUpdateAsync(_settings, CancellationToken.None);
            if (!result.Success)
            {
                SetUpdateStatus("Update: indisponível", isImportant: true);
                _logger.Warn(result.Message);
                return;
            }

            if (!result.UpdateAvailable)
            {
                SetUpdateStatus($"Atualizado v{UpdateService.FormatVersion(_updateService.CurrentVersion)}", isImportant: false);
                return;
            }

            var versionText = result.LatestVersion is null ? result.LatestTag : $"v{result.LatestVersion}";
            SetUpdateStatus($"Update {versionText} disponível", isImportant: true);
            _logger.Info($"Update disponível: {result.LatestTag} ({result.InstallerName}).");

            var shouldOffer = userInitiated ||
                              result.Mandatory ||
                              _settings.AutoApplyUpdates ||
                              (_settings.NotifyOnUpdate && !string.Equals(_lastPromptedUpdateTag, result.LatestTag, StringComparison.OrdinalIgnoreCase));

            if (shouldOffer)
            {
                _lastPromptedUpdateTag = result.LatestTag;
                ShowUpdateReleaseAlert(result);
                await OfferUpdateAsync(result);
            }
        }
        catch (Exception ex)
        {
            SetUpdateStatus("Update: falha ao verificar", isImportant: true);
            _logger.Warn($"Falha ao verificar update: {ex.Message}");

            if (userInitiated)
            {
                MessageBox.Show(this, ex.Message, "Atualizações", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _isUpdateCheckRunning = false;
        }
    }

    private async Task OfferUpdateAsync(UpdateCheckResult update)
    {
        var autoApply = update.Mandatory || _settings.AutoApplyUpdates;
        if (!autoApply)
        {
            var message = $"A versão {update.LatestVersion} está disponível.\n\nDeseja baixar e instalar agora?";
            var choice = MessageBox.Show(this, message, "Atualização disponível", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (choice != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            SetUpdateStatus("Update: baixando...", isImportant: true);
            var result = await _updateService.DownloadAndStartInstallerAsync(update, _settings, CancellationToken.None);
            _logger.Info(result.Message);
            _notifyIcon.ShowBalloonTip(2500, "Gugu Soluções", "Instalador de atualização iniciado.", ToolTipIcon.Info);
            _allowClose = true;
            Close();
        }
        catch (Exception ex)
        {
            SetUpdateStatus("Update: falhou", isImportant: true);
            _logger.Error($"Falha ao aplicar update: {ex}");
            MessageBox.Show(this, ex.Message, "Atualizações", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowUpdateReleaseAlert(UpdateCheckResult update)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowUpdateReleaseAlert(update)));
            return;
        }

        var versionText = update.LatestVersion is null ? update.LatestTag : $"v{update.LatestVersion}";
        ShowFromTray();
        BringToFront();
        TopMost = true;
        TopMost = false;
        FlashTaskbar();
        _notifyIcon.ShowBalloonTip(
            7000,
            "Atualização disponível",
            $"A versão {versionText} está pronta para instalar.",
            ToolTipIcon.Info);
    }

    private void FlashTaskbar()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var flashInfo = new FlashWindowInfo
        {
            cbSize = Convert.ToUInt32(Marshal.SizeOf<FlashWindowInfo>()),
            hwnd = Handle,
            dwFlags = FlashwAll | FlashwTimerNoForeground,
            uCount = 8,
            dwTimeout = 0
        };

        FlashWindowEx(ref flashInfo);
    }

    private void SetUpdateStatus(string text, bool isImportant)
    {
        if (_updateStatusLabel is null)
        {
            return;
        }

        var theme = GetActiveTheme();
        _updateStatusLabel.Text = text;
        _updateStatusLabel.ForeColor = isImportant ? theme.Accent : theme.TextMuted;
        PositionHeaderToggle();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray(bool showBalloon = true)
    {
        Hide();
        ShowInTaskbar = false;

        if (showBalloon)
        {
            _notifyIcon.ShowBalloonTip(2500, "Gugu Soluções", "Aplicativo continua ativo na bandeja do sistema.", ToolTipIcon.Info);
        }
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ShowUpdatedAppReady()
    {
        ShowFromTray();
        BringToFront();
        FlashTaskbar();
        _notifyIcon.ShowBalloonTip(
            4000,
            "Gugu Soluções atualizado",
            $"Versão v{UpdateService.FormatVersion(_updateService.CurrentVersion)} pronta para uso.",
            ToolTipIcon.Info);
    }

    private void ExitApplication()
    {
        _allowClose = true;
        Close();
    }

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FlashWindowInfo flashInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private async Task RunRepairAsync()
    {
        if (_isRepairRunning)
        {
            return;
        }

        _isRepairRunning = true;
        _runRepairButton.Enabled = false;
        _overallProgress.IsIndeterminate = true;
        _overallProgress.Value = 0;

        try
        {
            var progress = new Progress<RepairProgress>(p =>
            {
                if (_overallProgress.IsIndeterminate)
                {
                    _overallProgress.IsIndeterminate = false;
                }

                _overallProgress.Value = Math.Clamp(p.Percent, 0, 100);
                _statusLabel.Text = $"{p.Stage}: {p.Message}";
                Log($"[{p.Stage}] {p.Message}");
            });

            var result = await _repairService.RunAsync(_settings, progress, CancellationToken.None, forceFullRepair: true);

            if (_overallProgress.Value < 100)
            {
                _overallProgress.Value = 100;
            }

            _statusLabel.Text = result.Summary;
            _notifyIcon.ShowBalloonTip(
                3000,
                "VoltaGov",
                result.Success ? "Reparo concluído com sucesso." : "Reparo concluído com alertas.",
                result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            _logger.Error($"Erro no reparo: {ex.Message}");
            _statusLabel.Text = "Erro ao executar reparo.";
            MessageBox.Show(this, ex.Message, "VoltaGov", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _overallProgress.IsIndeterminate = false;
            _overallProgress.Value = 100;
            _runRepairButton.Enabled = true;
            _isRepairRunning = false;
        }
    }

    private async Task RunCleanupNowAsync()
    {
        if (_isCleanupRunning)
        {
            return;
        }

        _isCleanupRunning = true;
        _runCleanupNowButton.Enabled = false;
        _saveCleanupConfigButton.Enabled = false;
        _openCleanupLogsButton.Enabled = false;
        SetCleanupFeedback("Executando limpeza manual...", isError: false);
        try
        {
            var config = BuildCleanupConfigFromUi();
            _cleanupConfigStore.Save(config);

            var result = await Task.Run(() => _cleanupService.Run(config));
            _cleanupStateStore.SaveRun(result);
            LoadCleanupState();

            var hasFailures = result.FailedItems > 0;
            SetCleanupFeedback(
                $"Limpeza concluída: {result.DeletedFiles} arquivos, {result.DeletedDirectories} pastas, " +
                $"{CleanupSizeFormatter.Humanize(result.FreedBytes)} liberados.",
                isError: hasFailures);

            _notifyIcon.ShowBalloonTip(
                3000,
                "LimpaCache",
                hasFailures ? "Limpeza concluída com alertas." : "Limpeza concluída com sucesso.",
                hasFailures ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _cleanupLog.Error("Falha na execução manual: " + ex);
            SetCleanupFeedback("A limpeza falhou. Verifique os logs para detalhes.", isError: true);
            MessageBox.Show(this, ex.Message, "LimpaCache", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _runCleanupNowButton.Enabled = true;
            _saveCleanupConfigButton.Enabled = true;
            _openCleanupLogsButton.Enabled = true;
            _isCleanupRunning = false;
        }
    }

    private void SaveCleanupConfiguration()
    {
        try
        {
            var config = BuildCleanupConfigFromUi();
            _cleanupConfigStore.Save(config);

            var message = config.AutoCleanupEnabled
                ? $"Configuração salva. Limpeza automática ativa a cada {config.IntervalMinutes} minuto(s)."
                : "Configuração salva. Limpeza automática permanece desativada.";

            SetCleanupFeedback(message, isError: false);
        }
        catch (Exception ex)
        {
            _cleanupLog.Error("Falha ao salvar configuração: " + ex);
            SetCleanupFeedback("Não foi possível salvar a configuração.", isError: true);
        }
    }

    private CleanupConfig BuildCleanupConfigFromUi()
    {
        return new CleanupConfig
        {
            AutoCleanupEnabled = _cleanupAutoEnabledCheck.Checked,
            IntervalMinutes = SliderValueToMinutes(_cleanupIntervalSlider.Value),
            CleanWindowsTemp = _cleanupWindowsTempCheck.Checked,
            CleanUsersTemp = _cleanupUsersTempCheck.Checked
        };
    }

    private void LoadCleanupConfiguration()
    {
        var config = _cleanupConfigStore.Load();
        _cleanupAutoEnabledCheck.Checked = config.AutoCleanupEnabled;
        _cleanupIntervalSlider.Value = MinutesToSliderValue(config.GetValidatedIntervalMinutes());
        _cleanupWindowsTempCheck.Checked = config.CleanWindowsTemp;
        _cleanupUsersTempCheck.Checked = config.CleanUsersTemp;

        UpdateCleanupIntervalText();
        UpdateCleanupAutomationUiState();
    }

    private void UpdateCleanupAutomationUiState()
    {
        var theme = GetActiveTheme();
        var enabled = _cleanupAutoEnabledCheck.Checked;
        _cleanupIntervalSlider.Enabled = enabled;
        _cleanupIntervalCaptionLabel.Enabled = true;
        _cleanupIntervalValueLabel.Enabled = true;
        _cleanupIntervalCaptionLabel.ForeColor = theme.TextMuted;
        _cleanupIntervalValueLabel.ForeColor = theme.TextPrimary;
    }

    private void UpdateCleanupIntervalText()
    {
        var minutes = SliderValueToMinutes(_cleanupIntervalSlider.Value);
        _cleanupIntervalValueLabel.Text = $"Intervalo atual: {minutes} min";
    }

    private static int SliderValueToMinutes(int sliderValue)
    {
        return Math.Clamp(sliderValue * CleanupIntervalStepMinutes, CleanupIntervalMinMinutes, CleanupIntervalMaxMinutes);
    }

    private static int MinutesToSliderValue(int minutes)
    {
        var clamped = Math.Clamp(minutes, CleanupIntervalMinMinutes, CleanupIntervalMaxMinutes);
        var value = (int)Math.Round(clamped / (double)CleanupIntervalStepMinutes);
        return Math.Clamp(value, 1, CleanupIntervalMaxMinutes / CleanupIntervalStepMinutes);
    }

    private void LoadCleanupState()
    {
        var state = _cleanupStateStore.Load();
        if (state.LastRun is null)
        {
            _cleanupLastRunLabel.Text = "Nunca";
            _cleanupFreedLabel.Text = "0 B";
            _cleanupDeletedItemsLabel.Text = "0";
            _cleanupSummaryLabel.Text = "Nenhuma limpeza registrada ainda.";
            return;
        }

        var lastRun = state.LastRun;
        var finishedAtLocal = ToLocalTime(lastRun.FinishedAtUtc);

        _cleanupLastRunLabel.Text = finishedAtLocal == DateTime.MinValue
            ? "-"
            : finishedAtLocal.ToString("dd/MM/yyyy HH:mm:ss");

        _cleanupFreedLabel.Text = CleanupSizeFormatter.Humanize(lastRun.FreedBytes);
        _cleanupDeletedItemsLabel.Text = (lastRun.DeletedFiles + lastRun.DeletedDirectories).ToString("N0");

        _cleanupSummaryLabel.Text =
            $"Duração: {lastRun.DurationSeconds}s. Arquivos: {lastRun.DeletedFiles}. " +
            $"Pastas: {lastRun.DeletedDirectories}. Ignorados: {lastRun.SkippedItems}. Falhas: {lastRun.FailedItems}.";
    }

    private static DateTime ToLocalTime(DateTime utcLike)
    {
        if (utcLike == DateTime.MinValue)
        {
            return DateTime.MinValue;
        }

        if (utcLike.Kind == DateTimeKind.Local)
        {
            return utcLike;
        }

        if (utcLike.Kind == DateTimeKind.Unspecified)
        {
            utcLike = DateTime.SpecifyKind(utcLike, DateTimeKind.Utc);
        }

        return utcLike.ToLocalTime();
    }

    private void UpdateCleanupIdentity()
    {
        var theme = GetActiveTheme();
        _cleanupIdentityLabel.Text = CleanupSecurityContext.CurrentIdentity();
        _cleanupModeLabel.Text = CleanupSecurityContext.IsElevated() ? "Modo elevado" : "Modo usuário";
        _cleanupModeLabel.ForeColor = CleanupSecurityContext.IsElevated()
            ? theme.Success
            : theme.Error;
    }

    private void SetCleanupFeedback(string message, bool isError)
    {
        var theme = GetActiveTheme();
        _cleanupFeedbackIsError = isError;
        _cleanupFeedbackLabel.Text = message;
        _cleanupFeedbackLabel.ForeColor = isError ? theme.Error : theme.TextSecondary;
    }

    private void OpenFolder(string path, AppLogger log, string context)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            log.Error($"Falha ao abrir {context}: {ex}");
            MessageBox.Show(this, $"Não foi possível abrir {context}.", "Gugu Soluções", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenFolder(string path, CleanupLogWriter log, string context)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            log.Error($"Falha ao abrir {context}: {ex}");
            MessageBox.Show(this, $"Não foi possível abrir {context}.", "Gugu Soluções", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnLogLine(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(OnLogLine), line);
            return;
        }

        Log(line);
    }

    private void Log(string line)
    {
        if (_outputBox.Lines.Length > 600)
        {
            var lines = _outputBox.Lines;
            var start = Math.Max(0, lines.Length - 500);
            var trimmed = new string[lines.Length - start];
            Array.Copy(lines, start, trimmed, 0, trimmed.Length);
            _outputBox.Lines = trimmed;
        }

        _outputBox.AppendText(line + Environment.NewLine);
        _outputBox.SelectionStart = _outputBox.TextLength;
        _outputBox.ScrollToCaret();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
            _trayMenu.Dispose();
            _appIcon.Dispose();
            _updateCheckTimer.Dispose();
            _updateService.Dispose();
            _logger.LineLogged -= OnLogLine;
        }

        base.Dispose(disposing);
    }
}




