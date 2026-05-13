using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Docnet.Core;
using Docnet.Core.Models;
using GuguSolucoes.Desktop.UI;
using PdfSharpCore.Pdf.IO;

namespace GuguSolucoes.Desktop.Modules.PdfTools;

internal sealed class PdfSuiteForm : Form
{
    private const string UiFontFamily = StyleManager.FontFamily;
    private static readonly ThemePalette DarkTheme = StyleManager.DarkTheme;
    private static readonly ThemePalette LightTheme = StyleManager.LightTheme;
    private static readonly Color AppBackground = DarkTheme.Background;
    private static readonly Color AppSurface = DarkTheme.Surface;
    private static readonly Color AppBorder = DarkTheme.Border;
    private static readonly Color TextPrimary = DarkTheme.TextPrimary;
    private static readonly Color TextSecondary = DarkTheme.TextSecondary;
    private static readonly Color TextMuted = DarkTheme.TextMuted;
    private static readonly Color AccentOrange = StyleManager.AccentCyan;
    private const string PreviewReorderDataFormat = "GuguSolucoes.PdfPreviewReorder";

    private enum QpdfState
    {
        Neutral,
        Checking,
        Ok,
        Error
    }

    private sealed class OperationItem
    {
        public OperationItem(PdfToolOperation operation, string label)
        {
            Operation = operation;
            Label = label;
        }

        public PdfToolOperation Operation { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }

    private sealed class SelectedFileItem
    {
        public required string FullPath { get; init; }
        public required string DisplayName { get; init; }
        public required Image Thumbnail { get; init; }
    }

    private readonly PdfToolService _service;
    private readonly Dictionary<string, Control> _optionRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SelectedFileItem> _selectedFiles = new();

    private TableLayoutPanel _contentLayout = null!;
    private TableLayoutPanel _runtimePanel = null!;
    private ComboBox _operationCombo = null!;
    private ComboBox _splitModeCombo = null!;
    private ComboBox _rotateDegreesCombo = null!;
    private ComboBox _pageNumberPositionCombo = null!;
    private ListView _filesListView = null!;
    private ColumnHeader _fileNameColumn = null!;
    private FlowLayoutPanel _previewFlow = null!;
    private Label _previewEmptyLabel = null!;
    private Label _dropHintLabel = null!;
    private TextBox _rangesText = null!;
    private TextBox _pagesText = null!;
    private TextBox _orderText = null!;
    private TextBox _prefixText = null!;
    private TextBox _watermarkText = null!;
    private TextBox _watermarkFontSizeText = null!;
    private TextBox _watermarkOpacityText = null!;
    private TextBox _pageNumberStartText = null!;
    private TextBox _pageNumberFontSizeText = null!;
    private TextBox _protectOwnerText = null!;
    private TextBox _protectUserText = null!;
    private TextBox _unlockPasswordText = null!;
    private Button _addFilesButton = null!;
    private Button _removeSelectedButton = null!;
    private Button _clearFilesButton = null!;
    private Button _processButton = null!;
    private Button _downloadButton = null!;
    private CheckBox _showAdvancedToggle = null!;
    private CheckBox _themeToggle = null!;
    private Label _qpdfStatusLabel = null!;
    private Label _hintLabel = null!;
    private Label _statusLabel = null!;
    private ThemedProgressBar _progressBar = null!;
    private Panel _shellPanel = null!;
    private Panel _scrollViewport = null!;
    private Panel _filesDropPanel = null!;
    private Panel _optionsPanel = null!;
    private Color _shellBorderColor = AppBorder;
    private bool _isRunning;
    private bool _qpdfCheckInProgress;
    private QpdfState _qpdfState = QpdfState.Neutral;
    private string _workspaceDirectory = string.Empty;
    private List<string> _generatedOutputPaths = new();
    private Point _cardDragStartPoint = Point.Empty;
    private int _cardDragSourceIndex = -1;

    public PdfSuiteForm(PdfToolService service)
    {
        _service = service;

        Text = "Juntar PDF - Suite Completa";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1120, 800);
        MinimumSize = new Size(840, 620);
        BackColor = AppBackground;
        Font = new Font(UiFontFamily, 10F, FontStyle.Regular);
        AutoScaleMode = AutoScaleMode.Dpi;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;

        BuildInterface();
        StyleManager.EnableDoubleBufferingRecursive(
            this,
            static control => control is Panel or TableLayoutPanel or FlowLayoutPanel or ListView);
        _operationCombo.SelectedIndex = 0;
        UpdateUiState();
        ApplyTheme();
        _ = RefreshQpdfStatusAsync();
    }

    private void BuildInterface()
    {
        _shellPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppSurface,
            Padding = new Padding(18),
            Tag = "surface"
        };
        ConfigureSurfacePanel(_shellPanel, 12);

        _scrollViewport = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        _scrollViewport.Resize += (_, _) => SyncContentWidth();

