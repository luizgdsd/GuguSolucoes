using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GuguSolucoes.Desktop.Infrastructure;

namespace GuguSolucoes.Desktop.Modules.PdfTools;

public sealed record PdfMergeProgress(int Percent, string Message);

public sealed class PdfMergeResult
{
    public bool Success { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public sealed class PdfMergeService
{
    private readonly PdfToolService _pdfToolService;

    public PdfMergeService(AppLogger logger)
    {
        _pdfToolService = new PdfToolService(logger);
    }

    public async Task<PdfMergeResult> MergeAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        IProgress<PdfMergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        Progress<PdfToolProgress>? bridge = null;
        if (progress is not null)
        {
            bridge = new Progress<PdfToolProgress>(entry =>
                progress.Report(new PdfMergeProgress(entry.Percent, entry.Message)));
        }

        var result = await _pdfToolService.ExecuteAsync(
            new PdfToolRequest
            {
                Operation = PdfToolOperation.Merge,
                InputPaths = inputPaths,
                OutputPath = outputPath
            },
            bridge,
            cancellationToken).ConfigureAwait(false);

        return new PdfMergeResult
        {
            Success = result.Success,
            Summary = result.Message
        };
    }
}
