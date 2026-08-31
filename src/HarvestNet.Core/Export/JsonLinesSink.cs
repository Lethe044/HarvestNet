using System.Text.Json;

namespace HarvestNet.Core.Export;

/// <summary>
/// Writes one JSON object per line, the most convenient format for streaming large result
/// sets without holding everything in memory.
/// </summary>
public sealed class JsonLinesSink<T> : IResultSink<T>
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonLinesSink(string filePath)
    {
        _writer = new StreamWriter(filePath, append: false);
    }

    public async Task WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(item);
            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync().ConfigureAwait(false);
        await _writer.DisposeAsync().ConfigureAwait(false);
        _lock.Dispose();
    }
}
