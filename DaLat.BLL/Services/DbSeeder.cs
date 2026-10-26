using DaLat.BLL.Common;
using DaLat.DAL;
using DaLat.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DaLat.BLL.Services;

/// <summary>Tạo bảng và nạp dữ liệu mẫu: 5 ngôn ngữ, 6 địa điểm × 5 ngôn ngữ, tài khoản admin (US-36).</summary>
public class DbSeeder(AppDbContext db, IConfiguration config, ILogger<DbSeeder> log)
{
    // Thứ tự bản dịch: vi, en, zh, ja, ko. Tọa độ là giá trị gần đúng, nên đối chiếu lại trên bản đồ.
    private static readonly string[] Codes = ["vi", "en", "zh", "ja", "ko"];

    private record Seed(string Slug, double Lat, double Lng, int Radius, string Address, (string Name, string Text)[] T);

    private static readonly Seed[] Places =
    [
        new("ho-xuan-huong", 11.9437, 108.4419, 150, "Phường 1, Đà Lạt",
        [
            ("Hồ Xuân Hương", "Hồ Xuân Hương là hồ nước hình trăng lưỡi liềm nằm giữa trung tâm Đà Lạt, được xem là biểu tượng của thành phố ngàn hoa. Quanh hồ có con đường đi bộ và đạp vịt rất được du khách yêu thích."),
            ("Xuan Huong Lake", "Xuan Huong Lake is a crescent-shaped lake in the heart of Da Lat and a symbol of the City of Flowers. A lakeside path and swan-shaped paddle boats make it a favorite spot for visitors."),
            ("春香湖", "春香湖是位于大叻市中心的月牙形湖泊，被视为这座花城的象征。环湖步道和天鹅脚踏船深受游客喜爱。"),
            ("スアンフオン湖", "スアンフオン湖はダラット中心部にある三日月形の湖で、花の街の象徴とされています。湖畔の遊歩道やスワンボートが観光客に人気です。"),
            ("쑤언흐엉 호수", "쑤언흐엉 호수는 달랏 중심에 있는 초승달 모양의 호수로, 꽃의 도시를 상징합니다. 호숫가 산책로와 백조 모양 페달보트가 관광객에게 인기입니다.")
        ]),
        new("cho-da-lat", 11.9433, 108.4383, 100, "Nguyễn Thị Minh Khai, Phường 1, Đà Lạt",
        [
            ("Chợ Đà Lạt", "Chợ Đà Lạt là khu chợ trung tâm sôi động, nơi bán rau củ, hoa tươi, dâu tây, mứt và nhiều đặc sản của cao nguyên. Buổi tối khu vực quanh chợ trở thành phố ẩm thực đường phố nhộn nhịp."),
            ("Da Lat Market", "Da Lat Market is the lively central market selling vegetables, fresh flowers, strawberries, jams and many highland specialties. In the evening the area turns into a busy street-food district."),
            ("大叻市场", "大叻市场是热闹的中央市场，出售蔬菜、鲜花、草莓、果酱和许多高原特产。夜晚市场周围会变成热闹的街头美食区。"),
            ("ダラット市場", "ダラット市場は野菜、生花、イチゴ、ジャム、高原の特産品が並ぶ賑やかな中央市場です。夜は周辺が活気ある屋台グルメ街になります。"),
            ("달랏 시장", "달랏 시장은 채소, 생화, 딸기, 잼 등 고원 특산품을 파는 활기찬 중앙시장입니다. 저녁이면 주변이 붐비는 길거리 음식 거리로 변합니다.")
        ]),
        new("ga-da-lat", 11.9427, 108.4551, 120, "1 Quang Trung, Phường 10, Đà Lạt",
        [
            ("Ga Đà Lạt", "Ga Đà Lạt khởi công năm 1932, nổi tiếng với kiến trúc mái nhọn độc đáo và được công nhận là di tích kiến trúc nghệ thuật cấp quốc gia. Từ đây có chuyến tàu cổ chạy đến Trại Mát."),
            ("Da Lat Railway Station", "Da Lat Railway Station began construction in 1932 and is famous for its distinctive pointed roofs. It is recognized as a national architectural and artistic monument, and a heritage train runs from here to Trai Mat."),
            ("大叻火车站", "大叻火车站于1932年开始修建，以独特的尖顶建筑闻名，被列为国家级建筑艺术遗迹。游客可以从这里乘坐古老的列车前往附近的Trại Mát站。"),
            ("ダラット駅", "ダラット駅は1932年に着工し、独特の尖った屋根の建築で知られ、国家級の建築芸術遺跡に認定されています。ここからトライマット駅まで古い列車が走っています。"),
            ("달랏 기차역", "달랏 기차역은 1932년에 착공되었으며 독특한 뾰족 지붕 건축으로 유명하고 국가 건축 예술 유적으로 지정되었습니다. 이곳에서 짜이맛까지 운행하는 옛 열차를 탈 수 있습니다.")
        ]),
        new("crazy-house", 11.9354, 108.4308, 100, "3 Huỳnh Thúc Kháng, Phường 4, Đà Lạt",
        [
            ("Biệt thự Hằng Nga (Crazy House)", "Biệt thự Hằng Nga, còn gọi là Crazy House, do kiến trúc sư Đặng Việt Nga thiết kế với những hình khối uốn lượn như thân cây, hang động và mạng nhện. Đây vừa là nhà nghỉ độc đáo vừa là điểm tham quan nổi tiếng."),
            ("Crazy House", "Hang Nga Guesthouse, known as Crazy House, was designed by architect Dang Viet Nga with flowing forms resembling tree trunks, caves and spider webs. It is both a quirky guesthouse and a popular attraction."),
            ("疯狂屋", "嫦娥别墅又称疯狂屋，由建筑师邓越娥设计，造型像树干、洞穴和蜘蛛网一样蜿蜒曲折。它既是特色旅馆，也是热门景点。"),
            ("クレイジーハウス", "ハンガ・ゲストハウス（クレイジーハウス）は建築家ダン・ヴィエット・ガーが設計し、木の幹や洞窟、蜘蛛の巣のような曲線的な造形が特徴です。ユニークな宿泊施設であり、人気の観光名所でもあります。"),
            ("크레이지 하우스", "항응아 게스트하우스(크레이지 하우스)는 건축가 당비엣응아가 설계했으며 나무줄기, 동굴, 거미줄을 닮은 굽이치는 형태가 특징입니다. 독특한 숙소이자 인기 관광 명소입니다.")
        ]),
        new("dinh-bao-dai", 11.9308, 108.4497, 120, "Triệu Việt Vương, Phường 4, Đà Lạt",
        [
            ("Dinh Bảo Đại (Dinh III)", "Dinh Bảo Đại, hay Dinh III, được xây dựng vào những năm 1930 làm nơi nghỉ mát của vua Bảo Đại và gia đình. Bên trong còn lưu giữ nhiều đồ nội thất và vật dụng của hoàng gia."),
            ("Bao Dai Palace (Dinh III)", "Bao Dai Palace, also called Dinh III, was built in the 1930s as a summer retreat for Emperor Bao Dai and his family. Inside, much of the royal furniture and belongings is still preserved."),
            ("保大行宫（第三行宫）", "保大行宫又称第三行宫，建于20世纪30年代，是保大帝及其家人的避暑之地。宫内仍保存着许多皇室家具和物品。"),
            ("バオダイ帝の別荘（ディンIII）", "バオダイ帝の別荘（ディンIII）は1930年代に、バオダイ帝とその家族の避暑地として建てられました。館内には王室の家具や調度品が数多く保存されています。"),
            ("바오다이 황제 별궁(딘 3)", "바오다이 황제의 별궁(딘 3)은 1930년대에 바오다이 황제와 가족의 피서지로 지어졌습니다. 내부에는 왕실의 가구와 물품이 많이 보존되어 있습니다.")
        ]),
        new("truc-lam", 11.9006, 108.4285, 200, "Phù Đổng Thiên Vương, Phường 3, Đà Lạt",
        [
            ("Thiền viện Trúc Lâm", "Thiền viện Trúc Lâm nằm trên núi Phụng Hoàng, nhìn xuống hồ Tuyền Lâm, được xây dựng vào năm 1993. Không gian yên tĩnh giữa rừng thông và vườn hoa là nơi lý tưởng để thư giãn và tìm sự bình an."),
            ("Truc Lam Zen Monastery", "Truc Lam Zen Monastery stands on Phung Hoang mountain overlooking Tuyen Lam Lake and was built in 1993. Its quiet setting among pine forest and flower gardens is ideal for relaxing and finding peace."),
            ("竹林禅院", "竹林禅院建于1993年，坐落在凤凰山上，俯瞰春林湖。松林与花园环绕的宁静环境，是放松身心、寻找平静的理想之地。"),
            ("チュックラム禅院", "チュックラム禅院は1993年に建立され、フォンホアン山の上からトゥエンラム湖を見下ろしています。松林と花園に囲まれた静かな環境は、くつろぎと安らぎを求めるのに最適です。"),
            ("쭉럼 선원", "쭉럼 선원은 1993년에 세워졌으며 퐁호앙산 위에서 뚜옌럼 호수를 내려다봅니다. 소나무 숲과 꽃밭에 둘러싸인 고요한 분위기는 휴식과 평온을 찾기에 이상적입니다.")
        ])
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await EnsureDatabaseAsync(ct);
        await SeedLanguagesAsync(ct);
        await SeedPlacesAsync(ct);
        await SeedAdminAsync(ct);
    }

