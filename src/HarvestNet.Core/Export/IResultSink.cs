namespace HarvestNet.Core.Export;

/// <summary>
/// A destination for extracted items. HarvestNet ships JSON Lines, CSV and SQLite sinks;
/// implement this interface for anything else (a database, a message queue, and so on).
/// </summary>
public interface IResultSink<in T> : IAsyncDisposable
{
    Task WriteAsync(T item, CancellationToken cancellationToken = default);
}
