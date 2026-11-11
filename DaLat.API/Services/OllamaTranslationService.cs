using System.Net;
using System.Text.Json.Serialization;
using DaLat.BLL.Common;
using DaLat.BLL.Services;
using DaLat.DAL.Repositories;
using Microsoft.Extensions.Options;

namespace DaLat.API.Services;

/// <summary>
/// Dịch tên và thuyết minh tiếng Việt sang ngôn ngữ đích bằng Ollama (api/chat).
/// Trả bản nháp TranslationDto, KHÔNG ghi CSDL (BR16). Mỗi lúc chỉ xử lý một yêu cầu (BR19).
/// </summary>
public sealed class OllamaTranslationService(
    HttpClient http,
    IOptions<OllamaOptions> options,
    ILanguageRepository languages,
    ILogger<OllamaTranslationService> log) : ITranslationService
{
    public const int MaxNameLength = 200;
    public const int MaxNarrationLength = 4000;
    public const int ChunkSize = 600;

    // Dùng chung cho mọi instance: backend chỉ dịch một yêu cầu tại một thời điểm.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly OllamaOptions _opt = options.Value;

    public async Task<TranslationDto> TranslateAsync(AutoTranslateRequest request, CancellationToken ct)
    {
        // --- Kiểm tra dữ liệu trước khi gọi Ollama (BR14, BR15) ---
        var name = (request.Name ?? "").Trim();
        var narration = (request.NarrationText ?? "").Trim();
        if (name.Length is 0 or > MaxNameLength)
            throw new AppException($"Tên tiếng Việt phải từ 1 đến {MaxNameLength} ký tự.");
        if (narration.Length is 0 or > MaxNarrationLength)
            throw new AppException($"Thuyết minh tiếng Việt phải từ 1 đến {MaxNarrationLength} ký tự.");

        var target = (request.TargetLanguage ?? "").Trim().ToLowerInvariant();
        var lang = target.Length == 0 || target == "vi" ? null : await languages.FindAsync(target);
        if (lang == null || !lang.IsActive)
            throw new AppException("Ngôn ngữ đích không hợp lệ: phải đang kích hoạt và khác tiếng Việt.");

        if (!await Gate.WaitAsync(0, CancellationToken.None))
            throw new AppException("Hệ thống đang dịch một yêu cầu khác. Vui lòng thử lại sau ít phút.", 429);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_opt.TimeoutSeconds));

            string translatedName, translatedNarration;
            try
            {
                translatedName = await ChatAsync(BuildPrompt(lang.Name, lang.Code, name), cts.Token);
                translatedNarration = await TranslateNarrationAsync(lang, narration, cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AppException(
                    $"Quá thời gian chờ Ollama ({_opt.TimeoutSeconds} giây). Hãy tăng OLLAMA_TIMEOUT_SECONDS hoặc thử lại.", 504);
            }

            // BR20: không trả nội dung vượt giới hạn lưu
            if (translatedName.Length > MaxNameLength)
                throw new AppException($"Tên đã dịch dài hơn {MaxNameLength} ký tự; hãy rút gọn bản tiếng Việt.", 422);
            if (translatedNarration.Length > MaxNarrationLength)
                throw new AppException($"Thuyết minh đã dịch dài hơn {MaxNarrationLength} ký tự; hãy rút gọn bản tiếng Việt.", 422);

            // AudioUrl để trống: dùng giọng đọc của trình duyệt (BR21)
            return new TranslationDto(lang.Code, translatedName, translatedNarration, null);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Tách đoạn văn, chia tiếp tối đa 600 ký tự rồi dịch từng đoạn và ghép lại.</summary>
    private async Task<string> TranslateNarrationAsync(DaLat.DAL.Entities.Language lang, string text, CancellationToken ct)
    {
        var paragraphs = new List<string>();
        foreach (var paragraph in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = new List<string>();
            foreach (var chunk in TextChunker.Split(paragraph, ChunkSize))
                parts.Add(await ChatAsync(BuildPrompt(lang.Name, lang.Code, chunk), ct));
            paragraphs.Add(string.Join(" ", parts));
        }
        return string.Join("\n", paragraphs);
    }

    private static string BuildPrompt(string languageName, string code, string text) =>
        $"You are a professional Vietnamese (vi) to {languageName} ({code}) translator. " +
        "Your goal is to accurately convey the meaning and nuances of the original Vietnamese text " +
        "while adhering to the grammar, vocabulary, and cultural sensitivities of the target language. " +
        $"Produce only the {languageName} translation, without any additional explanations or commentary. " +
        $"Please translate the following Vietnamese text into {languageName}:\n\n\n{text}";

    private async Task<string> ChatAsync(string prompt, CancellationToken ct)
    {
        var body = new
        {
            model = _opt.Model,
            stream = false,
            messages = new[] { new { role = "user", content = prompt } },
            options = new { temperature = 0.1 }
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("api/chat", body, ct);
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Không kết nối được Ollama tại {Url}", _opt.BaseUrl);
            throw new AppException(
                $"Không kết nối được Ollama tại {_opt.BaseUrl}. Hãy kiểm tra Ollama đang chạy " +
                "(khi dùng Docker, địa chỉ là http://host.docker.internal:11434). Bạn vẫn có thể nhập bản dịch thủ công.", 502);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                if (response.StatusCode == HttpStatusCode.NotFound &&
                    detail.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    throw new AppException($"Chưa có mô hình '{_opt.Model}'. Hãy chạy: ollama pull {_opt.Model}", 502);
                log.LogWarning("Ollama trả lỗi {Status}: {Detail}", (int)response.StatusCode, detail);
                throw new AppException($"Ollama trả về lỗi {(int)response.StatusCode}.", 502);
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct);
            if (result?.DoneReason == "length")
                throw new AppException("Bản dịch bị ngắt giữa chừng; hãy rút gọn nội dung hoặc thử lại.", 422);

            var content = result?.Message?.Content?.Trim();
            if (string.IsNullOrEmpty(content))
                throw new AppException("Ollama trả về bản dịch rỗng.", 422);
            return content;
        }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
        [JsonPropertyName("done_reason")] public string? DoneReason { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