    /// <summary>MySQL có thể chưa nhận kết nối ngay khi container khởi động nên thử lại vài lần.</summary>
    private async Task EnsureDatabaseAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { await db.Database.EnsureCreatedAsync(ct); return; }
            catch (Exception ex) when (attempt < 10)
            {
                log.LogWarning("CSDL chưa sẵn sàng (lần {Attempt}/10): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }

    private async Task SeedLanguagesAsync(CancellationToken ct)
    {
        if (await db.Languages.AnyAsync(ct)) return;
        db.Languages.AddRange(
            new Language { Code = "vi", Name = "Vietnamese", NativeName = "Tiếng Việt", SpeechLocale = "vi-VN", SortOrder = 1 },
            new Language { Code = "en", Name = "English", NativeName = "English", SpeechLocale = "en-US", SortOrder = 2 },
            new Language { Code = "zh", Name = "Chinese", NativeName = "中文", SpeechLocale = "zh-CN", SortOrder = 3 },
            new Language { Code = "ja", Name = "Japanese", NativeName = "日本語", SpeechLocale = "ja-JP", SortOrder = 4 },
            new Language { Code = "ko", Name = "Korean", NativeName = "한국어", SpeechLocale = "ko-KR", SortOrder = 5 });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedPlacesAsync(CancellationToken ct)
    {
        if (await db.Places.AnyAsync(ct)) return;
        foreach (var s in Places)
        {
            var place = new Place
            {
                Slug = s.Slug, Latitude = s.Lat, Longitude = s.Lng, RadiusMeters = s.Radius, Address = s.Address
            };
            for (var i = 0; i < Codes.Length; i++)
                place.Translations.Add(new PlaceTranslation
                {
                    LanguageCode = Codes[i], Name = s.T[i].Name, NarrationText = s.T[i].Text
                });
            db.Places.Add(place);
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Đã nạp {Count} địa điểm mẫu.", Places.Length);
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        if (await db.AdminUsers.AnyAsync(ct)) return;
        var username = config["Admin:Username"] ?? "admin";
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            password = "ChangeMe@123";
            log.LogWarning("Chưa cấu hình ADMIN_PASSWORD (>= 8 ký tự); dùng mật khẩu mặc định. Hãy đổi ngay!");
        }
        db.AdminUsers.Add(new AdminUser
        {
            Username = username, PasswordHash = PasswordHasher.Hash(password), DisplayName = "Quản trị viên"
        });
        await db.SaveChangesAsync(ct);
    }
}
