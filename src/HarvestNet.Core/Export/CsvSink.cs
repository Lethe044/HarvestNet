using System.Reflection;

namespace HarvestNet.Core.Export;

/// <summary>
/// Writes items as CSV. When <typeparamref name="T"/> is <c>Dictionary&lt;string, string?&gt;</c>
/// (the shape used by CLI recipes) the columns are taken from the first item's keys;
/// otherwise the public properties of <typeparamref name="T"/> are used.
/// </summary>
public sealed class CsvSink<T> : IResultSink<T>
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly bool _isDictionaryMode;
    private readonly PropertyInfo[] _properties;
    private List<string>? _dictionaryColumns;
    private bool _headerWritten;

    public CsvSink(string filePath)
    {
        _writer = new StreamWriter(filePath, append: false);
        _isDictionaryMode = typeof(T) == typeof(Dictionary<string, string?>);
        _properties = _isDictionaryMode
            ? Array.Empty<PropertyInfo>()
            : typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    public async Task WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isDictionaryMode && item is Dictionary<string, string?> dictionary)
            {
                _dictionaryColumns ??= dictionary.Keys.ToList();

                if (!_headerWritten)
                {
                    await _writer.WriteLineAsync(string.Join(',', _dictionaryColumns.Select(Escape))).ConfigureAwait(false);
                    _headerWritten = true;
                }

                var values = _dictionaryColumns.Select(column =>
                    Escape(dictionary.TryGetValue(column, out var value) ? value ?? string.Empty : string.Empty));
                await _writer.WriteLineAsync(string.Join(',', values)).ConfigureAwait(false);
            }
            else
            {
                if (!_headerWritten)
                {
                    await _writer.WriteLineAsync(string.Join(',', _properties.Select(p => Escape(p.Name)))).ConfigureAwait(false);
                    _headerWritten = true;
                }

                var values = _properties.Select(p => Escape(p.GetValue(item)?.ToString() ?? string.Empty));
                await _writer.WriteLineAsync(string.Join(',', values)).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync().ConfigureAwait(false);
        await _writer.DisposeAsync().ConfigureAwait(false);
        _lock.Dispose();
    }
}
