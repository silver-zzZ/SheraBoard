using System.Text.Json;
using Microsoft.Data.Sqlite;
using SheraBoard.Core.Models;

namespace SheraBoard.Core.Persistence;

public sealed class StorageRepository : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _databasePath;
    private readonly string _connectionString;

    public StorageRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS clipboard_items (
                id TEXT PRIMARY KEY,
                captured_at TEXT NOT NULL,
                kind TEXT NOT NULL,
                preview_text TEXT NOT NULL,
                source_app TEXT NULL,
                formats_json TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                pinned INTEGER NOT NULL,
                favorite INTEGER NOT NULL,
                payload_ref TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                semantic_hash TEXT NOT NULL,
                format_score INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_clipboard_items_captured_at
                ON clipboard_items(captured_at DESC);

            CREATE INDEX IF NOT EXISTS ix_clipboard_items_semantic_hash
                ON clipboard_items(semantic_hash);

            CREATE INDEX IF NOT EXISTS ix_clipboard_items_source_app
                ON clipboard_items(source_app);

            CREATE INDEX IF NOT EXISTS ix_clipboard_items_kind
                ON clipboard_items(kind);

            CREATE INDEX IF NOT EXISTS ix_clipboard_items_pinned
                ON clipboard_items(pinned, captured_at DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InsertAsync(ClipboardItemRecord item, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO clipboard_items (
                id, captured_at, kind, preview_text, source_app, formats_json,
                size_bytes, pinned, favorite, payload_ref, content_hash, semantic_hash, format_score
            ) VALUES (
                $id, $captured_at, $kind, $preview_text, $source_app, $formats_json,
                $size_bytes, $pinned, $favorite, $payload_ref, $content_hash, $semantic_hash, $format_score
            );
            """;
        AddItemParameters(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClipboardItemRecord>> ListAsync(
        ClipboardQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        var conditions = new List<string>
        {
            "($kind IS NULL OR kind = $kind)",
            "($start IS NULL OR captured_at >= $start)",
            "($end IS NULL OR captured_at < $end)"
        };
        var searchTerms = ResolveSearchTerms(query);
        for (var index = 0; index < searchTerms.Count; index++)
        {
            conditions.Add($"(LOWER(preview_text) LIKE $term{index} OR LOWER(IFNULL(source_app, '')) LIKE $term{index})");
            command.Parameters.AddWithValue($"$term{index}", $"%{searchTerms[index].ToLowerInvariant()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.SourceApp))
        {
            conditions.Add("LOWER(IFNULL(source_app, '')) LIKE $source_app");
            command.Parameters.AddWithValue("$source_app", $"%{query.SourceApp.Trim().ToLowerInvariant()}%");
        }

        if (query.PinnedOnly)
        {
            conditions.Add("pinned = 1");
        }

        foreach (var feature in query.Features ?? [])
        {
            conditions.Add(feature switch
            {
                ClipboardContentFeature.Url => "(LOWER(preview_text) LIKE '%http://%' OR LOWER(preview_text) LIKE '%https://%' OR LOWER(preview_text) LIKE '%www.%')",
                ClipboardContentFeature.Code => """
                    (kind IN ('Text', 'RichText') AND (
                        LOWER(preview_text) LIKE '%function %'
                        OR LOWER(preview_text) LIKE '%class %'
                        OR LOWER(preview_text) LIKE '%public %'
                        OR LOWER(preview_text) LIKE '%private %'
                        OR LOWER(preview_text) LIKE '%namespace %'
                        OR LOWER(preview_text) LIKE '%using %'
                        OR LOWER(preview_text) LIKE '%import %'
                        OR LOWER(preview_text) LIKE '%const %'
                        OR LOWER(preview_text) LIKE '%let %'
                        OR LOWER(preview_text) LIKE '%var %'
                        OR LOWER(preview_text) LIKE '%def %'
                        OR LOWER(preview_text) LIKE '%select %'
                        OR LOWER(preview_text) LIKE '%=>%'
                        OR (INSTR(preview_text, '{') > 0 AND INSTR(preview_text, '}') > 0)
                    ))
                    """,
                _ => "1 = 1"
            });
        }

        command.CommandText = $$"""
            SELECT id, captured_at, kind, preview_text, source_app, formats_json,
                   size_bytes, pinned, favorite, payload_ref, content_hash, semantic_hash, format_score
            FROM clipboard_items
            WHERE {{string.Join("\n              AND ", conditions)}}
            ORDER BY pinned DESC, captured_at DESC
            LIMIT $limit OFFSET $offset;
            """;
        var (startUtc, endUtc) = ResolveUtcDateRange(query);
        command.Parameters.AddWithValue("$kind", query.Kind is null ? DBNull.Value : query.Kind.Value.ToString());
        command.Parameters.AddWithValue("$start", startUtc is null ? DBNull.Value : startUtc.Value.ToString("O"));
        command.Parameters.AddWithValue("$end", endUtc is null ? DBNull.Value : endUtc.Value.ToString("O"));
        command.Parameters.AddWithValue("$limit", Math.Clamp(query.Limit, 1, 5000));
        command.Parameters.AddWithValue("$offset", Math.Max(0, query.Offset));

        var items = new List<ClipboardItemRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<SourceAppSummary>> ListRecentSourceAppsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_app, COUNT(*) AS item_count, MAX(captured_at) AS last_captured_at
            FROM clipboard_items
            WHERE source_app IS NOT NULL AND TRIM(source_app) <> ''
            GROUP BY source_app
            ORDER BY last_captured_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 24));

        var apps = new List<SourceAppSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            apps.Add(new SourceAppSummary(
                reader.GetString(0),
                reader.GetInt32(1),
                DateTimeOffset.Parse(reader.GetString(2))));
        }

        return apps;
    }

    public async Task<ClipboardItemRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, captured_at, kind, preview_text, source_app, formats_json,
                   size_bytes, pinned, favorite, payload_ref, content_hash, semantic_hash, format_score
            FROM clipboard_items
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadItem(reader) : null;
    }

    public async Task<ClipboardItemRecord?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, captured_at, kind, preview_text, source_app, formats_json,
                   size_bytes, pinned, favorite, payload_ref, content_hash, semantic_hash, format_score
            FROM clipboard_items
            ORDER BY captured_at DESC
            LIMIT 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadItem(reader) : null;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM clipboard_items WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task SetPinnedAsync(Guid id, bool pinned, CancellationToken cancellationToken = default)
    {
        return SetFlagAsync(id, "pinned", pinned, cancellationToken);
    }

    public Task SetFavoriteAsync(Guid id, bool favorite, CancellationToken cancellationToken = default)
    {
        return SetFlagAsync(id, "favorite", favorite, cancellationToken);
    }

    public async Task UpdatePayloadRefAsync(
        Guid id,
        string payloadRef,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_items
            SET payload_ref = $payload_ref,
                size_bytes = $size_bytes
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$payload_ref", payloadRef);
        command.Parameters.AddWithValue("$size_bytes", sizeBytes);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task SetFlagAsync(Guid id, string columnName, bool value, CancellationToken cancellationToken)
    {
        if (columnName is not ("pinned" or "favorite"))
        {
            throw new ArgumentOutOfRangeException(nameof(columnName));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = $"UPDATE clipboard_items SET {columnName} = $value WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$value", value ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static (DateTimeOffset? StartUtc, DateTimeOffset? EndUtc) ResolveUtcDateRange(ClipboardQuery query)
    {
        var startDate = query.Date ?? query.StartDate;
        var endDate = query.Date ?? query.EndDate;

        DateTimeOffset? startUtc = startDate is null
            ? null
            : new DateTimeOffset(startDate.Value.ToDateTime(TimeOnly.MinValue)).ToUniversalTime();
        DateTimeOffset? endUtc = endDate is null
            ? null
            : new DateTimeOffset(endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)).ToUniversalTime();

        return (startUtc, endUtc);
    }

    private static IReadOnlyList<string> ResolveSearchTerms(ClipboardQuery query)
    {
        if (query.SearchTerms is { Count: > 0 })
        {
            return query.SearchTerms
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Select(term => term.Trim())
                .ToList();
        }

        return string.IsNullOrWhiteSpace(query.SearchText)
            ? []
            : [query.SearchText.Trim()];
    }

    private static void AddItemParameters(SqliteCommand command, ClipboardItemRecord item)
    {
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$captured_at", item.CapturedAt.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$kind", item.Kind.ToString());
        command.Parameters.AddWithValue("$preview_text", item.PreviewText);
        command.Parameters.AddWithValue("$source_app", string.IsNullOrWhiteSpace(item.SourceApp) ? DBNull.Value : item.SourceApp);
        command.Parameters.AddWithValue("$formats_json", JsonSerializer.Serialize(item.Formats, JsonOptions));
        command.Parameters.AddWithValue("$size_bytes", item.SizeBytes);
        command.Parameters.AddWithValue("$pinned", item.Pinned ? 1 : 0);
        command.Parameters.AddWithValue("$favorite", item.Favorite ? 1 : 0);
        command.Parameters.AddWithValue("$payload_ref", item.PayloadRef);
        command.Parameters.AddWithValue("$content_hash", item.ContentHash);
        command.Parameters.AddWithValue("$semantic_hash", item.SemanticHash);
        command.Parameters.AddWithValue("$format_score", item.FormatScore);
    }

    private static ClipboardItemRecord ReadItem(SqliteDataReader reader)
    {
        var formats = JsonSerializer.Deserialize<List<string>>(reader.GetString(5), JsonOptions) ?? [];
        return new ClipboardItemRecord(
            Guid.Parse(reader.GetString(0)),
            DateTimeOffset.Parse(reader.GetString(1)),
            Enum.Parse<ClipboardKind>(reader.GetString(2)),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            formats,
            reader.GetInt64(6),
            reader.GetInt32(7) == 1,
            reader.GetInt32(8) == 1,
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetInt32(12));
    }
}
