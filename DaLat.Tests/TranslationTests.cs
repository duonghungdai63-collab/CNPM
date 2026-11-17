using System.Net;
using System.Text;
using DaLat.API;
using DaLat.API.Services;
using DaLat.BLL.Common;
using DaLat.DAL.Entities;
using DaLat.DAL.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DaLat.Tests;

// Cùng một class nên xUnit chạy tuần tự (service dùng semaphore tĩnh).
public class TranslationTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return send(request, ct);
        }
    }

    private sealed class FakeLanguages : ILanguageRepository
    {
        private readonly List<Language> _items =
        [
            new() { Code = "vi", Name = "Vietnamese", NativeName = "Tiếng Việt", SpeechLocale = "vi-VN", IsActive = true },
            new() { Code = "en", Name = "English", NativeName = "English", SpeechLocale = "en-US", IsActive = true },
            new() { Code = "fr", Name = "French", NativeName = "Français", SpeechLocale = "fr-FR", IsActive = false }
        ];
        public Task<List<Language>> GetActiveAsync() => Task.FromResult(_items.Where(l => l.IsActive).ToList());
        public Task<List<Language>> GetAllAsync() => Task.FromResult(_items.ToList());
        public Task<Language?> FindAsync(string code) => Task.FromResult(_items.FirstOrDefault(l => l.Code == code));
        public Task AddAsync(Language language) { _items.Add(language); return Task.CompletedTask; }
        public Task SaveAsync() => Task.CompletedTask;
    }

    private static HttpResponseMessage Ok(string text, string doneReason = "stop")
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content = text },
            done = true,
            done_reason = doneReason
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static OllamaTranslationService Make(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test/") },
            Options.Create(new OllamaOptions()), new FakeLanguages(),
            NullLogger<OllamaTranslationService>.Instance);

    private static readonly AutoTranslateRequest Valid = new("en", "Hồ Xuân Hương", "Hồ nằm giữa trung tâm Đà Lạt.");

    // ---------- Chia đoạn ----------

    [Fact]
    public void Chunker_ShortText_ReturnsSingleChunk()
    {
        var chunks = TextChunker.Split("Xin chào Đà Lạt.", 600);
        Assert.Single(chunks);
    }

    [Fact]
    public void Chunker_LongText_SplitsWithinLimitAndKeepsContent()
    {
        var text = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"Đây là câu số {i}."));
        var chunks = TextChunker.Split(text, 600);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= 600));
        Assert.Equal(
            new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray()),
            new string(string.Concat(chunks).Where(c => !char.IsWhiteSpace(c)).ToArray()));
    }

    [Fact]
    public void Chunker_TextWithoutSpaces_HardCuts()
    {
        var chunks = TextChunker.Split(new string('字', 1500), 600);
        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Length <= 600));
    }

    // ---------- Kiểm tra đầu vào (không gọi Ollama) ----------

    [Theory]
    [InlineData("vi")]   // trùng ngôn ngữ nguồn
    [InlineData("fr")]   // đang tắt
    [InlineData("xx")]   // không tồn tại
    [InlineData("")]
    public async Task InvalidTargetLanguage_IsRejected_WithoutCallingOllama(string target)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ok("x")));
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Make(handler).TranslateAsync(Valid with { TargetLanguage = target }, default));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("", "Nội dung")]
    [InlineData("Tên", "")]
    [InlineData("Tên", "   ")]
    public async Task MissingSource_IsRejected(string name, string narration)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ok("x")));
        await Assert.ThrowsAsync<AppException>(() =>
            Make(handler).TranslateAsync(Valid with { Name = name, NarrationText = narration }, default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task SourceTooLong_IsRejected()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ok("x")));
        await Assert.ThrowsAsync<AppException>(() =>
            Make(handler).TranslateAsync(Valid with { Name = new string('a', 201) }, default));
        await Assert.ThrowsAsync<AppException>(() =>
            Make(handler).TranslateAsync(Valid with { NarrationText = new string('a', 4001) }, default));
    }

    // ---------- Kết quả từ Ollama ----------

    [Fact]
    public async Task Success_ReturnsDraftWithoutAudioUrl()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ok("Xuan Huong Lake")));
        var result = await Make(handler).TranslateAsync(Valid, default);

        Assert.Equal("en", result.LanguageCode);
        Assert.Equal("Xuan Huong Lake", result.Name);
        Assert.False(string.IsNullOrWhiteSpace(result.NarrationText));
        Assert.Null(result.AudioUrl);
        Assert.Equal(2, handler.Calls); // tên + 1 đoạn thuyết minh
    }

    [Fact]
    public async Task TruncatedOutput_IsRejected()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ok("Half a sen", "length")));
        var ex = await Assert.ThrowsAsync<AppException>(() => Make(handler).TranslateAsync(Valid, default));
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task EmptyOutput_IsRejected()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ok("   ")));
        var ex = await Assert.ThrowsAsync<AppException>(() => Make(handler).TranslateAsync(Valid, default));
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task OllamaUnreachable_ReturnsBadGateway()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));
        var ex = await Assert.ThrowsAsync<AppException>(() => Make(handler).TranslateAsync(Valid, default));
        Assert.Equal(502, ex.StatusCode);
    }

    [Fact]
    public async Task ModelMissing_TellsAdminToPull()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"error\":\"model 'translategemma:4b' not found\"}")
        }));
        var ex = await Assert.ThrowsAsync<AppException>(() => Make(handler).TranslateAsync(Valid, default));
        Assert.Contains("ollama pull", ex.Message);
    }

    [Fact]
    public async Task Cancelled_PropagatesOperationCanceled()
    {
        var started = new TaskCompletionSource();
        var handler = new StubHandler(async (_, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return Ok("never");
        });
        using var cts = new CancellationTokenSource();
        var task = Make(handler).TranslateAsync(Valid, cts.Token);
        await started.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

        // Sau khi hủy, cổng phải được nhả để yêu cầu sau chạy bình thường (BR18, BR19).
        var next = await Make(new StubHandler((_, _) => Task.FromResult(Ok("ok")))).TranslateAsync(Valid, default);
        Assert.Equal("ok", next.Name);
    }

    [Fact]
    public async Task SecondRequestWhileBusy_IsRejectedWith429()
    {
        var gate = new TaskCompletionSource<HttpResponseMessage>();
        var started = new TaskCompletionSource();
        var calls = 0;
        var first = new StubHandler((_, _) =>
        {
            started.TrySetResult();
            return Interlocked.Increment(ref calls) == 1 ? gate.Task : Task.FromResult(Ok("done"));
        });
        var firstTask = Make(first).TranslateAsync(Valid, default);
        await started.Task;

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Make(new StubHandler((_, _) => Task.FromResult(Ok("x")))).TranslateAsync(Valid, default));
        Assert.Equal(429, ex.StatusCode);

        gate.SetResult(Ok("done"));
        await firstTask;
    }
}

