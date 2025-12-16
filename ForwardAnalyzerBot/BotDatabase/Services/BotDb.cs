using Microsoft.EntityFrameworkCore;
using BotDatabase.Entities;

namespace BotDatabase.Services;

/// <summary>
/// Main database service with fluent API.
///
/// Usage:
///   var db = new BotDb("bot.db");
///   await db.InitializeAsync();
///
///   // Store message
///   var msg = await db.Messages.Store(message);
///
///   // Query messages
///   var recent = await db.Messages.FromChat(chatId).Recent(20).ToListAsync();
///
///   // Create todo
///   var todo = await db.Todos.Create("Review article").ForUser(userId).ExecuteAsync();
/// </summary>
public class BotDb : IDisposable
{
    private readonly BotDbContext _context;
    private bool _disposed;

    public MessageRepository Messages { get; }
    public UserRepository Users { get; }
    public ChatRepository Chats { get; }
    public TodoRepository Todos { get; }
    public NoteRepository Notes { get; }
    public AnalysisRepository Analysis { get; }

    public BotDb(string dbPath = "bot.db")
    {
        _context = new BotDbContext(dbPath);
        Messages = new MessageRepository(_context);
        Users = new UserRepository(_context);
        Chats = new ChatRepository(_context);
        Todos = new TodoRepository(_context);
        Notes = new NoteRepository(_context);
        Analysis = new AnalysisRepository(_context);
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task SaveAsync()
    {
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _context.Dispose();
    }
}

// ========== Message Repository ==========

public class MessageRepository
{
    private readonly BotDbContext _context;

    public MessageRepository(BotDbContext context) => _context = context;

    public async Task<Message> Store(Message message)
    {
        message.CreatedAt = DateTime.UtcNow;
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task<Message> Find(int id)
    {
        return await _context.Messages
            .Include(m => m.ForwardSource)
            .Include(m => m.AnalysisResult)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<Message> FindByTelegramId(long messageId, long chatId)
    {
        return await _context.Messages
            .Include(m => m.ForwardSource)
            .FirstOrDefaultAsync(m => m.TelegramMessageId == messageId && m.ChatId == chatId);
    }

    public async Task<bool> Exists(long telegramMessageId, long chatId)
    {
        return await _context.Messages.AnyAsync(m => m.TelegramMessageId == telegramMessageId && m.ChatId == chatId);
    }

    public MessageQuery FromChat(long chatId) => new MessageQuery(_context).FromChat(chatId);
    public MessageQuery FromUser(long userId) => new MessageQuery(_context).FromUser(userId);
    public MessageQuery Forwarded() => new MessageQuery(_context).ForwardedOnly();
    public MessageQuery Recent(int count = 20) => new MessageQuery(_context).Recent(count);
}

public class MessageQuery
{
    private readonly BotDbContext _context;
    private IQueryable<Message> _query;
    private int _limit = 50;

    public MessageQuery(BotDbContext context)
    {
        _context = context;
        _query = context.Messages.AsQueryable();
    }

    public MessageQuery FromChat(long chatId) { _query = _query.Where(m => m.ChatId == chatId); return this; }
    public MessageQuery FromUser(long userId) { _query = _query.Where(m => m.UserId == userId); return this; }
    public MessageQuery ForwardedOnly() { _query = _query.Where(m => m.ForwardSourceId != 0); return this; }
    public MessageQuery OfType(string contentType) { _query = _query.Where(m => m.ContentType == contentType); return this; }
    public MessageQuery After(DateTime date) { _query = _query.Where(m => m.SentAt > date); return this; }
    public MessageQuery Before(DateTime date) { _query = _query.Where(m => m.SentAt < date); return this; }
    public MessageQuery Recent(int count) { _limit = count; _query = _query.OrderByDescending(m => m.SentAt); return this; }
    public MessageQuery Limit(int count) { _limit = count; return this; }
    public MessageQuery WithForwardInfo() { _query = _query.Include(m => m.ForwardSource); return this; }
    public MessageQuery WithAnalysis() { _query = _query.Include(m => m.AnalysisResult); return this; }

    public async Task<List<Message>> ToListAsync() => await _query.Take(_limit).ToListAsync();
    public async Task<Message> FirstAsync() => await _query.FirstOrDefaultAsync();
    public async Task<int> CountAsync() => await _query.CountAsync();
}

// ========== User Repository ==========

public class UserRepository
{
    private readonly BotDbContext _context;

    public UserRepository(BotDbContext context) => _context = context;

    public async Task<User> Find(long userId) => await _context.Users.FindAsync(userId);

    public async Task<User> FindByUsername(string username)
    {
        var normalized = username.TrimStart('@').ToLower();
        return await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized);
    }

    public async Task<User> GetOrCreate(long userId, string username = "", string firstName = "", string lastName = "")
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(username) && user.Username != username) { user.Username = username; changed = true; }
            if (!string.IsNullOrEmpty(firstName) && user.FirstName != firstName) { user.FirstName = firstName; changed = true; }
            if (!string.IsNullOrEmpty(lastName) && user.LastName != lastName) { user.LastName = lastName; changed = true; }
            if (changed) { user.UpdatedAt = DateTime.UtcNow; await _context.SaveChangesAsync(); }
            return user;
        }