        _contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 14,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };
        _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _contentLayout.Controls.Add(new Label
        {
            Text = "JuntarPDF",
            AutoSize = true,
            Font = new Font(UiFontFamily, 19F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 2),
            Tag = "title"
        }, 0, 0);

        _contentLayout.Controls.Add(new Label
        {
            Text = "Fluxo simples: escolha a ferramenta, adicione arquivos, veja miniaturas e baixe o resultado.",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 0, 0, 12),
            Tag = "secondary"
        }, 0, 1);

        _contentLayout.Controls.Add(BuildToolStepRow(), 0, 2);
        _contentLayout.Controls.Add(BuildFileActionsRow(), 0, 3);
        _contentLayout.Controls.Add(BuildFileNamesPanel(), 0, 4);

        _contentLayout.Controls.Add(new Label
        {
            Text = "Passo 3 - Pré-visualização de miniaturas",
            AutoSize = true,
            ForeColor = TextPrimary,
            Font = new Font(UiFontFamily, 11.5F, FontStyle.Bold),
            Margin = new Padding(0, 10, 0, 4),
            Tag = "title"
        }, 0, 5);

        _contentLayout.Controls.Add(BuildPreviewPanel(), 0, 6);

        _qpdfStatusLabel = new Label
        {
            Text = "QPDF: não necessário nesta operação.",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 8, 0, 8),
            Tag = "status"
        };
        _contentLayout.Controls.Add(_qpdfStatusLabel, 0, 7);

        _showAdvancedToggle = new CheckBox
        {
            Text = "Mostrar opções avançadas da ferramenta",
            AutoSize = true,
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8),
            Tag = "secondary"
        };
        _showAdvancedToggle.CheckedChanged += (_, _) => UpdateUiState();
        _contentLayout.Controls.Add(_showAdvancedToggle, 0, 8);

        _contentLayout.Controls.Add(BuildOptionsPanel(), 0, 9);

        _hintLabel = new Label
        {
            Text = "Selecione a ferramenta para continuar.",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 0, 0, 8),
            Tag = "secondary"
        };
        _contentLayout.Controls.Add(_hintLabel, 0, 10);

        _contentLayout.Controls.Add(BuildActionRow(), 0, 11);
        _contentLayout.Controls.Add(BuildRuntimePanel(), 0, 12);

        _contentLayout.Controls.Add(new Label
        {
            Text = "Processamento 100% local. Nenhum arquivo enviado para a internet.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 10, 0, 0),
            Tag = "secondary"
        }, 0, 13);

        _scrollViewport.Controls.Add(_contentLayout);
        _shellPanel.Controls.Add(_scrollViewport);
        Controls.Add(_shellPanel);

        ConfigureDropTargets();
        StyleManager.EnableDoubleBuffering(_shellPanel);
        StyleManager.EnableDoubleBuffering(_previewFlow);
        StyleManager.EnableDoubleBuffering(_filesListView);
        SyncContentWidth();
    }

    private Control BuildToolStepRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 8),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        row.Controls.Add(CreateStepLabel("Passo 1 - Ferramenta"), 0, 0);

        _operationCombo = CreateComboBox();
        _operationCombo.Items.AddRange(new object[]
        {
            new OperationItem(PdfToolOperation.Merge, "Juntar PDF"),
            new OperationItem(PdfToolOperation.Split, "Dividir PDF"),
            new OperationItem(PdfToolOperation.Extract, "Extrair Páginas"),
            new OperationItem(PdfToolOperation.Remove, "Remover Páginas"),
            new OperationItem(PdfToolOperation.Rotate, "Girar Páginas"),
            new OperationItem(PdfToolOperation.Reorder, "Reordenar Páginas"),
            new OperationItem(PdfToolOperation.Watermark, "Marca d'água"),
            new OperationItem(PdfToolOperation.PageNumbers, "Numerar Páginas"),
            new OperationItem(PdfToolOperation.ImagesToPdf, "Imagens para PDF"),
            new OperationItem(PdfToolOperation.Compress, "Comprimir PDF (QPDF)"),
            new OperationItem(PdfToolOperation.Protect, "Proteger PDF (QPDF)"),
            new OperationItem(PdfToolOperation.Unlock, "Desbloquear PDF (QPDF)"),
            new OperationItem(PdfToolOperation.Repair, "Reparar PDF (QPDF)")
        });
        _operationCombo.SelectedIndexChanged += async (_, _) => await OnOperationChangedAsync();
        row.Controls.Add(_operationCombo, 1, 0);
        return row;
    }

    private Control BuildFileActionsRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 6),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        row.Controls.Add(CreateStepLabel("Passo 2 - Arquivos"), 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0)
        };

        _addFilesButton = CreatePrimaryButton("Adicionar Arquivos", 200);
        _addFilesButton.Click += (_, _) => AddFiles();
        actions.Controls.Add(_addFilesButton);

        _removeSelectedButton = CreateSecondaryButton("Remover Selecionado", 190);
        _removeSelectedButton.Click += (_, _) => RemoveSelectedFile();
        actions.Controls.Add(_removeSelectedButton);

        _clearFilesButton = CreateSecondaryButton("Limpar Arquivos", 160);
        _clearFilesButton.Click += (_, _) => ClearFiles();
        actions.Controls.Add(_clearFilesButton);

        row.Controls.Add(actions, 1, 0);
        return row;
    }

    private Control BuildFileNamesPanel()
    {
        _filesDropPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 140,
            MinimumSize = new Size(0, 116),
            BackColor = DarkTheme.InputBackground,
            Padding = new Padding(8),
            Tag = "surface"
        };

        var header = new Label
        {
            Text = "Arquivos anexados (somente nomes)",
            AutoSize = true,
            ForeColor = TextMuted,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 6),
            Tag = "muted"
        };
        _filesDropPanel.Controls.Add(header);

        _dropHintLabel = new Label
        {
            Text = "Arraste e solte aqui para adicionar arquivos",
            AutoSize = false,
            Height = 24,
            ForeColor = TextSecondary,
            Dock = DockStyle.Bottom,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 4, 0, 0),
            Tag = "secondary"
        };
        _filesDropPanel.Controls.Add(_dropHintLabel);

        _filesListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            HeaderStyle = ColumnHeaderStyle.None,
            HideSelection = false,
            FullRowSelect = true,
            MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle,
            LabelWrap = false
        };
        _fileNameColumn = new ColumnHeader { Text = "Nome do arquivo" };
        _filesListView.Columns.Add(_fileNameColumn);
        _filesListView.SelectedIndexChanged += (_, _) => UpdateUiState();
        _filesListView.Resize += (_, _) => UpdateFileColumnWidth();
        _filesDropPanel.Controls.Add(_filesListView);
        _filesDropPanel.Controls.SetChildIndex(header, 2);
        _filesDropPanel.Controls.SetChildIndex(_dropHintLabel, 1);

        ConfigureSurfacePanel(_filesDropPanel, 10);

        return _filesDropPanel;
    }

    private Control BuildPreviewPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 220,
            MinimumSize = new Size(0, 170),
            Padding = new Padding(8),
            BackColor = DarkTheme.InputBackground,
            Tag = "surface"
        };
        ConfigureSurfacePanel(panel, 10);

        _previewFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            Margin = new Padding(0),
            Padding = new Padding(2),
            BackColor = Color.Transparent
        };

        _previewEmptyLabel = new Label
        {
            Text = "Nenhum arquivo anexado ainda.",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(10, 8, 0, 0),
            Tag = "secondary"
        };
        _previewFlow.Controls.Add(_previewEmptyLabel);
        panel.Controls.Add(_previewFlow);
        return panel;
    }

    private Control BuildOptionsPanel()
    {
        _optionsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            BackColor = DarkTheme.InputBackground,
            Margin = new Padding(0, 0, 0, 8),
            Tag = "surface"
        };
        ConfigureSurfacePanel(_optionsPanel, 10);

        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        BuildOptionRows(host);
        _optionsPanel.Controls.Add(host);
        return _optionsPanel;
    }

    private void BuildOptionRows(TableLayoutPanel host)
    {
        _splitModeCombo = CreateComboBox();
        _splitModeCombo.Items.Add("Intervalos");
        _splitModeCombo.Items.Add("Página por página");
        _splitModeCombo.SelectedIndex = 0;
        _splitModeCombo.SelectedIndexChanged += (_, _) => UpdateUiState();
        AddOptionRow(host, "split_mode", CreateOptionRow("Modo de divisão", _splitModeCombo));

        _rangesText = CreateTextBox("1-3,5");
        AddOptionRow(host, "ranges", CreateOptionRow("Intervalos / páginas", _rangesText));

        _prefixText = CreateTextBox("página");
        AddOptionRow(host, "prefix", CreateOptionRow("Prefixo dos arquivos", _prefixText));

        _pagesText = CreateTextBox("2,4");
        AddOptionRow(host, "pages", CreateOptionRow("Páginas", _pagesText));

        _rotateDegreesCombo = CreateComboBox();
        _rotateDegreesCombo.Items.AddRange(new object[] { "90", "180", "270" });
        _rotateDegreesCombo.SelectedIndex = 0;
        AddOptionRow(host, "rotate_degrees", CreateOptionRow("Grau de rotação", _rotateDegreesCombo));

        _orderText = CreateTextBox("3,1,2,4");
        AddOptionRow(host, "order", CreateOptionRow("Nova ordem", _orderText));

        _watermarkText = CreateTextBox("CONFIDENCIAL");
        AddOptionRow(host, "watermark_text", CreateOptionRow("Texto da marca d'água", _watermarkText));

        _watermarkFontSizeText = CreateTextBox("42");
        AddOptionRow(host, "watermark_size", CreateOptionRow("Tamanho da fonte", _watermarkFontSizeText));

        _watermarkOpacityText = CreateTextBox("0.25");
        AddOptionRow(host, "watermark_opacity", CreateOptionRow("Opacidade (0.05-1)", _watermarkOpacityText));

        _pageNumberStartText = CreateTextBox("1");
        AddOptionRow(host, "number_start", CreateOptionRow("Número inicial", _pageNumberStartText));

        _pageNumberPositionCombo = CreateComboBox();
        _pageNumberPositionCombo.Items.AddRange(new object[]
        {
            "Inferior direita",
            "Inferior esquerda",
            "Superior direita",
            "Superior esquerda",
            "Centro"
        });
        _pageNumberPositionCombo.SelectedIndex = 0;
        AddOptionRow(host, "number_position", CreateOptionRow("Posição da numeração", _pageNumberPositionCombo));

        _pageNumberFontSizeText = CreateTextBox("11");
        AddOptionRow(host, "number_font", CreateOptionRow("Fonte da numeração", _pageNumberFontSizeText));

        _protectOwnerText = CreateTextBox(string.Empty);
        _protectOwnerText.UseSystemPasswordChar = true;
        AddOptionRow(host, "protect_owner", CreateOptionRow("Senha do proprietário", _protectOwnerText));

        _protectUserText = CreateTextBox(string.Empty);
        _protectUserText.UseSystemPasswordChar = true;
        AddOptionRow(host, "protect_user", CreateOptionRow("Senha do usuário", _protectUserText));

        _unlockPasswordText = CreateTextBox(string.Empty);
        _unlockPasswordText.UseSystemPasswordChar = true;
        AddOptionRow(host, "unlock_password", CreateOptionRow("Senha atual", _unlockPasswordText));
    }

    private void AddOptionRow(TableLayoutPanel host, string key, Control row)
    {
        var nextRow = host.RowCount;
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.Controls.Add(RegisterOptionRow(key, row), 0, nextRow);
        host.RowCount = nextRow + 1;
    }

    private Control BuildActionRow()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        _processButton = CreatePrimaryButton("Processar Arquivos", 210);
        _processButton.Click += async (_, _) => await ProcessCurrentOperationAsync();
        row.Controls.Add(_processButton);

        _downloadButton = CreateSecondaryButton("Download Resultado", 190);
        _downloadButton.Click += (_, _) => DownloadResult();
        row.Controls.Add(_downloadButton);

        _themeToggle = new CheckBox
        {
            Text = "Tema Claro",
            AutoSize = true,
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Margin = new Padding(10, 10, 0, 0),
            Tag = "theme-toggle"
        };
        _themeToggle.CheckedChanged += (_, _) => ApplyTheme();
        row.Controls.Add(_themeToggle);

        return row;
    }

    private Control BuildRuntimePanel()
    {
        _runtimePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        _runtimePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _runtimePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _runtimePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _statusLabel = new Label
        {
            Text = "Pronto para processar.",
            AutoSize = true,
            ForeColor = TextMuted,
            Margin = new Padding(0, 0, 0, 6),
            Tag = "muted"
        };
        _runtimePanel.Controls.Add(_statusLabel, 0, 0);

        _progressBar = new ThemedProgressBar
        {
            Dock = DockStyle.Top,
            Height = 8,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            TrackColor = DarkTheme.ProgressTrack,
            FillColor = DarkTheme.ProgressFill,
            BorderColor = DarkTheme.LogBorder,
            Margin = new Padding(0, 0, 0, 0)
        };
        _runtimePanel.Controls.Add(_progressBar, 0, 1);
        return _runtimePanel;
    }

    private void ConfigureDropTargets()
    {
        ConfigureDropTarget(_filesDropPanel);
        ConfigureDropTarget(_filesListView);
        ConfigureDropTarget(_previewFlow);
        ConfigureDropTarget(_previewEmptyLabel);
        ConfigureDropTarget(_scrollViewport);
        ConfigureDropTarget(_shellPanel);
    }

    private void ConfigureDropTarget(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += OnDropTargetDragEnter;
        control.DragDrop += OnDropTargetDragDrop;
    }

    private void OnDropTargetDragEnter(object? sender, DragEventArgs e)
    {
        if (TryGetPreviewDragIndex(e, out _))
        {
            e.Effect = _selectedFiles.Count > 1 ? DragDropEffects.Move : DragDropEffects.None;
            return;
        }

        if (!TryGetDroppedPaths(e, out var paths))
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        var anyValid = paths.Any(IsSupportedByCurrentOperation);
        e.Effect = anyValid ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDropTargetDragDrop(object? sender, DragEventArgs e)
    {
        if (TryGetPreviewDragIndex(e, out var sourceIndex))
        {
            ReorderPreviewCards(sender as Control, e, sourceIndex);
            return;
        }

        if (!TryGetDroppedPaths(e, out var paths))
        {
            return;
        }

        AddFilesFromPaths(paths);
    }

    private static bool TryGetPreviewDragIndex(DragEventArgs e, out int index)
    {
        index = -1;
        var dataObject = e.Data;
        if (dataObject is null || !dataObject.GetDataPresent(PreviewReorderDataFormat))
        {
            return false;
        }

        if (dataObject.GetData(PreviewReorderDataFormat) is int draggedIndex)
        {
            index = draggedIndex;
            return true;
        }

        return false;
    }

    private void ReorderPreviewCards(Control? sender, DragEventArgs e, int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _selectedFiles.Count || _selectedFiles.Count < 2)
        {
            return;
        }

        var targetIndex = ResolvePreviewDropIndex(sender, e);
        targetIndex = Math.Clamp(targetIndex, 0, _selectedFiles.Count - 1);

        if (sourceIndex == targetIndex)
        {
            return;
        }

        var moved = _selectedFiles[sourceIndex];
        _selectedFiles.RemoveAt(sourceIndex);
        if (targetIndex > sourceIndex)
        {
            targetIndex--;
        }

        _selectedFiles.Insert(targetIndex, moved);
        ResetGeneratedResult();
        RefreshFilesView();

        if (targetIndex >= 0 && targetIndex < _filesListView.Items.Count)
        {
            _filesListView.Items[targetIndex].Selected = true;
            _filesListView.EnsureVisible(targetIndex);
        }

        _statusLabel.Text = $"Ordem atualizada. {moved.DisplayName} foi movido.";
        UpdateUiState();
    }

    private int ResolvePreviewDropIndex(Control? sender, DragEventArgs e)
    {
        var senderControl = sender ?? _previewFlow;
        var cardBySender = FindPreviewCard(senderControl);
        if (cardBySender?.Tag is int senderIndex)
        {
            return senderIndex;
        }

        var pointInFlow = _previewFlow.PointToClient(new Point(e.X, e.Y));
        foreach (Control child in _previewFlow.Controls)
        {
            if (FindPreviewCard(child) is not Panel card || card.Tag is not int index)
            {
                continue;
            }

            var cardCenterX = card.Left + card.Width / 2;
            if (pointInFlow.X <= cardCenterX && pointInFlow.Y <= card.Bottom + 6)
            {
                return index;
            }
        }

        return _selectedFiles.Count - 1;
    }

    private static Panel? FindPreviewCard(Control? start)
    {
        var current = start;
        while (current is not null)
        {
            if (string.Equals(current.Name, "preview-card", StringComparison.OrdinalIgnoreCase))
            {
                return current as Panel;
            }

            current = current.Parent;
        }

        return null;
    }

    private void OnPreviewCardMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var card = FindPreviewCard(sender as Control);
        if (card?.Tag is not int index)
        {
            return;
        }

        _cardDragSourceIndex = index;
        _cardDragStartPoint = Control.MousePosition;
    }

    private void OnPreviewCardMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _cardDragSourceIndex < 0)
        {
            return;
        }

        var deltaX = Math.Abs(Control.MousePosition.X - _cardDragStartPoint.X);
        var deltaY = Math.Abs(Control.MousePosition.Y - _cardDragStartPoint.Y);
        if (deltaX < SystemInformation.DragSize.Width / 2 &&
            deltaY < SystemInformation.DragSize.Height / 2)
        {
            return;
        }

        var card = FindPreviewCard(sender as Control);
        if (card is null)
        {
            return;
        }

        var data = new DataObject();
        data.SetData(PreviewReorderDataFormat, _cardDragSourceIndex);
        card.DoDragDrop(data, DragDropEffects.Move);
        _cardDragSourceIndex = -1;
    }

    private void OnPreviewCardMouseUp(object? sender, MouseEventArgs e)
    {
        _ = sender;
        _ = e;
        _cardDragSourceIndex = -1;
    }

    private static bool TryGetDroppedPaths(DragEventArgs e, out string[] paths)
    {
        paths = Array.Empty<string>();
        var dataObject = e.Data;
        if (dataObject is null || !dataObject.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        if (dataObject.GetData(DataFormats.FileDrop) is not string[] dropped || dropped.Length == 0)
        {
            return false;
        }

        paths = dropped;
        return true;
    }

    private void AddFilesFromPaths(IEnumerable<string> paths)
    {
        var operation = CurrentOperation();
        var allowMultiple = UsesMultiInput(operation);
        var candidates = paths
            .Where(File.Exists)
            .Where(IsSupportedByCurrentOperation)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            _statusLabel.Text = operation == PdfToolOperation.ImagesToPdf
                ? "Nenhuma imagem válida detectada no drag and drop."
                : "Nenhum PDF válido detectado no drag and drop.";
            return;
        }

        if (!allowMultiple)
        {
            DisposeAndClearSelectedFiles();
            candidates = candidates.Take(1).ToList();
        }

        var existing = _selectedFiles.Select(x => x.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var path in candidates)
        {
            if (existing.Contains(path))
            {
                continue;
            }

            try
            {
                var item = new SelectedFileItem
                {
                    FullPath = path,
                    DisplayName = Path.GetFileName(path),
                    Thumbnail = BuildThumbnailForFile(path)
                };
                _selectedFiles.Add(item);
                existing.Add(path);
                added++;
            }
            catch
            {
                // ignore unreadable files
            }
        }

        if (added > 0)
        {
            ResetGeneratedResult();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {added} arquivo(s) adicionado(s) via drag and drop.");
            if (!allowMultiple && candidates.Count > 1)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] Esta ferramenta usa apenas 1 arquivo. Somente o primeiro foi considerado.");
            }
        }

        RefreshFilesView();
        UpdateUiState();
    }

    private bool IsSupportedByCurrentOperation(string path)
    {
        var operation = CurrentOperation();
        return operation == PdfToolOperation.ImagesToPdf ? IsImageFile(path) : IsPdfFile(path);
    }

    private void ConfigureSurfacePanel(Panel panel, int radius)
    {
        StyleManager.ConfigureSurfacePanel(panel, () => _shellBorderColor, radius, shadowAlpha: 28);
    }

    private static Label CreateStepLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(UiFontFamily, 11.25F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Tag = "title"
        };
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextMuted,
            Tag = "muted"
        };
    }

    private ComboBox CreateComboBox()
    {
        var comboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = GetActiveTheme().InputBackground,
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill
        };
        comboBox.Enter += (_, _) =>
        {
            if (!comboBox.IsDisposed)
            {
                comboBox.BackColor = GetActiveTheme().InputFocusBackground;
            }
        };
        comboBox.Leave += (_, _) =>
        {
            if (!comboBox.IsDisposed)
            {
                comboBox.BackColor = GetActiveTheme().InputBackground;
            }
        };
        return comboBox;
    }

    private TextBox CreateTextBox(string initialValue)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = initialValue,
            BackColor = GetActiveTheme().InputBackground,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };
        textBox.TextChanged += (_, _) => UpdateUiState();
        textBox.Enter += (_, _) =>
        {
            if (!textBox.IsDisposed)
            {
                textBox.BackColor = GetActiveTheme().InputFocusBackground;
            }
        };
        textBox.Leave += (_, _) =>
        {
            if (!textBox.IsDisposed)
            {
                textBox.BackColor = GetActiveTheme().InputBackground;
            }
        };
        return textBox;
    }

    private static Button CreatePrimaryButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 42,
            BackColor = AccentOrange,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(UiFontFamily, 10F, FontStyle.Bold),
            Margin = new Padding(0, 0, 10, 0),
            Tag = "primary-button"
        };
        StyleManager.ConfigurePrimaryButton(button, DarkTheme, AccentOrange);
        button.Resize += (_, _) => StyleManager.ApplyRoundedRegion(button, 9);
        StyleManager.ApplyRoundedRegion(button, 9);
        return button;
    }

    private static Button CreateSecondaryButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 40,
            BackColor = Color.FromArgb(24, 35, 51),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(UiFontFamily, 9.75F, FontStyle.Bold),
            Margin = new Padding(0, 0, 10, 0),
            Tag = "secondary-button"
        };
        StyleManager.ConfigureSecondaryButton(button, DarkTheme);
        button.Resize += (_, _) => StyleManager.ApplyRoundedRegion(button, 9);
        StyleManager.ApplyRoundedRegion(button, 9);
        return button;
    }

    private Control CreateOptionRow(string label, Control input)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 2),
            MinimumSize = new Size(0, 40)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        input.Margin = new Padding(0);
        input.Dock = DockStyle.Fill;
        row.Controls.Add(CreateFieldLabel(label), 0, 0);
        row.Controls.Add(input, 1, 0);
        return row;
    }

    private Control RegisterOptionRow(string key, Control row)
    {
        _optionRows[key] = row;
        return row;
    }

    private PdfToolOperation CurrentOperation()
    {
        return _operationCombo.SelectedItem is OperationItem item ? item.Operation : PdfToolOperation.Merge;
    }

    private static bool UsesMultiInput(PdfToolOperation operation)
    {
        return operation is PdfToolOperation.Merge or PdfToolOperation.ImagesToPdf;
    }

    private static bool NeedsQpdf(PdfToolOperation operation)
    {
        return operation is PdfToolOperation.Compress or PdfToolOperation.Protect or PdfToolOperation.Unlock or PdfToolOperation.Repair;
    }

    private bool IsSplitEachMode()
    {
        return CurrentOperation() == PdfToolOperation.Split && _splitModeCombo.SelectedIndex == 1;
    }

    private async Task OnOperationChangedAsync()
    {
        NormalizeSelectedFilesForOperation();
        ResetGeneratedResult();
        UpdateUiState();
        await RefreshQpdfStatusAsync();
    }

    private void NormalizeSelectedFilesForOperation()
    {
        var operation = CurrentOperation();
        var expectImages = operation == PdfToolOperation.ImagesToPdf;

        for (var index = _selectedFiles.Count - 1; index >= 0; index--)
        {
            var file = _selectedFiles[index];
            var valid = expectImages ? IsImageFile(file.FullPath) : IsPdfFile(file.FullPath);
            if (!valid)
            {
                file.Thumbnail.Dispose();
                _selectedFiles.RemoveAt(index);
            }
        }

        if (!UsesMultiInput(operation) && _selectedFiles.Count > 1)
        {
            while (_selectedFiles.Count > 1)
            {
                var toRemove = _selectedFiles[^1];
                toRemove.Thumbnail.Dispose();
                _selectedFiles.RemoveAt(_selectedFiles.Count - 1);
            }

            AppendLog($"[{DateTime.Now:HH:mm:ss}] Operação atual usa apenas um arquivo. Mantido somente o primeiro.");
        }

        RefreshFilesView();
    }

    private void AddFiles()
    {
        var operation = CurrentOperation();
        var allowMultiple = UsesMultiInput(operation);
        var isImages = operation == PdfToolOperation.ImagesToPdf;

        using var picker = new OpenFileDialog
        {
            Title = isImages ? "Selecione as imagens" : "Selecione os arquivos PDF",
            Filter = isImages
                ? "Imagens (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
                : "Arquivos PDF (*.pdf)|*.pdf",
            Multiselect = allowMultiple,
            CheckFileExists = true
        };

        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        AddFilesFromPaths(picker.FileNames);
    }

    private void RemoveSelectedFile()
    {
        if (_filesListView.SelectedIndices.Count == 0)
        {
            return;
        }

        var index = _filesListView.SelectedIndices[0];
        if (index < 0 || index >= _selectedFiles.Count)
        {
            return;
        }

        var file = _selectedFiles[index];
        file.Thumbnail.Dispose();
        _selectedFiles.RemoveAt(index);
        ResetGeneratedResult();
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Removido: {file.DisplayName}");
        RefreshFilesView();
        UpdateUiState();
    }

    private void ClearFiles()
    {
        if (_selectedFiles.Count == 0)
        {
            return;
        }

        DisposeAndClearSelectedFiles();
        ResetGeneratedResult();
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Lista de arquivos limpa.");
        RefreshFilesView();
        UpdateUiState();
    }

    private void DisposeAndClearSelectedFiles()
    {
        foreach (var file in _selectedFiles)
        {
            file.Thumbnail.Dispose();
        }

        _selectedFiles.Clear();
    }

    private void RefreshFilesView()
    {
        _filesListView.BeginUpdate();
        _filesListView.Items.Clear();
        foreach (var file in _selectedFiles)
        {
            _filesListView.Items.Add(new ListViewItem(file.DisplayName));
        }
        _filesListView.EndUpdate();
        UpdateFileColumnWidth();
        RefreshPreviewCards();
    }

    private void UpdateFileColumnWidth()
    {
        if (_fileNameColumn is null || _filesListView is null)
        {
            return;
        }

        var width = Math.Max(120, _filesListView.ClientSize.Width - 6);
        _fileNameColumn.Width = width;
    }

    private void RefreshPreviewCards()
    {
        _previewFlow.SuspendLayout();
        _previewFlow.Controls.Clear();

        if (_selectedFiles.Count == 0)
        {
            _previewFlow.Controls.Add(_previewEmptyLabel);
            _previewFlow.ResumeLayout();
            return;
        }

        for (var index = 0; index < _selectedFiles.Count; index++)
        {
            _previewFlow.Controls.Add(CreatePreviewCard(_selectedFiles[index], index));
        }

        _previewFlow.ResumeLayout();
        ApplyTheme();
    }

    private Control CreatePreviewCard(SelectedFileItem file, int index)
    {
        var card = new Panel
        {
            Width = 160,
            Height = 176,
            Margin = new Padding(0, 0, 10, 10),
            Padding = new Padding(8),
            BackColor = Color.FromArgb(24, 34, 52),
            Name = "preview-card",
            Tag = index
        };

        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = card.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            using var shapePath = StyleManager.CreateRoundedPath(rect, 8);
            using var pen = new Pen(_shellBorderColor, 1F);
            e.Graphics.DrawPath(pen, shapePath);
        };
        card.Resize += (_, _) => StyleManager.ApplyRoundedRegion(card, 8);
        StyleManager.ApplyRoundedRegion(card, 8);

        var imageBox = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 120,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = file.Thumbnail,
            BackColor = Color.Transparent
        };
        card.Controls.Add(imageBox);

        var nameLabel = new Label
        {
            Text = file.DisplayName,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.TopCenter,
            ForeColor = TextSecondary,
            Font = new Font(UiFontFamily, 8.7F, FontStyle.Regular),
            Tag = "secondary"
        };
        card.Controls.Add(nameLabel);
        card.Controls.SetChildIndex(imageBox, 0);
        card.Controls.SetChildIndex(nameLabel, 1);

        ConfigureDropTarget(card);
        ConfigureDropTarget(imageBox);
        ConfigureDropTarget(nameLabel);
        card.MouseDown += OnPreviewCardMouseDown;
        card.MouseMove += OnPreviewCardMouseMove;
        card.MouseUp += OnPreviewCardMouseUp;
        imageBox.MouseDown += OnPreviewCardMouseDown;
        imageBox.MouseMove += OnPreviewCardMouseMove;
        imageBox.MouseUp += OnPreviewCardMouseUp;
        nameLabel.MouseDown += OnPreviewCardMouseDown;
        nameLabel.MouseMove += OnPreviewCardMouseMove;
        nameLabel.MouseUp += OnPreviewCardMouseUp;

        return card;
    }

    private static bool IsPdfFile(string filePath)
    {
        return string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private Image BuildThumbnailForFile(string filePath)
    {
        if (IsImageFile(filePath))
        {
            return BuildImageThumbnail(filePath);
        }

        if (IsPdfFile(filePath))
        {
            return BuildPdfThumbnail(filePath);
        }

        return BuildPlaceholderThumbnail("ARQ", "arquivo", Path.GetExtension(filePath).Trim('.').ToUpperInvariant());
    }

    private static Image BuildImageThumbnail(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var original = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
        return BuildFittedThumbnail(original);
    }

    private Image BuildPdfThumbnail(string filePath)
    {
        using var renderedThumb = TryRenderPdfThumbnail(filePath, 360);
        if (renderedThumb is not null)
        {
            return BuildFittedThumbnail(renderedThumb);
        }

        using var shellThumb = TryGetShellThumbnail(filePath, 256);
        if (shellThumb is not null)
        {
            return BuildFittedThumbnail(shellThumb);
        }

        var pageCount = TryGetPdfPageCount(filePath);
        var subtitle = pageCount > 0 ? $"{pageCount} página(s)" : "documento PDF";
        return BuildPlaceholderThumbnail("PDF", subtitle, "pré-visualização indisponível");
    }

    private static Image? TryRenderPdfThumbnail(string filePath, int maxDimension)
    {
        try
        {
            var dimensions = new PageDimensions(maxDimension, maxDimension);
            using var docReader = DocLib.Instance.GetDocReader(filePath, dimensions);
            if (docReader.GetPageCount() <= 0)
            {
                return null;
            }

            using var pageReader = docReader.GetPageReader(0);
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var bytes = pageReader.GetImage();
            if (bytes.Length == 0)
            {
                return null;
            }

            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                var bytesPerLine = width * 4;
                if (data.Stride == bytesPerLine)
                {
                    Marshal.Copy(bytes, 0, data.Scan0, Math.Min(bytes.Length, Math.Abs(data.Stride) * height));
                }
                else
                {
                    for (var row = 0; row < height; row++)
                    {
                        var sourceOffset = row * bytesPerLine;
                        if (sourceOffset + bytesPerLine > bytes.Length)
                        {
                            break;
                        }

                        var target = IntPtr.Add(data.Scan0, row * data.Stride);
                        Marshal.Copy(bytes, sourceOffset, target, bytesPerLine);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static Image BuildFittedThumbnail(Image original)
    {
        var bitmap = new Bitmap(136, 110);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(10, 15, 23));
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var ratio = Math.Min(132f / original.Width, 106f / original.Height);
        var drawWidth = (int)(original.Width * ratio);
        var drawHeight = (int)(original.Height * ratio);
        var left = (bitmap.Width - drawWidth) / 2;
        var top = (bitmap.Height - drawHeight) / 2;
        var target = new Rectangle(left, top, drawWidth, drawHeight);

        graphics.DrawImage(original, target);
        using var borderPen = new Pen(Color.FromArgb(124, 95, 128, 162), 1F);
        graphics.DrawRectangle(borderPen, new Rectangle(0, 0, bitmap.Width - 1, bitmap.Height - 1));
        return bitmap;
    }

    private static Image BuildPlaceholderThumbnail(string title, string subtitle, string footer)
    {
        var bitmap = new Bitmap(136, 110);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            Color.FromArgb(22, 45, 62),
            Color.FromArgb(15, 24, 35),
            LinearGradientMode.ForwardDiagonal);
        graphics.FillRectangle(brush, 0, 0, bitmap.Width, bitmap.Height);

        using var borderPen = new Pen(Color.FromArgb(120, 95, 128, 162), 1F);
        graphics.DrawRectangle(borderPen, new Rectangle(0, 0, bitmap.Width - 1, bitmap.Height - 1));

        using var titleFont = new Font(UiFontFamily, 15.5F, FontStyle.Bold);
        using var subtitleFont = new Font(UiFontFamily, 8.25F, FontStyle.Regular);
        using var footerFont = new Font(UiFontFamily, 8F, FontStyle.Italic);
        using var whiteBrush = new SolidBrush(Color.White);
        using var mutedBrush = new SolidBrush(Color.FromArgb(205, 225, 255));

        var titleSize = graphics.MeasureString(title, titleFont);
        graphics.DrawString(title, titleFont, whiteBrush, (bitmap.Width - titleSize.Width) / 2, 14);
        graphics.DrawString(subtitle, subtitleFont, mutedBrush, 10, 56);
        graphics.DrawString(footer, footerFont, mutedBrush, 10, 78);
        return bitmap;
    }

    private static int TryGetPdfPageCount(string filePath)
    {
        try
        {
            using var pdf = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
            return pdf.PageCount;
        }
        catch
        {
            return 0;
        }
    }

    private static Image? TryGetShellThumbnail(string filePath, int edgeSize)
    {
        object? shellItem = null;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            var hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref iid, out shellItem);
            if (hr != 0 || shellItem is not IShellItemImageFactory imageFactory)
            {
                return null;
            }

            var size = new NativeSize { cx = edgeSize, cy = edgeSize };
            hr = imageFactory.GetImage(size, SIIGBF.THUMBNAILONLY | SIIGBF.BIGGERSIZEOK, out var hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                using var shellBitmap = Image.FromHbitmap(hBitmap);
                return new Bitmap(shellBitmap);
            }
            finally
            {
                _ = DeleteObject(hBitmap);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (shellItem is not null && Marshal.IsComObject(shellItem))
            {
                _ = Marshal.ReleaseComObject(shellItem);
            }
        }
    }

    private async Task RefreshQpdfStatusAsync()
    {
        var operation = CurrentOperation();
        if (!NeedsQpdf(operation))
        {
            SetQpdfStatus("QPDF: não necessário nesta operação.", QpdfState.Neutral);
            return;
        }

        _qpdfCheckInProgress = true;
        SetQpdfStatus("QPDF: verificando instalação...", QpdfState.Checking);
        UpdateUiState();

        var status = await _service.GetQpdfStatusAsync(CancellationToken.None);
        if (status.Installed)
        {
            SetQpdfStatus($"QPDF: OK ({status.BinaryPath}).", QpdfState.Ok);
        }
        else
        {
            SetQpdfStatus($"QPDF: indisponível. {status.Reason}", QpdfState.Error);
        }

        _qpdfCheckInProgress = false;
        UpdateUiState();
    }

    private void SetQpdfStatus(string message, QpdfState state)
    {
        _qpdfState = state;
        _qpdfStatusLabel.Text = message;
        _qpdfStatusLabel.ForeColor = ResolveQpdfColor(GetActiveTheme());
    }

    private bool HasRequiredOptions(PdfToolOperation operation, bool splitEachMode)
    {
        return operation switch
        {
            PdfToolOperation.Split when !splitEachMode => !string.IsNullOrWhiteSpace(_rangesText.Text),
            PdfToolOperation.Extract => !string.IsNullOrWhiteSpace(_rangesText.Text),
            PdfToolOperation.Remove => !string.IsNullOrWhiteSpace(_pagesText.Text),
            PdfToolOperation.Reorder => !string.IsNullOrWhiteSpace(_orderText.Text),
            PdfToolOperation.Watermark => !string.IsNullOrWhiteSpace(_watermarkText.Text),
            PdfToolOperation.Protect => !string.IsNullOrWhiteSpace(_protectOwnerText.Text),
            _ => true
        };
    }

    private void UpdateUiState()
    {
        var operation = CurrentOperation();
        var useMultiInput = UsesMultiInput(operation);
        var splitEachMode = operation == PdfToolOperation.Split && IsSplitEachMode();
        var showAdvanced = _showAdvancedToggle.Checked;

        _optionsPanel.SuspendLayout();
        foreach (Control child in _optionsPanel.Controls)
        {
            child.SuspendLayout();
        }

        try
        {
            _optionRows["split_mode"].Visible = operation == PdfToolOperation.Split;
            _optionRows["ranges"].Visible = (operation == PdfToolOperation.Split && !splitEachMode) || operation == PdfToolOperation.Extract;
            _optionRows["prefix"].Visible = operation == PdfToolOperation.Split && splitEachMode;
            _optionRows["pages"].Visible = operation == PdfToolOperation.Remove || (operation == PdfToolOperation.Rotate && showAdvanced);
            _optionRows["rotate_degrees"].Visible = operation == PdfToolOperation.Rotate;
            _optionRows["order"].Visible = operation == PdfToolOperation.Reorder;
            _optionRows["watermark_text"].Visible = operation == PdfToolOperation.Watermark;
            _optionRows["watermark_size"].Visible = operation == PdfToolOperation.Watermark && showAdvanced;
            _optionRows["watermark_opacity"].Visible = operation == PdfToolOperation.Watermark && showAdvanced;
            _optionRows["number_start"].Visible = operation == PdfToolOperation.PageNumbers && showAdvanced;
            _optionRows["number_position"].Visible = operation == PdfToolOperation.PageNumbers && showAdvanced;
            _optionRows["number_font"].Visible = operation == PdfToolOperation.PageNumbers && showAdvanced;
            _optionRows["protect_owner"].Visible = operation == PdfToolOperation.Protect;
            _optionRows["protect_user"].Visible = operation == PdfToolOperation.Protect && showAdvanced;
            _optionRows["unlock_password"].Visible = operation == PdfToolOperation.Unlock && showAdvanced;
            _optionsPanel.Visible = _optionRows.Values.Any(control => control.Visible);
        }
        finally
        {
            foreach (Control child in _optionsPanel.Controls)
            {
                child.ResumeLayout(performLayout: true);
            }

            _optionsPanel.ResumeLayout(performLayout: true);
        }

        _addFilesButton.Text = useMultiInput ? "Adicionar / Arrastar" : "Adicionar Arquivo";
        _dropHintLabel.Text = operation == PdfToolOperation.ImagesToPdf
            ? "Arraste e solte imagens aqui (PNG/JPG/JPEG)"
            : useMultiInput
                ? "Arraste e solte vários PDFs aqui"
                : "Arraste e solte 1 PDF aqui";

        var fileCount = _selectedFiles.Count;
        var minFiles = operation == PdfToolOperation.Merge ? 2 : 1;
        var validFileCount = useMultiInput ? fileCount >= minFiles : fileCount == 1;
        var validOptions = HasRequiredOptions(operation, splitEachMode);

        _addFilesButton.Enabled = !_isRunning;
        _removeSelectedButton.Enabled = !_isRunning && _filesListView.SelectedIndices.Count > 0;
        _clearFilesButton.Enabled = !_isRunning && fileCount > 0;
        _operationCombo.Enabled = !_isRunning;
        _showAdvancedToggle.Enabled = !_isRunning;
        _processButton.Enabled = !_isRunning && validFileCount && validOptions && !_qpdfCheckInProgress;
        _downloadButton.Enabled = !_isRunning && _generatedOutputPaths.Count > 0;

        SetOptionControlsEnabled(!_isRunning);
        UpdateHint(operation, useMultiInput, fileCount, splitEachMode, validOptions);
    }

    private void SetOptionControlsEnabled(bool enabled)
    {
        _splitModeCombo.Enabled = enabled;
        _rangesText.Enabled = enabled;
        _pagesText.Enabled = enabled;
        _rotateDegreesCombo.Enabled = enabled;
        _orderText.Enabled = enabled;
        _prefixText.Enabled = enabled;
        _watermarkText.Enabled = enabled;
        _watermarkFontSizeText.Enabled = enabled;
        _watermarkOpacityText.Enabled = enabled;
        _pageNumberStartText.Enabled = enabled;
        _pageNumberPositionCombo.Enabled = enabled;
        _pageNumberFontSizeText.Enabled = enabled;
        _protectOwnerText.Enabled = enabled;
        _protectUserText.Enabled = enabled;
        _unlockPasswordText.Enabled = enabled;
    }

    private void UpdateHint(PdfToolOperation operation, bool useMultiInput, int fileCount, bool splitEachMode, bool validOptions)
    {
        if (!useMultiInput && fileCount > 1)
        {
            _hintLabel.Text = "Esta ferramenta usa apenas 1 arquivo. Remova os extras para continuar.";
            return;
        }

        if (!validOptions)
        {
            _hintLabel.Text = operation switch
            {
                PdfToolOperation.Split when !splitEachMode => "Informe os intervalos de divisão (ex: 1-3,5).",
                PdfToolOperation.Extract => "Informe as páginas para extração (ex: 1-3,5).",
                PdfToolOperation.Remove => "Informe as páginas para remover (ex: 2,4).",
                PdfToolOperation.Reorder => "Informe a nova ordem (ex: 3,1,2,4).",
                PdfToolOperation.Watermark => "Informe o texto da marca d'água.",
                PdfToolOperation.Protect => "Informe a senha de proprietário.",
                _ => "Complete os campos obrigatorios."
            };
            return;
        }

        _hintLabel.Text = operation switch
        {
            PdfToolOperation.Merge => "Anexe ou arraste ao menos 2 PDFs para juntar.",
            PdfToolOperation.Split when splitEachMode => "Divisão página por página. O download será em lote.",
            PdfToolOperation.Split => "Divisão por intervalos configurada.",
            PdfToolOperation.Extract => "Extração pronta para as páginas informadas.",
            PdfToolOperation.Remove => "Remoção pronta para as páginas informadas.",
            PdfToolOperation.Rotate => "Rotação pronta. Use avançado para limitar páginas.",
            PdfToolOperation.Reorder => "Reordenação pronta com a ordem informada.",
            PdfToolOperation.Watermark => "Marca d'água pronta para aplicação.",
            PdfToolOperation.PageNumbers => "Numeração pronta com configuração padrão.",
            PdfToolOperation.ImagesToPdf => "Anexe ou arraste uma ou mais imagens para gerar PDF.",
            PdfToolOperation.Compress => "Compressão pronta (QPDF).",
            PdfToolOperation.Protect => "Proteção pronta com senha de proprietário.",
            PdfToolOperation.Unlock => "Desbloqueio pronto (a senha atual é opcional).",
            PdfToolOperation.Repair => "Reparo pronto para execução com QPDF.",
            _ => "Processamento local e offline."
        };
    }

    private async Task ProcessCurrentOperationAsync()
    {
        if (_isRunning)
        {
            return;
        }

        var operation = CurrentOperation();
        var useMultiInput = UsesMultiInput(operation);
        var minFiles = operation == PdfToolOperation.Merge ? 2 : 1;

        if (useMultiInput && _selectedFiles.Count < minFiles)
        {
            MessageBox.Show(this, $"Adicione ao menos {minFiles} arquivo(s).", "JuntarPDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!useMultiInput && _selectedFiles.Count != 1)
        {
            MessageBox.Show(this, "Esta ferramenta exige exatamente 1 arquivo.", "JuntarPDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (NeedsQpdf(operation))
        {
            var qpdfStatus = await _service.GetQpdfStatusAsync(CancellationToken.None);
            if (!qpdfStatus.Installed)
            {
                SetQpdfStatus($"QPDF: indisponível. {qpdfStatus.Reason}", QpdfState.Error);
                MessageBox.Show(this, "QPDF não encontrado para esta operação.", "JuntarPDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetQpdfStatus($"QPDF: OK ({qpdfStatus.BinaryPath}).", QpdfState.Ok);
        }

        PdfToolRequest request;
        try
        {
            request = BuildRequest();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "JuntarPDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isRunning = true;
        _generatedOutputPaths = new List<string>();
        _downloadButton.Enabled = false;
        _progressBar.Value = 0;
        _statusLabel.Text = "Processando arquivos...";
        UpdateUiState();
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Inicio: {operation}");

        try
        {
            var progress = new Progress<PdfToolProgress>(entry =>
            {
                _progressBar.Value = Math.Clamp(entry.Percent, 0, 100);
                _statusLabel.Text = entry.Message;
                AppendLog($"[{DateTime.Now:HH:mm:ss}] {entry.Message}");
            });

            var result = await _service.ExecuteAsync(request, progress, CancellationToken.None);
            _generatedOutputPaths = result.OutputPaths.ToList();
            _progressBar.Value = 100;
            _statusLabel.Text = $"{result.Message} Clique em Download Resultado para escolher onde salvar.";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {result.Message}");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Falha ao processar arquivos.";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] Erro: {ex.Message}");
            MessageBox.Show(this, ex.Message, "JuntarPDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isRunning = false;
            UpdateUiState();
        }
    }

    private PdfToolRequest BuildRequest()
    {
        var operation = CurrentOperation();
        var splitEach = operation == PdfToolOperation.Split && IsSplitEachMode();
        var selectedPaths = _selectedFiles.Select(x => x.FullPath).ToList();
        var workspace = PrepareWorkspace();

        var outputPath = splitEach
            ? string.Empty
            : Path.Combine(workspace, BuildTemporaryOutputFileName(operation, selectedPaths.FirstOrDefault() ?? string.Empty));

        var outputDirectory = splitEach
            ? Path.Combine(workspace, "split-output")
            : string.Empty;

        if (splitEach)
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var position = _pageNumberPositionCombo.SelectedIndex switch
        {
            1 => PdfPageNumberPosition.BottomLeft,
            2 => PdfPageNumberPosition.TopRight,
            3 => PdfPageNumberPosition.TopLeft,
            4 => PdfPageNumberPosition.Center,
            _ => PdfPageNumberPosition.BottomRight
        };

        return new PdfToolRequest
        {
            Operation = operation,
            InputPaths = UsesMultiInput(operation) ? selectedPaths : Array.Empty<string>(),
            InputPath = UsesMultiInput(operation) ? string.Empty : selectedPaths[0],
            OutputPath = outputPath,
            OutputDirectory = outputDirectory,
            Prefix = _prefixText.Text.Trim(),
            SplitMode = splitEach ? PdfSplitMode.Each : PdfSplitMode.Ranges,
            Ranges = _rangesText.Text.Trim(),
            Pages = _pagesText.Text.Trim(),
            RotateDegrees = int.TryParse(_rotateDegreesCombo.Text, out var degree) ? degree : 90,
            Order = _orderText.Text.Trim(),
            WatermarkText = _watermarkText.Text.Trim(),
            WatermarkFontSize = ParseDouble(_watermarkFontSizeText.Text, 42),
            WatermarkOpacity = ParseDouble(_watermarkOpacityText.Text, 0.25),
            PageNumberStart = ParseInt(_pageNumberStartText.Text, 1),
            PageNumberPosition = position,
            PageNumberFontSize = ParseDouble(_pageNumberFontSizeText.Text, 11),
            OwnerPassword = _protectOwnerText.Text,
            UserPassword = _protectUserText.Text,
            Password = _unlockPasswordText.Text
        };
    }

    private string PrepareWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "GuguSolucoes", "PdfSuite");
        Directory.CreateDirectory(root);
        _workspaceDirectory = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_workspaceDirectory);
        return _workspaceDirectory;
    }

    private static string BuildTemporaryOutputFileName(PdfToolOperation operation, string sourcePath)
    {
        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            sourceName = "documento";
        }

        var suffix = operation switch
        {
            PdfToolOperation.Merge => "unido",
            PdfToolOperation.Split => "dividido",
            PdfToolOperation.Extract => "extraido",
            PdfToolOperation.Remove => "ajustado",
            PdfToolOperation.Rotate => "rotacionado",
            PdfToolOperation.Reorder => "reordenado",
            PdfToolOperation.Watermark => "marca",
            PdfToolOperation.PageNumbers => "numerado",
            PdfToolOperation.ImagesToPdf => "imagens",
            PdfToolOperation.Compress => "comprimido",
            PdfToolOperation.Protect => "protegido",
            PdfToolOperation.Unlock => "desbloqueado",
            PdfToolOperation.Repair => "reparado",
            _ => "saída"
        };

        return $"{sourceName}-{suffix}.pdf";
    }

    private static double ParseDouble(string text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static int ParseInt(string text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private void DownloadResult()
    {
        if (_generatedOutputPaths.Count == 0)
        {
            MessageBox.Show(this, "Nenhum resultado pronto para download. Execute a ferramenta primeiro.", "JuntarPDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            if (_generatedOutputPaths.Count == 1)
            {
                DownloadSingleOutput(_generatedOutputPaths[0]);
            }
            else
            {
                DownloadMultipleOutputs(_generatedOutputPaths);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "JuntarPDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DownloadSingleOutput(string sourcePath)
    {
        var defaultName = Path.GetFileName(sourcePath);
        using var picker = new SaveFileDialog
        {
            Title = "Salvar resultado",
            Filter = "Arquivos PDF (*.pdf)|*.pdf|Todos os arquivos (*.*)|*.*",
            FileName = defaultName,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            AddExtension = true,
            OverwritePrompt = true
        };

        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.Copy(sourcePath, picker.FileName, overwrite: true);
        _statusLabel.Text = "Download concluído com sucesso.";
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Arquivo salvo: {Path.GetFileName(picker.FileName)}");
    }

    private void DownloadMultipleOutputs(IReadOnlyList<string> sourcePaths)
    {
        using var picker = new FolderBrowserDialog
        {
            Description = "Escolha a pasta para salvar os arquivos",
            SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var copied = 0;
        foreach (var sourcePath in sourcePaths)
        {
            var target = Path.Combine(picker.SelectedPath, Path.GetFileName(sourcePath));
            var safeTarget = BuildSafeTargetPath(target);
            File.Copy(sourcePath, safeTarget, overwrite: false);
            copied++;
        }

        _statusLabel.Text = $"Download concluído com {copied} arquivo(s).";
        AppendLog($"[{DateTime.Now:HH:mm:ss}] {copied} arquivo(s) salvos na pasta escolhida.");
    }

    private static string BuildSafeTargetPath(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        var index = 2;

        while (true)
        {
            var candidate = Path.Combine(directory, $"{fileName} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private void AppendLog(string line)
    {
        _ = line;
    }

    private void ResetGeneratedResult()
    {
        _generatedOutputPaths = new List<string>();
        _downloadButton.Enabled = false;
    }

    private void SyncContentWidth()
    {
        if (_scrollViewport is null || _contentLayout is null)
        {
            return;
        }

        var scrollbarWidth = _scrollViewport.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
        var targetWidth = Math.Max(1, _scrollViewport.ClientSize.Width - scrollbarWidth - 1);
        if (_contentLayout.Width != targetWidth)
        {
            _contentLayout.Width = targetWidth;
        }
    }

    private ThemePalette GetActiveTheme()
    {
        return StyleManager.ResolveTheme(_themeToggle is not null && _themeToggle.Checked);
    }

    private void ApplyTheme()
    {
        SuspendLayout();
        _shellPanel?.SuspendLayout();
        _scrollViewport?.SuspendLayout();
        _contentLayout?.SuspendLayout();

        var theme = GetActiveTheme();
        try
        {
            BackColor = theme.Background;
            ForeColor = theme.TextPrimary;
            _shellBorderColor = theme.Border;

            if (_shellPanel is not null)
            {
                _shellPanel.BackColor = theme.Surface;
                _shellPanel.Invalidate();
            }

            ApplyThemeRecursive(this, theme);
            _qpdfStatusLabel.ForeColor = ResolveQpdfColor(theme);
            _filesListView.BackColor = theme.InputBackground;
            _filesListView.ForeColor = theme.TextPrimary;
        }
        finally
        {
            _contentLayout?.ResumeLayout(performLayout: true);
            _scrollViewport?.ResumeLayout(performLayout: true);
            _shellPanel?.ResumeLayout(performLayout: true);
            ResumeLayout(performLayout: true);
        }
    }

    private void ApplyThemeRecursive(Control root, ThemePalette theme)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case Label label:
                {
                    var role = label.Tag as string;
                    label.BackColor = Color.Transparent;
                    label.ForeColor = role switch
                    {
                        "title" => theme.TextPrimary,
                        "secondary" => theme.TextSecondary,
                        "muted" => theme.TextMuted,
                        "status" => ResolveQpdfColor(theme),
                        _ => theme.TextPrimary
                    };
                    break;
                }
                case TextBox textBox:
                    textBox.BackColor = textBox.Focused ? theme.InputFocusBackground : theme.InputBackground;
                    textBox.ForeColor = theme.TextPrimary;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = comboBox.Focused ? theme.InputFocusBackground : theme.InputBackground;
                    comboBox.ForeColor = theme.TextPrimary;
                    break;
                case ThemedProgressBar progressBar:
                    progressBar.TrackColor = theme.ProgressTrack;
                    progressBar.FillColor = theme.ProgressFill;
                    progressBar.BorderColor = theme.LogBorder;
                    break;
                case RichTextBox richTextBox:
                    richTextBox.BackColor = theme.InputBackground;
                    richTextBox.ForeColor = theme.TextPrimary;
                    break;
                case CheckBox checkBox:
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = theme.TextPrimary;
                    break;
                case Button button:
                {
                    var style = button.Tag as string;
                    if (style == "primary-button")
                    {
                        var accent = theme.Accent;
                        StyleManager.ConfigurePrimaryButton(button, theme, accent);
                    }
                    else
                    {
                        StyleManager.ConfigureSecondaryButton(button, theme);
                    }
                    break;
                }
                case FlowLayoutPanel:
                case TableLayoutPanel:
                    control.BackColor = Color.Transparent;
                    break;
                case Panel panel:
                    if (ReferenceEquals(panel, _shellPanel))
                    {
                        panel.BackColor = theme.Surface;
                    }
                    else if (string.Equals(panel.Name, "preview-card", StringComparison.OrdinalIgnoreCase))
                    {
                        panel.BackColor = theme.PreviewCardBackground;
                        panel.Invalidate();
                    }
                    else if (panel.Tag as string == "surface")
                    {
                        panel.BackColor = theme.InputBackground;
                        panel.Invalidate();
                    }
                    else
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

    private Color ResolveQpdfColor(ThemePalette theme)
    {
        return _qpdfState switch
        {
            QpdfState.Ok => Color.FromArgb(22, 163, 74),
            QpdfState.Error => Color.FromArgb(220, 38, 38),
            QpdfState.Checking => theme.TextMuted,
            _ => theme.TextSecondary
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int cx;
        public int cy;
    }

    [Flags]
    private enum SIIGBF
    {
        RESIZETOFIT = 0x0,
        BIGGERSIZEOK = 0x1,
        MEMORYONLY = 0x2,
        ICONONLY = 0x4,
        THUMBNAILONLY = 0x8,
        INCACHEONLY = 0x10
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, SIIGBF flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object? ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    internal void SetTheme(bool useLightTheme)
    {
        if (_themeToggle is null)
        {
            return;
        }

        if (_themeToggle.Checked != useLightTheme)
        {
            _themeToggle.Checked = useLightTheme;
            return;
        }

        ApplyTheme();
    }

    internal void SetThemeToggleVisible(bool visible)
    {
        if (_themeToggle is null)
        {
            return;
        }

        _themeToggle.Visible = visible;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeAndClearSelectedFiles();
        }

        base.Dispose(disposing);
    }
}




