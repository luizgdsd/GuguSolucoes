using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GuguSolucoes.Desktop.Infrastructure;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace GuguSolucoes.Desktop.Modules.PdfTools;

public enum PdfToolOperation
{
    Merge,
    Split,
    Extract,
    Remove,
    Rotate,
    Reorder,
    Watermark,
    PageNumbers,
    ImagesToPdf,
    Compress,
    Protect,
    Unlock,
    Repair
}

public enum PdfSplitMode
{
    Ranges,
    Each
}

public enum PdfPageNumberPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center
}

public sealed class PdfToolRequest
{
    public PdfToolOperation Operation { get; init; }
    public IReadOnlyList<string> InputPaths { get; init; } = Array.Empty<string>();
    public string InputPath { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public string OutputDirectory { get; init; } = string.Empty;
    public string Prefix { get; init; } = string.Empty;
    public PdfSplitMode SplitMode { get; init; } = PdfSplitMode.Ranges;
    public string Ranges { get; init; } = string.Empty;
    public string Pages { get; init; } = string.Empty;
    public int RotateDegrees { get; init; } = 90;
    public string Order { get; init; } = string.Empty;
    public string WatermarkText { get; init; } = string.Empty;
    public double WatermarkFontSize { get; init; } = 42;
    public double WatermarkOpacity { get; init; } = 0.25;
    public int PageNumberStart { get; init; } = 1;
    public PdfPageNumberPosition PageNumberPosition { get; init; } = PdfPageNumberPosition.BottomRight;
    public double PageNumberFontSize { get; init; } = 11;
    public string OwnerPassword { get; init; } = string.Empty;
    public string UserPassword { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed record PdfToolProgress(int Percent, string Message);

public sealed class PdfToolResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> OutputPaths { get; init; } = Array.Empty<string>();
}

public sealed class PdfQpdfStatus
{
    public bool Installed { get; init; }
    public string BinaryPath { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class PdfToolService
{
    private static readonly HashSet<int> SupportedRotations = new() { 90, 180, 270 };
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    private readonly AppLogger _logger;
    private string? _cachedQpdfBinary;

    public PdfToolService(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task<PdfToolResult> ExecuteAsync(
        PdfToolRequest request,
        IProgress<PdfToolProgress>? progress,
        CancellationToken cancellationToken)
    {
        return request.Operation switch
        {
            PdfToolOperation.Merge => await MergeAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Split => await SplitAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Extract => await ExtractAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Remove => await RemoveAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Rotate => await RotateAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Reorder => await ReorderAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Watermark => await WatermarkAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.PageNumbers => await PageNumbersAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.ImagesToPdf => await ImagesToPdfAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Compress => await CompressAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Protect => await ProtectAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Unlock => await UnlockAsync(request, progress, cancellationToken).ConfigureAwait(false),
            PdfToolOperation.Repair => await RepairAsync(request, progress, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Operação PDF não suportada.")
        };
    }

    public async Task<PdfQpdfStatus> GetQpdfStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var binary = await ResolveQpdfBinaryAsync(cancellationToken).ConfigureAwait(false);
            return new PdfQpdfStatus
            {
                Installed = true,
                BinaryPath = binary
            };
        }
        catch (Exception ex)
        {
            return new PdfQpdfStatus
            {
                Installed = false,
                Reason = ex.Message
            };
        }
    }

    private Task<PdfToolResult> MergeAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var sources = NormalizeInputPathList(request.InputPaths, "arquivos de entrada", 2, expectPdf: false);
            var destination = NormalizeOutputPdfPath(request.OutputPath);
            EnsureOutputNotInInputs(sources, destination);

            using var outputDocument = new PdfDocument();
            var totalPages = 0;

            for (var index = 0; index < sources.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = sources[index];
                EnsurePdfFile(source);

                using var inputDocument = PdfReader.Open(source, PdfDocumentOpenMode.Import);
                for (var page = 0; page < inputDocument.PageCount; page++)
                {
                    outputDocument.AddPage(inputDocument.Pages[page]);
                    totalPages++;
                }

                var percent = Percent(index + 1, sources.Count);
                progress?.Report(new PdfToolProgress(percent, $"Importado: {Path.GetFileName(source)}"));
                _logger.Info($"Juntar PDF: importado {Path.GetFileName(source)}.");
            }

            EnsureOutputDirectory(destination);
            outputDocument.Save(destination);

            var message = $"{sources.Count} PDF(s) unidos com sucesso. Total de páginas: {totalPages}.";
            return new PdfToolResult
            {
                Success = true,
                Message = message,
                OutputPaths = new[] { destination }
            };
        }, cancellationToken);
    }

    private Task<PdfToolResult> SplitAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var inputPath = NormalizeSingleInputPath(request.InputPath);
            EnsurePdfFile(inputPath);

            using var sourcePdf = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            if (request.SplitMode == PdfSplitMode.Each)
            {
                var outputDirectory = NormalizeOutputDirectory(request.OutputDirectory);
                var prefix = string.IsNullOrWhiteSpace(request.Prefix)
                    ? $"{Path.GetFileNameWithoutExtension(inputPath)}-pagina"
                    : request.Prefix.Trim();

                var outputs = new List<string>();
                for (var index = 0; index < sourcePdf.PageCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var pagePdf = new PdfDocument();
                    pagePdf.AddPage(sourcePdf.Pages[index]);
                    var output = Path.Combine(outputDirectory, $"{prefix}-{index + 1}.pdf");
                    EnsureOutputDirectory(output);
                    pagePdf.Save(output);
                    outputs.Add(output);
                    progress?.Report(new PdfToolProgress(Percent(index + 1, sourcePdf.PageCount), $"Gerado: {Path.GetFileName(output)}"));
                }

                return new PdfToolResult
                {
                    Success = true,
                    Message = $"{outputs.Count} arquivo(s) gerado(s) na divisão por página.",
                    OutputPaths = outputs
                };
            }

            var destination = NormalizeOutputPdfPath(request.OutputPath);
            EnsureDifferentInputAndOutput(inputPath, destination);
            var selectedPages = ParsePageExpression(request.Ranges, sourcePdf.PageCount, allowDuplicates: false);

            using var outputPdf = new PdfDocument();
            foreach (var pageIndex in selectedPages)
            {
                outputPdf.AddPage(sourcePdf.Pages[pageIndex]);
            }

            EnsureOutputDirectory(destination);
            outputPdf.Save(destination);

            progress?.Report(new PdfToolProgress(100, "PDF dividido com sucesso."));
            return new PdfToolResult
            {
                Success = true,
                Message = "PDF dividido com sucesso pelos intervalos informados.",
                OutputPaths = new[] { destination }
            };
        }, cancellationToken);
    }

    private Task<PdfToolResult> ExtractAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var inputPath = NormalizeSingleInputPath(request.InputPath);
            EnsurePdfFile(inputPath);
            var destination = NormalizeOutputPdfPath(request.OutputPath);
            EnsureDifferentInputAndOutput(inputPath, destination);

            using var sourcePdf = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            var selectedPages = ParsePageExpression(request.Ranges, sourcePdf.PageCount, allowDuplicates: false);

            using var outputPdf = new PdfDocument();
            foreach (var pageIndex in selectedPages)
            {
                outputPdf.AddPage(sourcePdf.Pages[pageIndex]);
            }

            EnsureOutputDirectory(destination);
            outputPdf.Save(destination);
            progress?.Report(new PdfToolProgress(100, "Páginas extraídas com sucesso."));

            return new PdfToolResult
            {
                Success = true,
                Message = "Páginas extraídas com sucesso.",
                OutputPaths = new[] { destination }
            };
        }, cancellationToken);
    }

    private Task<PdfToolResult> RemoveAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var inputPath = NormalizeSingleInputPath(request.InputPath);
            EnsurePdfFile(inputPath);
            var destination = NormalizeOutputPdfPath(request.OutputPath);
            EnsureDifferentInputAndOutput(inputPath, destination);

            using var sourcePdf = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            var remove = new HashSet<int>(ParsePageExpression(request.Pages, sourcePdf.PageCount, allowDuplicates: false));
            if (remove.Count >= sourcePdf.PageCount)
            {
                throw new InvalidOperationException("A remoção não pode excluir todas as páginas do documento.");
            }

            using var outputPdf = new PdfDocument();
            for (var index = 0; index < sourcePdf.PageCount; index++)
            {
                if (!remove.Contains(index))
                {
                    outputPdf.AddPage(sourcePdf.Pages[index]);
                }
            }

            EnsureOutputDirectory(destination);
            outputPdf.Save(destination);
            progress?.Report(new PdfToolProgress(100, "Páginas removidas com sucesso."));

            return new PdfToolResult
            {
                Success = true,
                Message = $"{remove.Count} página(s) removida(s) com sucesso.",
                OutputPaths = new[] { destination }
            };
        }, cancellationToken);
    }

    private Task<PdfToolResult> RotateAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var inputPath = NormalizeSingleInputPath(request.InputPath);
            EnsurePdfFile(inputPath);
            var destination = NormalizeOutputPdfPath(request.OutputPath);
            EnsureDifferentInputAndOutput(inputPath, destination);

            if (!SupportedRotations.Contains(request.RotateDegrees))
            {
                throw new InvalidOperationException("Rotação inválida. Use 90, 180 ou 270.");
            }

            using var sourcePdf = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            var targetPages = string.IsNullOrWhiteSpace(request.Pages)
                ? Enumerable.Range(0, sourcePdf.PageCount).ToList()
                : ParsePageExpression(request.Pages, sourcePdf.PageCount, allowDuplicates: false);
            var targetSet = new HashSet<int>(targetPages);

            using var outputPdf = new PdfDocument();
            for (var index = 0; index < sourcePdf.PageCount; index++)
            {
                var page = sourcePdf.Pages[index];
                var outputPage = outputPdf.AddPage(page);
                if (targetSet.Contains(index))
                {
                    outputPage.Rotate = NormalizeRotation(outputPage.Rotate + request.RotateDegrees);
                }
            }

            EnsureOutputDirectory(destination);
            outputPdf.Save(destination);
            progress?.Report(new PdfToolProgress(100, "Rotação aplicada com sucesso."));

            return new PdfToolResult
            {
                Success = true,
                Message = $"{targetSet.Count} página(s) rotacionada(s) com sucesso.",
                OutputPaths = new[] { destination }
            };
        }, cancellationToken);
    }

    private Task<PdfToolResult> ReorderAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var inputPath = NormalizeSingleInputPath(request.InputPath);
            EnsurePdfFile(inputPath);
            var destination = NormalizeOutputPdfPath(request.OutputPath);
            EnsureDifferentInputAndOutput(inputPath, destination);

            using var sourcePdf = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            var ordered = ParseOrderExpression(request.Order, sourcePdf.PageCount);

            using var outputPdf = new PdfDocument();
            foreach (var pageIndex in ordered)
            {
                outputPdf.AddPage(sourcePdf.Pages[pageIndex]);
            }

            EnsureOutputDirectory(destination);
            outputPdf.Save(destination);
            progress?.Report(new PdfToolProgress(100, "Ordem das páginas atualizada."));

            return new PdfToolResult
            {
                Success = true,
                Message = "Ordem das páginas atualizada com sucesso.",
                OutputPaths = new[] { destination }
            };
        }, cancellationToken);
    }

    private Task<PdfToolResult> WatermarkAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var inputPath = NormalizeSingleInputPath(request.InputPath);
            EnsurePdfFile(inputPath);
            var destination = NormalizeOutputPdfPath(request.OutputPath);
            EnsureDifferentInputAndOutput(inputPath, destination);

            var watermark = NormalizeRequiredText(request.WatermarkText, "texto da marca d'água");
            var fontSize = Clamp(request.WatermarkFontSize, 10, 180);
            var opacity = Clamp(request.WatermarkOpacity, 0.05, 1);
            var alpha = (int)Math.Round(255 * opacity);
            var color = XColor.FromArgb(alpha, 56, 94, 140);

            using var sourcePdf = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            using var outputPdf = new PdfDocument();
            var font = new XFont("Arial", fontSize, XFontStyle.Bold);

            for (var index = 0; index < sourcePdf.PageCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outputPage = outputPdf.AddPage(sourcePdf.Pages[index]);

                using var gfx = XGraphics.FromPdfPage(outputPage, XGraphicsPdfPageOptions.Append);
                var size = gfx.MeasureString(watermark, font);
                var centerX = outputPage.Width.Point / 2D;
                var centerY = outputPage.Height.Point / 2D;

                var state = gfx.Save();
                gfx.TranslateTransform(centerX, centerY);
                gfx.RotateTransform(-35);
                gfx.DrawString(
                    watermark,
                    font,
                    new XSolidBrush(color),
                    new XPoint(-size.Width / 2D, size.Height / 2D),
                    XStringFormats.TopLeft);
                gfx.Restore(state);

                progress?.Report(new PdfToolProgress(Percent(index + 1, sourcePdf.PageCount), $"Marca d'água aplicada na página {index + 1}."));
            }

            EnsureOutputDirectory(destination);
            outputPdf.Save(destination);

            return new PdfToolResult
            {
                Success = true,
                Message = "Marca d'água aplicada com sucesso.",
                OutputPaths = new[] { destination }
            };
        }, cancellationToken);
    }

    private Task<PdfToolResult> PageNumbersAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var inputPath = NormalizeSingleInputPath(request.InputPath);
            EnsurePdfFile(inputPath);
            var destination = NormalizeOutputPdfPath(request.OutputPath);
            EnsureDifferentInputAndOutput(inputPath, destination);

            var startNumber = request.PageNumberStart <= 0 ? 1 : request.PageNumberStart;
            var fontSize = Clamp(request.PageNumberFontSize, 8, 48);

            using var sourcePdf = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            using var outputPdf = new PdfDocument();
            var font = new XFont("Arial", fontSize, XFontStyle.Regular);
            var brush = new XSolidBrush(XColor.FromArgb(245, 240, 246, 255));

            for (var index = 0; index < sourcePdf.PageCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outputPage = outputPdf.AddPage(sourcePdf.Pages[index]);
                using var gfx = XGraphics.FromPdfPage(outputPage, XGraphicsPdfPageOptions.Append);

                var numberText = (startNumber + index).ToString(CultureInfo.InvariantCulture);
                var size = gfx.MeasureString(numberText, font);
                var location = ResolvePageNumberLocation(request.PageNumberPosition, outputPage.Width.Point, outputPage.Height.Point, size.Width, size.Height);
                gfx.DrawString(numberText, font, brush, location, XStringFormats.TopLeft);

                progress?.Report(new PdfToolProgress(Percent(index + 1, sourcePdf.PageCount), $"Numeração aplicada na página {index + 1}."));
            }

            EnsureOutputDirectory(destination);
            outputPdf.Save(destination);
            return new PdfToolResult
            {
                Success = true,
                Message = "Numeração aplicada com sucesso.",
                OutputPaths = new[] { destination }
            };
        }, cancellationToken);
    }

    private Task<PdfToolResult> ImagesToPdfAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var sources = NormalizeInputPathList(request.InputPaths, "imagens de entrada", 1, expectPdf: false);
            var destination = NormalizeOutputPdfPath(request.OutputPath);
            EnsureOutputNotInInputs(sources, destination);

            using var outputPdf = new PdfDocument();
            for (var index = 0; index < sources.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var imagePath = sources[index];
                var extension = Path.GetExtension(imagePath);
                if (!SupportedImageExtensions.Contains(extension))
                {
                    throw new InvalidOperationException($"Formato não suportado: {Path.GetFileName(imagePath)}. Use PNG/JPG/JPEG.");
                }

                using var image = XImage.FromFile(imagePath);
                var page = outputPdf.AddPage();
                page.Width = image.PointWidth;
                page.Height = image.PointHeight;

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(image, 0, 0, image.PointWidth, image.PointHeight);

                progress?.Report(new PdfToolProgress(Percent(index + 1, sources.Count), $"Imagem convertida: {Path.GetFileName(imagePath)}"));
            }

            EnsureOutputDirectory(destination);
            outputPdf.Save(destination);
            return new PdfToolResult
            {
                Success = true,
                Message = $"{sources.Count} imagem(ns) convertida(s) para PDF.",
                OutputPaths = new[] { destination }
            };
        }, cancellationToken);
    }

    private async Task<PdfToolResult> CompressAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        var inputPath = NormalizeSingleInputPath(request.InputPath);
        EnsurePdfFile(inputPath);
        var destination = NormalizeOutputPdfPath(request.OutputPath);
        EnsureDifferentInputAndOutput(inputPath, destination);
        EnsureOutputDirectory(destination);
        progress?.Report(new PdfToolProgress(20, "Preparando compressão com QPDF..."));

        await RunQpdfAsync(
            new[]
            {
                "--object-streams=generate",
                "--compress-streams=y",
                inputPath,
                destination
            },
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new PdfToolProgress(100, "Compressão concluída."));
        return new PdfToolResult
        {
            Success = true,
            Message = "Compressão concluída com QPDF.",
            OutputPaths = new[] { destination }
        };
    }

    private async Task<PdfToolResult> ProtectAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        var inputPath = NormalizeSingleInputPath(request.InputPath);
        EnsurePdfFile(inputPath);
        var destination = NormalizeOutputPdfPath(request.OutputPath);
        EnsureDifferentInputAndOutput(inputPath, destination);
        EnsureOutputDirectory(destination);

        var ownerPassword = NormalizeRequiredText(request.OwnerPassword, "senha de proprietário");
        var userPassword = request.UserPassword?.Trim() ?? string.Empty;
        progress?.Report(new PdfToolProgress(20, "Aplicando proteção com QPDF..."));

        await RunQpdfAsync(
            new[]
            {
                "--encrypt",
                userPassword,
                ownerPassword,
                "256",
                "--",
                inputPath,
                destination
            },
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new PdfToolProgress(100, "Proteção aplicada."));
        return new PdfToolResult
        {
            Success = true,
            Message = "Proteção aplicada com sucesso.",
            OutputPaths = new[] { destination }
        };
    }

    private async Task<PdfToolResult> UnlockAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        var inputPath = NormalizeSingleInputPath(request.InputPath);
        EnsurePdfFile(inputPath);
        var destination = NormalizeOutputPdfPath(request.OutputPath);
        EnsureDifferentInputAndOutput(inputPath, destination);
        EnsureOutputDirectory(destination);

        var args = new List<string>();
        var password = request.Password?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(password))
        {
            args.Add($"--password={password}");
        }
        args.Add("--decrypt");
        args.Add(inputPath);
        args.Add(destination);

        progress?.Report(new PdfToolProgress(20, "Removendo senha com QPDF..."));
        await RunQpdfAsync(args, cancellationToken).ConfigureAwait(false);
        progress?.Report(new PdfToolProgress(100, "Desbloqueio concluído."));

        return new PdfToolResult
        {
            Success = true,
            Message = "PDF desbloqueado com sucesso.",
            OutputPaths = new[] { destination }
        };
    }

    private async Task<PdfToolResult> RepairAsync(PdfToolRequest request, IProgress<PdfToolProgress>? progress, CancellationToken cancellationToken)
    {
        var inputPath = NormalizeSingleInputPath(request.InputPath);
        EnsurePdfFile(inputPath);
        var destination = NormalizeOutputPdfPath(request.OutputPath);
        EnsureDifferentInputAndOutput(inputPath, destination);
        EnsureOutputDirectory(destination);

        progress?.Report(new PdfToolProgress(20, "Executando reparo com QPDF..."));
        await RunQpdfAsync(
            new[]
            {
                "--linearize",
                inputPath,
                destination
            },
            cancellationToken).ConfigureAwait(false);
        progress?.Report(new PdfToolProgress(100, "Reparo concluído."));

        return new PdfToolResult
        {
            Success = true,
            Message = "Reparo concluído com sucesso.",
            OutputPaths = new[] { destination }
        };
    }

    private async Task RunQpdfAsync(IEnumerable<string> args, CancellationToken cancellationToken)
    {
        var binary = await ResolveQpdfBinaryAsync(cancellationToken).ConfigureAwait(false);
        var (exitCode, stdOut, stdErr) = await ExecuteProcessAsync(binary, args, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr);
        }
    }

    private async Task<string> ResolveQpdfBinaryAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_cachedQpdfBinary))
        {
            return _cachedQpdfBinary!;
        }

        var candidates = new List<string>();
        var qpdfFromEnv = Environment.GetEnvironmentVariable("QPDF_PATH");
        if (!string.IsNullOrWhiteSpace(qpdfFromEnv))
        {
            candidates.Add(qpdfFromEnv);
        }

        candidates.Add(@"C:\Program Files\qpdf\bin\qpdf.exe");
        candidates.Add(@"C:\Program Files (x86)\qpdf\bin\qpdf.exe");
        candidates.Add("qpdf.exe");
        candidates.Add("qpdf");

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await ProbeQpdfBinaryAsync(candidate, cancellationToken).ConfigureAwait(false))
            {
                _cachedQpdfBinary = candidate;
                return candidate;
            }
        }

        throw new InvalidOperationException("QPDF não encontrado. Instale e adicione ao PATH para usar compressão, proteção, desbloqueio e reparo.");
    }

    private static async Task<bool> ProbeQpdfBinaryAsync(string binaryPath, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

            var (exitCode, _, _) = await ExecuteProcessAsync(binaryPath, new[] { "--version" }, timeoutCts.Token).ConfigureAwait(false);
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> ExecuteProcessAsync(
        string fileName,
        IEnumerable<string> args,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        return (process.ExitCode, stdOut, stdErr);
    }

    private static string NormalizeSingleInputPath(string inputPath)
    {
        var normalized = inputPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Selecione um arquivo de entrada.");
        }

        if (!File.Exists(normalized))
        {
            throw new FileNotFoundException($"Arquivo não encontrado: {normalized}", normalized);
        }

        return normalized;
    }

    private static List<string> NormalizeInputPathList(IReadOnlyList<string> inputPaths, string fieldName, int minLength, bool expectPdf)
    {
        var normalized = inputPaths
            .Select(path => path?.Trim() ?? string.Empty)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count < minLength)
        {
            throw new InvalidOperationException($"Selecione ao menos {minLength} arquivo(s) em {fieldName}.");
        }

        foreach (var path in normalized)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Arquivo não encontrado: {path}", path);
            }

            if (expectPdf)
            {
                EnsurePdfFile(path);
            }
        }

        return normalized;
    }

    private static void EnsurePdfFile(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Arquivo inválido para operação PDF: {path}");
        }
    }

    private static string NormalizeOutputPdfPath(string outputPath)
    {
        var normalized = outputPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Informe o arquivo de saída.");
        }

        if (!string.Equals(Path.GetExtension(normalized), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ".pdf";
        }

        return normalized;
    }

    private static string NormalizeOutputDirectory(string outputDirectory)
    {
        var normalized = outputDirectory?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Informe o diretório de saída.");
        }

        Directory.CreateDirectory(normalized);
        return normalized;
    }

    private static string NormalizeRequiredText(string value, string fieldName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"Campo obrigatorio: {fieldName}.");
        }

        return normalized;
    }

    private static void EnsureOutputDirectory(string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
    }

    private static void EnsureDifferentInputAndOutput(string inputPath, string outputPath)
    {
        var inputFull = Path.GetFullPath(inputPath);
        var outputFull = Path.GetFullPath(outputPath);
        if (string.Equals(inputFull, outputFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("O arquivo de saída deve ser diferente do arquivo de entrada.");
        }
    }

    private static void EnsureOutputNotInInputs(IEnumerable<string> inputPaths, string outputPath)
    {
        var outputFull = Path.GetFullPath(outputPath);
        foreach (var inputPath in inputPaths)
        {
            if (string.Equals(Path.GetFullPath(inputPath), outputFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("O arquivo de saída deve ser diferente dos arquivos de entrada.");
            }
        }
    }

    private static int NormalizeRotation(int value)
    {
        var normalized = value % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        return normalized;
    }

    private static List<int> ParsePageExpression(string expression, int totalPages, bool allowDuplicates)
    {
        var input = NormalizeRequiredText(expression, "páginas")
            .Replace("–", "-", StringComparison.Ordinal)
            .Replace("—", "-", StringComparison.Ordinal)
            .Replace("−", "-", StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        var tokens = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            throw new InvalidOperationException("Nenhuma página válida informada. Use formato como 1-3,5,8.");
        }

        var pages = new List<int>();
        var seen = new HashSet<int>();

        foreach (var token in tokens)
        {
            if (token.Contains('-', StringComparison.Ordinal))
            {
                var pair = token.Split('-', StringSplitOptions.None);
                if (pair.Length != 2 ||
                    !int.TryParse(pair[0], NumberStyles.None, CultureInfo.InvariantCulture, out var start) ||
                    !int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var end))
                {
                    throw new InvalidOperationException($"Intervalo inválido: \"{token}\".");
                }

                ValidatePageBounds(start, totalPages, token);
                ValidatePageBounds(end, totalPages, token);

                var step = start <= end ? 1 : -1;
                for (var page = start; ; page += step)
                {
                    var zeroBased = page - 1;
                    if (allowDuplicates || seen.Add(zeroBased))
                    {
                        pages.Add(zeroBased);
                    }

                    if (page == end)
                    {
                        break;
                    }
                }

                continue;
            }

            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidOperationException($"Página inválida: \"{token}\".");
            }

            ValidatePageBounds(value, totalPages, token);
            var pageIndex = value - 1;
            if (allowDuplicates || seen.Add(pageIndex))
            {
                pages.Add(pageIndex);
            }
        }

        if (pages.Count == 0)
        {
            throw new InvalidOperationException("A expressão de páginas não gerou resultados.");
        }

        return pages;
    }

    private static List<int> ParseOrderExpression(string expression, int totalPages)
    {
        var input = NormalizeRequiredText(expression, "ordem");
        var tokens = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            throw new InvalidOperationException("Informe a ordem no formato 3,1,2,4.");
        }

        var order = new List<int>();
        var used = new HashSet<int>();

        foreach (var token in tokens)
        {
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var page))
            {
                throw new InvalidOperationException($"Página inválida na ordem: \"{token}\".");
            }

            if (page < 1 || page > totalPages)
            {
                throw new InvalidOperationException($"Página inválida na ordem: \"{token}\".");
            }

            var pageIndex = page - 1;
            if (!used.Add(pageIndex))
            {
                throw new InvalidOperationException("A ordem não pode repetir páginas.");
            }

            order.Add(pageIndex);
        }

        for (var pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            if (!used.Contains(pageIndex))
            {
                order.Add(pageIndex);
            }
        }

        return order;
    }

    private static void ValidatePageBounds(int page, int totalPages, string token)
    {
        if (page < 1 || page > totalPages)
        {
            throw new InvalidOperationException($"Página inválida: \"{token}\".");
        }
    }

    private static int Percent(int current, int total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return (int)Math.Round((current / (double)total) * 100D);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static XPoint ResolvePageNumberLocation(
        PdfPageNumberPosition position,
        double pageWidth,
        double pageHeight,
        double textWidth,
        double textHeight)
    {
        const double margin = 24;
        return position switch
        {
            PdfPageNumberPosition.TopLeft => new XPoint(margin, margin),
            PdfPageNumberPosition.TopRight => new XPoint(pageWidth - textWidth - margin, margin),
            PdfPageNumberPosition.BottomLeft => new XPoint(margin, pageHeight - textHeight - margin),
            PdfPageNumberPosition.Center => new XPoint((pageWidth - textWidth) / 2D, (pageHeight - textHeight) / 2D),
            _ => new XPoint(pageWidth - textWidth - margin, pageHeight - textHeight - margin)
        };
    }
}


