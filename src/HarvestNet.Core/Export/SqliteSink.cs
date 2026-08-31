using System.Reflection;
using Microsoft.Data.Sqlite;

namespace HarvestNet.Core.Export;

/// <summary>
/// Writes items into a local SQLite database file, creating the table on first write.
/// Supports both strongly typed POCOs and the dictionary shape used by CLI recipes.
/// </summary>
public sealed class SqliteSink<T> : IResultSink<T>
{
    private readonly SqliteConnection _connection;
    private readonly string _tableName;
    private readonly bool _isDictionaryMode;
    private readonly PropertyInfo[] _properties;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _tableCreated;
    private List<string>? _dictionaryColumns;

    public SqliteSink(string filePath, string? tableName = null)
    {
        _tableName = tableName ?? typeof(T).Name;
        _isDictionaryMode = typeof(T) == typeof(Dictionary<string, string?>);
        _properties = _isDictionaryMode
            ? Array.Empty<PropertyInfo>()
            : typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        _connection = new SqliteConnection($"Data Source={filePath}");
        _connection.Open();

        if (!_isDictionaryMode)
        {
            CreateTable(_properties.Select(p => p.Name));
            _tableCreated = true;
        }
    }

    private void CreateTable(IEnumerable<string> columns)
    {
        var columnDefinitions = string.Join(", ", columns.Select(c => $"\"{c}\" TEXT"));
        using var command = _connection.CreateCommand();
        command.CommandText = $"CREATE TABLE IF NOT EXISTS \"{_tableName}\" ({columnDefinitions})";
        command.ExecuteNonQuery();
    }

    public async Task WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isDictionaryMode && item is Dictionary<string, string?> dictionary)
            {
                if (!_tableCreated)
                {
                    _dictionaryColumns = dictionary.Keys.ToList();
                    CreateTable(_dictionaryColumns);
                    _tableCreated = true;
                }

                var columns = _dictionaryColumns ?? dictionary.Keys.ToList();
                var columnNames = string.Join(", ", columns.Select(c => $"\"{c}\""));
                var parameterNames = string.Join(", ", columns.Select((_, i) => "$p" + i));

                using var insertCommand = _connection.CreateCommand();
                insertCommand.CommandText = $"INSERT INTO \"{_tableName}\" ({columnNames}) VALUES ({parameterNames})";

                for (var i = 0; i < columns.Count; i++)
                {
                    dictionary.TryGetValue(columns[i], out var value);
                    insertCommand.Parameters.AddWithValue("$p" + i, (object?)value ?? string.Empty);
                }

                insertCommand.ExecuteNonQuery();
            }
            else
            {
                var columnNames = string.Join(", ", _properties.Select(p => $"\"{p.Name}\""));
                var parameterNames = string.Join(", ", _properties.Select((p, i) => "$p" + i));

                using var insertCommand = _connection.CreateCommand();
                insertCommand.CommandText = $"INSERT INTO \"{_tableName}\" ({columnNames}) VALUES ({parameterNames})";

                for (var i = 0; i < _properties.Length; i++)
                {
                    var value = _properties[i].GetValue(item)?.ToString() ?? string.Empty;
                    insertCommand.Parameters.AddWithValue("$p" + i, value);
                }

                insertCommand.ExecuteNonQuery();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _connection.Dispose();
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}
