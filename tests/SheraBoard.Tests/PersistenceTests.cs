using System.Text;
using SheraBoard.Core.Capture;
using SheraBoard.Core.Models;
using SheraBoard.Core.Persistence;

namespace SheraBoard.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task StorageRepositoryPersistsItemsAcrossRepositoryInstances()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        var capture = await pipeline.CaptureAsync(
            ClipboardCapture.FromText("persist me after restart"),
            DateTimeOffset.UtcNow,
            "notepad.exe");

        Assert.NotNull(capture.Item);
        await fixture.Repository.DisposeAsync();

        await using var reopened = new StorageRepository(fixture.Paths.DatabasePath);
        await reopened.InitializeAsync();

        var items = await reopened.ListAsync(new ClipboardQuery(SearchText: "persist"));
        var item = Assert.Single(items);
        Assert.Equal(capture.Item.Id, item.Id);
        Assert.Equal("persist me after restart", item.PreviewText);
    }

    [Fact]
    public async Task FilePayloadStoreProtectsPayloadBytesAndRoundTripsJson()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var payload = new TextPayload("very secret clipboard text");

        var payloadRef = await fixture.PayloadStore.WriteAsync(Guid.NewGuid(), payload);
        var payloadPath = fixture.PayloadStore.ResolvePayloadPath(payloadRef);
        var rawBytes = await File.ReadAllBytesAsync(payloadPath);

        Assert.DoesNotContain("very secret clipboard text", Encoding.UTF8.GetString(rawBytes));

        var roundTrip = await fixture.PayloadStore.ReadAsync<TextPayload>(payloadRef);
        Assert.Equal(payload.Text, roundTrip.Text);
    }

    [Fact]
    public async Task StorageRepositorySupportsPagedQueriesInNewestFirstOrder()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();
        var start = new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.FromHours(8));

        for (var i = 0; i < 6; i++)
        {
            await pipeline.CaptureAsync(
                ClipboardCapture.FromText($"paged item {i}"),
                start.AddMinutes(i),
                "notepad.exe");
        }

        var firstPage = await fixture.Repository.ListAsync(new ClipboardQuery(Limit: 2, Offset: 0));
        var secondPage = await fixture.Repository.ListAsync(new ClipboardQuery(Limit: 2, Offset: 2));

        Assert.Equal(["paged item 5", "paged item 4"], firstPage.Select(item => item.PreviewText));
        Assert.Equal(["paged item 3", "paged item 2"], secondPage.Select(item => item.PreviewText));
    }

    [Fact]
    public async Task StorageRepositoryFiltersLocalDateRangeBeforeLimit()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        await pipeline.CaptureAsync(
            ClipboardCapture.FromText("older item"),
            new DateTimeOffset(2026, 5, 24, 10, 0, 0, TimeSpan.FromHours(8)),
            "notepad.exe");
        await pipeline.CaptureAsync(
            ClipboardCapture.FromText("target item"),
            new DateTimeOffset(2026, 5, 25, 10, 0, 0, TimeSpan.FromHours(8)),
            "notepad.exe");
        await pipeline.CaptureAsync(
            ClipboardCapture.FromText("newer item"),
            new DateTimeOffset(2026, 5, 26, 10, 0, 0, TimeSpan.FromHours(8)),
            "notepad.exe");

        var items = await fixture.Repository.ListAsync(new ClipboardQuery(
            StartDate: new DateOnly(2026, 5, 25),
            EndDate: new DateOnly(2026, 5, 25),
            Limit: 10));

        var item = Assert.Single(items);
        Assert.Equal("target item", item.PreviewText);
    }

    [Fact]
    public async Task StorageRepositoryFiltersBySourceAppSearchTermsPinnedAndFeatures()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();
        var start = new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.FromHours(8));

        await pipeline.CaptureAsync(
            ClipboardCapture.FromText("regular meeting note"),
            start,
            "notepad.exe");
        await pipeline.CaptureAsync(
            ClipboardCapture.FromText("https://example.com/shera docs"),
            start.AddMinutes(1),
            "chrome.exe");
        var codeCapture = await pipeline.CaptureAsync(
            ClipboardCapture.FromText("public class Demo { private string Name; }"),
            start.AddMinutes(2),
            "Code.exe");
        Assert.NotNull(codeCapture.Item);
        await fixture.Repository.SetPinnedAsync(codeCapture.Item.Id, pinned: true);

        var appItems = await fixture.Repository.ListAsync(new ClipboardQuery(
            SearchTerms: ["class"],
            SourceApp: "code",
            PinnedOnly: true,
            Features: [ClipboardContentFeature.Code]));

        var appItem = Assert.Single(appItems);
        Assert.Equal(codeCapture.Item.Id, appItem.Id);

        var urlItems = await fixture.Repository.ListAsync(new ClipboardQuery(
            SearchTerms: ["docs"],
            SourceApp: "chrome",
            Features: [ClipboardContentFeature.Url]));

        var urlItem = Assert.Single(urlItems);
        Assert.Equal("https://example.com/shera docs", urlItem.PreviewText);
    }

    [Fact]
    public async Task StorageRepositoryReturnsRecentSourceApps()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();
        var start = new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.FromHours(8));

        await pipeline.CaptureAsync(ClipboardCapture.FromText("from note"), start, "notepad.exe");
        await pipeline.CaptureAsync(ClipboardCapture.FromText("from browser"), start.AddMinutes(1), "chrome.exe");
        await pipeline.CaptureAsync(ClipboardCapture.FromText("from note later"), start.AddMinutes(2), "notepad.exe");

        var apps = await fixture.Repository.ListRecentSourceAppsAsync(limit: 4);

        Assert.Equal(["notepad.exe", "chrome.exe"], apps.Select(app => app.SourceApp));
        Assert.Equal(2, apps[0].ItemCount);
        Assert.Equal(1, apps[1].ItemCount);
    }
}