        user = new User
        {
            UserId = userId,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<List<User>> AllAsync() => await _context.Users.ToListAsync();
    public async Task<int> CountAsync() => await _context.Users.CountAsync();
}

// ========== Chat Repository ==========

public class ChatRepository
{
    private readonly BotDbContext _context;

    public ChatRepository(BotDbContext context) => _context = context;

    public async Task<Chat> Find(long chatId) => await _context.Chats.FindAsync(chatId);

    public async Task<Chat> GetOrCreate(long chatId, string chatType = "", string title = "", string username = "")
    {
        var chat = await _context.Chats.FindAsync(chatId);
        if (chat != null)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(chatType) && chat.ChatType != chatType) { chat.ChatType = chatType; changed = true; }
            if (!string.IsNullOrEmpty(title) && chat.Title != title) { chat.Title = title; changed = true; }
            if (!string.IsNullOrEmpty(username) && chat.Username != username) { chat.Username = username; changed = true; }
            if (changed) { chat.UpdatedAt = DateTime.UtcNow; await _context.SaveChangesAsync(); }
            return chat;
        }

        chat = new Chat
        {
            ChatId = chatId,
            ChatType = chatType,
            Title = title,
            Username = username,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Chats.Add(chat);
        await _context.SaveChangesAsync();
        return chat;
    }

    public async Task<List<Chat>> AllAsync() => await _context.Chats.ToListAsync();
    public async Task<int> CountAsync() => await _context.Chats.CountAsync();
}

// ========== Todo Repository ==========

public class TodoRepository
{
    private readonly BotDbContext _context;

    public TodoRepository(BotDbContext context) => _context = context;

    public TodoBuilder Create(string title) => new TodoBuilder(_context, title);

    public async Task<Todo> Find(int id) => await _context.Todos.FindAsync(id);

