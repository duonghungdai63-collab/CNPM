namespace DaLat.DAL.Entities;

/// <summary>Ngôn ngữ hỗ trợ (PK = Code: vi, en, zh, ja, ko).</summary>
public class Language
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string NativeName { get; set; } = "";
    public string SpeechLocale { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public ICollection<PlaceTranslation> Translations { get; set; } = new List<PlaceTranslation>();
}

/// <summary>Địa điểm (POI) có tọa độ và bán kính kích hoạt.</summary>
public class Place
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; } = 100;
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PlaceTranslation> Translations { get; set; } = new List<PlaceTranslation>();
    public ICollection<PlayLog> PlayLogs { get; set; } = new List<PlayLog>();
}

/// <summary>Bản dịch của một địa điểm theo từng ngôn ngữ.</summary>
public class PlaceTranslation
{
    public int Id { get; set; }
    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    public string LanguageCode { get; set; } = "";
    public Language Language { get; set; } = null!;
    public string Name { get; set; } = "";
    public string NarrationText { get; set; } = "";
    public string? AudioUrl { get; set; }
}
