using DaLat.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaLat.DAL.Repositories;

public interface IPlaceRepository
{
    Task<List<Place>> GetActiveWithTranslationsAsync();
    Task<List<Place>> GetAllWithTranslationsAsync();
    Task<Place?> GetByIdAsync(int id);
    Task<bool> SlugExistsAsync(string slug, int? exceptId);
    Task AddAsync(Place place);
    void Remove(Place place);
    Task SaveAsync();
}

public interface ILanguageRepository
{
    Task<List<Language>> GetActiveAsync();
    Task<List<Language>> GetAllAsync();
    Task<Language?> FindAsync(string code);
    Task AddAsync(Language language);
    Task SaveAsync();
}

public interface IVisitorRepository
{
    Task<Visitor?> FindVisitorAsync(string clientId);
    Task<VisitSession?> FindSessionAsync(string sessionKey);
    Task AddVisitorAsync(Visitor visitor);
    Task AddSessionAsync(VisitSession session);
    Task AddPlayLogAsync(PlayLog log);
    Task<List<VisitSession>> GetOnlineSessionsAsync(DateTime since);
    Task<int> DeleteSessionsOlderThanAsync(DateTime cutoff);
    IQueryable<VisitSession> Sessions { get; }
    IQueryable<PlayLog> PlayLogs { get; }
    Task SaveAsync();
}

public interface IAdminUserRepository
{
    Task<AdminUser?> FindByUsernameAsync(string username);
    Task<AdminUser?> FindByIdAsync(int id);
    Task SaveAsync();
}

public class PlaceRepository(AppDbContext db) : IPlaceRepository
{
    private IQueryable<Place> WithTranslations() =>
        db.Places.Include(p => p.Translations).ThenInclude(t => t.Language);

    public Task<List<Place>> GetActiveWithTranslationsAsync() =>
        WithTranslations().AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync();

    public Task<List<Place>> GetAllWithTranslationsAsync() =>
        WithTranslations().AsNoTracking().OrderBy(p => p.Id).ToListAsync();

    public Task<Place?> GetByIdAsync(int id) =>
        WithTranslations().FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> SlugExistsAsync(string slug, int? exceptId) =>
        db.Places.AnyAsync(p => p.Slug == slug && (exceptId == null || p.Id != exceptId));

    public async Task AddAsync(Place place) => await db.Places.AddAsync(place);
    public void Remove(Place place) => db.Places.Remove(place);
    public Task SaveAsync() => db.SaveChangesAsync();
}

public class LanguageRepository(AppDbContext db) : ILanguageRepository
{
    public Task<List<Language>> GetActiveAsync() =>
        db.Languages.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.SortOrder).ToListAsync();

    public Task<List<Language>> GetAllAsync() =>
        db.Languages.AsNoTracking().OrderBy(l => l.SortOrder).ToListAsync();

    public Task<Language?> FindAsync(string code) =>
        db.Languages.FirstOrDefaultAsync(l => l.Code == code);

    public async Task AddAsync(Language language) => await db.Languages.AddAsync(language);
    public Task SaveAsync() => db.SaveChangesAsync();
}

public class VisitorRepository(AppDbContext db) : IVisitorRepository
{
    public IQueryable<VisitSession> Sessions => db.VisitSessions.AsNoTracking();
    public IQueryable<PlayLog> PlayLogs => db.PlayLogs.AsNoTracking();

    public Task<Visitor?> FindVisitorAsync(string clientId) =>
        db.Visitors.FirstOrDefaultAsync(v => v.ClientId == clientId);

    public Task<VisitSession?> FindSessionAsync(string sessionKey) =>
        db.VisitSessions.FirstOrDefaultAsync(s => s.SessionKey == sessionKey);

    public async Task AddVisitorAsync(Visitor visitor) => await db.Visitors.AddAsync(visitor);
    public async Task AddSessionAsync(VisitSession session) => await db.VisitSessions.AddAsync(session);
    public async Task AddPlayLogAsync(PlayLog log) => await db.PlayLogs.AddAsync(log);

    public Task<List<VisitSession>> GetOnlineSessionsAsync(DateTime since) =>
        db.VisitSessions.AsNoTracking().Where(s => s.LastSeenAt >= since)
            .OrderByDescending(s => s.LastSeenAt).ToListAsync();

    public Task<int> DeleteSessionsOlderThanAsync(DateTime cutoff) =>
        db.VisitSessions.Where(s => s.LastSeenAt < cutoff).ExecuteDeleteAsync();

    public Task SaveAsync() => db.SaveChangesAsync();
}

public class AdminUserRepository(AppDbContext db) : IAdminUserRepository
{
    public Task<AdminUser?> FindByUsernameAsync(string username) =>
        db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);

    public Task<AdminUser?> FindByIdAsync(int id) =>
        db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id);

    public Task SaveAsync() => db.SaveChangesAsync();
}