    public async Task Complete(int id)
    {
        var todo = await _context.Todos.FindAsync(id);
        if (todo != null)
        {
            todo.Status = TodoStatus.Completed;
            todo.CompletedAt = DateTime.UtcNow;
            todo.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task Cancel(int id)
    {
        var todo = await _context.Todos.FindAsync(id);
        if (todo != null)
        {
            todo.Status = TodoStatus.Cancelled;
            todo.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task Delete(int id)
    {
        var todo = await _context.Todos.FindAsync(id);
        if (todo != null)
        {
            _context.Todos.Remove(todo);
            await _context.SaveChangesAsync();
        }
    }

    public TodoQuery ForUser(long userId) => new TodoQuery(_context).ForUser(userId);
    public TodoQuery Pending() => new TodoQuery(_context).WithStatus(TodoStatus.Pending);
    public TodoQuery InProgress() => new TodoQuery(_context).WithStatus(TodoStatus.InProgress);
    public TodoQuery Overdue() => new TodoQuery(_context).Overdue();
}

public class TodoBuilder
{
    private readonly BotDbContext _context;
    private readonly Todo _todo;

    public TodoBuilder(BotDbContext context, string title)
    {
        _context = context;
        _todo = new Todo
        {
            Title = title,
            Status = TodoStatus.Pending,
            Priority = TodoPriority.Normal,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public TodoBuilder ForUser(long userId) { _todo.UserId = userId; return this; }
    public TodoBuilder InChat(long chatId) { _todo.ChatId = chatId; return this; }
    public TodoBuilder WithDescription(string desc) { _todo.Description = desc; return this; }
    public TodoBuilder WithPriority(string priority) { _todo.Priority = priority; return this; }
    public TodoBuilder DueAt(DateTime date) { _todo.DueAt = date; return this; }
    public TodoBuilder DueIn(TimeSpan duration) { _todo.DueAt = DateTime.UtcNow.Add(duration); return this; }

    public async Task<Todo> ExecuteAsync()
    {
        _context.Todos.Add(_todo);
        await _context.SaveChangesAsync();
        return _todo;
    }
}

public class TodoQuery
{
    private readonly BotDbContext _context;
    private IQueryable<Todo> _query;
    private int _limit = 50;

    public TodoQuery(BotDbContext context)
    {
        _context = context;
        _query = context.Todos.AsQueryable();
    }

    public TodoQuery ForUser(long userId) { _query = _query.Where(t => t.UserId == userId); return this; }
    public TodoQuery InChat(long chatId) { _query = _query.Where(t => t.ChatId == chatId); return this; }
    public TodoQuery WithStatus(string status) { _query = _query.Where(t => t.Status == status); return this; }
    public TodoQuery WithPriority(string priority) { _query = _query.Where(t => t.Priority == priority); return this; }
    public TodoQuery Overdue() { _query = _query.Where(t => t.DueAt < DateTime.UtcNow && t.Status != TodoStatus.Completed); return this; }
    public TodoQuery Limit(int count) { _limit = count; return this; }
    public TodoQuery OrderByDue() { _query = _query.OrderBy(t => t.DueAt); return this; }
    public TodoQuery OrderByPriority() { _query = _query.OrderByDescending(t => t.Priority); return this; }

    public async Task<List<Todo>> ToListAsync() => await _query.Take(_limit).ToListAsync();
    public async Task<Todo> FirstAsync() => await _query.FirstOrDefaultAsync();
    public async Task<int> CountAsync() => await _query.CountAsync();
}

// ========== Note Repository ==========

public class NoteRepository
{
    private readonly BotDbContext _context;

    public NoteRepository(BotDbContext context) => _context = context;

    public NoteBuilder Create(string title) => new NoteBuilder(_context, title);

    public async Task<Note> Find(int id) => await _context.Notes.FindAsync(id);

    public async Task Delete(int id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note != null)
        {
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
        }
    }

    public async Task Pin(int id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note != null)
        {
            note.IsPinned = true;
            note.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task Unpin(int id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note != null)
        {
            note.IsPinned = false;
            note.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public NoteQuery ForUser(long userId) => new NoteQuery(_context).ForUser(userId);
    public NoteQuery Pinned() => new NoteQuery(_context).PinnedOnly();
    public NoteQuery WithTag(string tag) => new NoteQuery(_context).WithTag(tag);
}

public class NoteBuilder
{
    private readonly BotDbContext _context;
    private readonly Note _note;

    public NoteBuilder(BotDbContext context, string title)
    {
        _context = context;
        _note = new Note
        {
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public NoteBuilder ForUser(long userId) { _note.UserId = userId; return this; }
    public NoteBuilder InChat(long chatId) { _note.ChatId = chatId; return this; }
    public NoteBuilder WithContent(string content) { _note.Content = content; return this; }
    public NoteBuilder WithTags(params string[] tags) { _note.Tags = string.Join(",", tags); return this; }
    public NoteBuilder Pinned() { _note.IsPinned = true; return this; }

    public async Task<Note> ExecuteAsync()
    {
        _context.Notes.Add(_note);
        await _context.SaveChangesAsync();
        return _note;
    }
}

public class NoteQuery
{
    private readonly BotDbContext _context;
    private IQueryable<Note> _query;
    private int _limit = 50;

    public NoteQuery(BotDbContext context)
    {
        _context = context;
        _query = context.Notes.AsQueryable();
    }

    public NoteQuery ForUser(long userId) { _query = _query.Where(n => n.UserId == userId); return this; }
    public NoteQuery InChat(long chatId) { _query = _query.Where(n => n.ChatId == chatId); return this; }
    public NoteQuery PinnedOnly() { _query = _query.Where(n => n.IsPinned); return this; }
    public NoteQuery WithTag(string tag) { _query = _query.Where(n => n.Tags.Contains(tag)); return this; }
    public NoteQuery Search(string keyword) { _query = _query.Where(n => n.Title.Contains(keyword) || n.Content.Contains(keyword)); return this; }
    public NoteQuery Limit(int count) { _limit = count; return this; }
    public NoteQuery OrderByRecent() { _query = _query.OrderByDescending(n => n.UpdatedAt); return this; }

    public async Task<List<Note>> ToListAsync() => await _query.Take(_limit).ToListAsync();
    public async Task<Note> FirstAsync() => await _query.FirstOrDefaultAsync();
    public async Task<int> CountAsync() => await _query.CountAsync();
}

// ========== Analysis Repository ==========

public class AnalysisRepository
{
    private readonly BotDbContext _context;

    public AnalysisRepository(BotDbContext context) => _context = context;

    public async Task<AnalysisResult> Store(AnalysisResult result)
    {
        result.CreatedAt = DateTime.UtcNow;
        _context.AnalysisResults.Add(result);
        await _context.SaveChangesAsync();
        return result;
    }

    public async Task<AnalysisResult> Find(int id) => await _context.AnalysisResults.FindAsync(id);

    public async Task<AnalysisResult> ForMessage(int messageId)
    {
        return await _context.AnalysisResults.FirstOrDefaultAsync(a => a.MessageId == messageId);
    }

    public async Task MarkCompleted(int id, string result, int exitCode, long executionTimeMs)
    {
        var analysis = await _context.AnalysisResults.FindAsync(id);
        if (analysis != null)
        {
            analysis.Status = AnalysisStatus.Completed;
            analysis.Result = result;
            analysis.ExitCode = exitCode;
            analysis.ExecutionTimeMs = executionTimeMs;
            analysis.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkFailed(int id, string error, int exitCode)
    {
        var analysis = await _context.AnalysisResults.FindAsync(id);
        if (analysis != null)
        {
            analysis.Status = AnalysisStatus.Failed;
            analysis.Error = error;
            analysis.ExitCode = exitCode;
            analysis.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public AnalysisQuery Pending() => new AnalysisQuery(_context).WithStatus(AnalysisStatus.Pending);
    public AnalysisQuery Failed() => new AnalysisQuery(_context).WithStatus(AnalysisStatus.Failed);
}

public class AnalysisQuery
{
    private readonly BotDbContext _context;
    private IQueryable<AnalysisResult> _query;
    private int _limit = 50;

    public AnalysisQuery(BotDbContext context)
    {
        _context = context;
        _query = context.AnalysisResults.AsQueryable();
    }

    public AnalysisQuery WithStatus(string status) { _query = _query.Where(a => a.Status == status); return this; }
    public AnalysisQuery OfType(string scriptType) { _query = _query.Where(a => a.ScriptType == scriptType); return this; }
    public AnalysisQuery Limit(int count) { _limit = count; return this; }

    public async Task<List<AnalysisResult>> ToListAsync() => await _query.Take(_limit).ToListAsync();
    public async Task<int> CountAsync() => await _query.CountAsync();
}
