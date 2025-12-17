using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BotDatabase.Entities;

namespace BotDatabase.Services;

public class BotDb : IDisposable
{
    private const string Tag = "BotDb";

    private readonly BotDbContext _context;
    private readonly string _dbPath;
    private bool _disposed;

    public MessageRepository Messages { get; }
    public UserRepository Users { get; }
    public ChatRepository Chats { get; }
    public TodoRepository Todos { get; }
    public NoteRepository Notes { get; }
    public AnalysisRepository Analysis { get; }
    public MediaFileRepository MediaFiles { get; }

    public BotDb(string dbPath = "bot.db")
    {
        _dbPath = dbPath;
        _context = new BotDbContext(dbPath);
        Messages = new MessageRepository(_context);
        Users = new UserRepository(_context);
        Chats = new ChatRepository(_context);
        Todos = new TodoRepository(_context);
        Notes = new NoteRepository(_context);
        Analysis = new AnalysisRepository(_context);
        MediaFiles = new MediaFileRepository(_context);
        DbLogger.Debug(Tag, $"Initialized with path: {dbPath}");
    }

    public async Task InitializeAsync()
    {
        DbLogger.Debug(Tag, "Ensuring database exists...");
        await _context.Database.EnsureCreatedAsync();
        DbLogger.Info(Tag, $"Database initialized: {_dbPath}");
    }

    public async Task SaveAsync()
    {
        await _context.SaveChangesAsync();
        DbLogger.Debug(Tag, "Changes saved");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _context.Dispose();
        DbLogger.Debug(Tag, "Disposed");
    }
}

// ========== Message Repository ==========

public class MessageRepository
{
    private const string Tag = "Messages";
    private readonly BotDbContext _context;

    public MessageRepository(BotDbContext context) => _context = context;

    public async Task<Message> Store(Message message)
    {
        message.CreatedAt = DateTime.UtcNow;
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        DbLogger.Debug(Tag, $"Stored message id={message.Id}, chat={message.ChatId}");
        return message;
    }

    public async Task<Message> Find(int id)
    {
        var msg = await _context.Messages
            .Include(m => m.ForwardSource)
            .Include(m => m.AnalysisResult)
            .FirstOrDefaultAsync(m => m.Id == id);
        DbLogger.Debug(Tag, msg != null ? $"Found message id={id}" : $"Message id={id} not found");
        return msg;
    }

    public async Task<Message> FindByTelegramId(long messageId, long chatId)
    {
        var msg = await _context.Messages
            .Include(m => m.ForwardSource)
            .FirstOrDefaultAsync(m => m.TelegramMessageId == messageId && m.ChatId == chatId);
        DbLogger.Debug(Tag, msg != null ? $"Found tg message {messageId} in chat {chatId}" : $"Tg message {messageId} not found in chat {chatId}");
        return msg;
    }

    public async Task<bool> Exists(long telegramMessageId, long chatId)
    {
        return await _context.Messages.AnyAsync(m => m.TelegramMessageId == telegramMessageId && m.ChatId == chatId);
    }

    public MessageQuery FromChat(long chatId) => new MessageQuery(_context).FromChat(chatId);
    public MessageQuery FromUser(long userId) => new MessageQuery(_context).FromUser(userId);
    public MessageQuery Forwarded() => new MessageQuery(_context).ForwardedOnly();
    public MessageQuery Recent(int count = 20) => new MessageQuery(_context).Recent(count);
    public MessageQuery PendingConversion() => new MessageQuery(_context).WithConversionStatus(ConversionStatuses.Pending);
    public MessageQuery WithMedia() => new MessageQuery(_context).MediaOnly();

    // Update conversion status
    public async Task UpdateConversionStatus(int id, string status, string convertedText = "", string error = "")
    {
        var msg = await _context.Messages.FindAsync(id);
        if (msg != null)
        {
            msg.ConversionStatus = status;
            if (!string.IsNullOrEmpty(convertedText))
            {
                msg.ConvertedText = convertedText;
            }
            if (!string.IsNullOrEmpty(error))
            {
                msg.ConversionError = error;
            }
            if (status == ConversionStatuses.Completed || status == ConversionStatuses.Failed)
            {
                msg.ConvertedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            DbLogger.Debug(Tag, $"Updated conversion status for message {id}: {status}");
        }
    }

    // Mark conversion as completed
    public async Task MarkConversionCompleted(int id, string convertedText)
    {
        await UpdateConversionStatus(id, ConversionStatuses.Completed, convertedText);
    }

    // Mark conversion as failed
    public async Task MarkConversionFailed(int id, string error)
    {
        await UpdateConversionStatus(id, ConversionStatuses.Failed, error: error);
    }

    // Mark conversion as skipped (e.g., text messages)
    public async Task MarkConversionSkipped(int id)
    {
        await UpdateConversionStatus(id, ConversionStatuses.Skipped);
    }

    // Mark conversion as unavailable (service not available)
    public async Task MarkConversionUnavailable(int id)
    {
        await UpdateConversionStatus(id, ConversionStatuses.Unavailable);
    }
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

    // Media and conversion queries
    public MessageQuery MediaOnly()
    {
        _query = _query.Where(m => m.ContentType != ContentTypes.Text && !string.IsNullOrEmpty(m.FileId));
        return this;
    }
    public MessageQuery WithConversionStatus(string status) { _query = _query.Where(m => m.ConversionStatus == status); return this; }
    public MessageQuery WithConvertedText() { _query = _query.Where(m => !string.IsNullOrEmpty(m.ConvertedText)); return this; }
    public MessageQuery SearchText(string keyword)
    {
        _query = _query.Where(m => m.Content.Contains(keyword) || m.ConvertedText.Contains(keyword));
        return this;
    }

    public async Task<List<Message>> ToListAsync() => await _query.Take(_limit).ToListAsync();
    public async Task<Message> FirstAsync() => await _query.FirstOrDefaultAsync();
    public async Task<int> CountAsync() => await _query.CountAsync();
}

// ========== User Repository ==========

public class UserRepository
{
    private const string Tag = "Users";
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
            if (changed)
            {
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                DbLogger.Debug(Tag, $"Updated user {userId} (@{username})");
            }
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
        DbLogger.Debug(Tag, $"Created user {userId} (@{username})");
        return user;
    }

    public async Task<List<User>> AllAsync() => await _context.Users.ToListAsync();
    public async Task<int> CountAsync() => await _context.Users.CountAsync();
}

// ========== Chat Repository ==========

public class ChatRepository
{
    private const string Tag = "Chats";
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
            if (changed)
            {
                chat.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                DbLogger.Debug(Tag, $"Updated chat {chatId} ({title})");
            }
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
        DbLogger.Debug(Tag, $"Created chat {chatId} type={chatType}");
        return chat;
    }

    public async Task<List<Chat>> AllAsync() => await _context.Chats.ToListAsync();
    public async Task<int> CountAsync() => await _context.Chats.CountAsync();
}

// ========== Todo Repository ==========

public class TodoRepository
{
    private const string Tag = "Todos";
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
            DbLogger.Debug(Tag, $"Completed todo {id}: {todo.Title}");
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
            DbLogger.Debug(Tag, $"Cancelled todo {id}: {todo.Title}");
        }
    }

    public async Task Delete(int id)
    {
        var todo = await _context.Todos.FindAsync(id);
        if (todo != null)
        {
            _context.Todos.Remove(todo);
            await _context.SaveChangesAsync();
            DbLogger.Debug(Tag, $"Deleted todo {id}");
        }
    }

    public TodoQuery ForUser(long userId) => new TodoQuery(_context).ForUser(userId);
    public TodoQuery Pending() => new TodoQuery(_context).WithStatus(TodoStatus.Pending);
    public TodoQuery InProgress() => new TodoQuery(_context).WithStatus(TodoStatus.InProgress);
    public TodoQuery Overdue() => new TodoQuery(_context).Overdue();
}

public class TodoBuilder
{
    private const string Tag = "Todos";
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
        DbLogger.Debug(Tag, $"Created todo {_todo.Id}: {_todo.Title}");
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
    private const string Tag = "Notes";
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
            DbLogger.Debug(Tag, $"Deleted note {id}");
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
            DbLogger.Debug(Tag, $"Pinned note {id}: {note.Title}");
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
            DbLogger.Debug(Tag, $"Unpinned note {id}: {note.Title}");
        }
    }

    public NoteQuery ForUser(long userId) => new NoteQuery(_context).ForUser(userId);
    public NoteQuery Pinned() => new NoteQuery(_context).PinnedOnly();
    public NoteQuery WithTag(string tag) => new NoteQuery(_context).WithTag(tag);
}

public class NoteBuilder
{
    private const string Tag = "Notes";
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
        DbLogger.Debug(Tag, $"Created note {_note.Id}: {_note.Title}");
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
    private const string Tag = "Analysis";
    private readonly BotDbContext _context;

    public AnalysisRepository(BotDbContext context) => _context = context;

    public async Task<AnalysisResult> Store(AnalysisResult result)
    {
        result.CreatedAt = DateTime.UtcNow;
        _context.AnalysisResults.Add(result);
        await _context.SaveChangesAsync();
        DbLogger.Debug(Tag, $"Stored analysis {result.Id} for message {result.MessageId}, status={result.Status}");
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
            DbLogger.Debug(Tag, $"Analysis {id} completed in {executionTimeMs}ms");
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
            DbLogger.Warn(Tag, $"Analysis {id} failed: {error}");
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

// ========== MediaFile Repository ==========

public class MediaFileRepository
{
    private const string Tag = "MediaFiles";
    private readonly BotDbContext _context;

    public MediaFileRepository(BotDbContext context) => _context = context;

    // Create new media file record
    public async Task<MediaFile> Create(MediaFile mediaFile)
    {
        if (string.IsNullOrEmpty(mediaFile.Id))
        {
            mediaFile.Id = Guid.NewGuid().ToString("N");
        }
        mediaFile.CreatedAt = DateTime.UtcNow;
        mediaFile.UpdatedAt = DateTime.UtcNow;

        _context.MediaFiles.Add(mediaFile);
        await _context.SaveChangesAsync();
        DbLogger.Debug(Tag, $"Created media file {mediaFile.Id}, type={mediaFile.FileType}");
        return mediaFile;
    }

    // Find by ID
    public async Task<MediaFile> Find(string id)
    {
        return await _context.MediaFiles.FindAsync(id);
    }

    // Find by Telegram file unique ID (for deduplication)
    public async Task<MediaFile> FindByTelegramFileId(string telegramFileUniqueId)
    {
        return await _context.MediaFiles
            .FirstOrDefaultAsync(m => m.TelegramFileUniqueId == telegramFileUniqueId);
    }

    // Check if file exists by Telegram unique ID
    public async Task<bool> Exists(string telegramFileUniqueId)
    {
        return await _context.MediaFiles
            .AnyAsync(m => m.TelegramFileUniqueId == telegramFileUniqueId);
    }

    // Update conversion status
    public async Task UpdateConvertStatus(string id, string status, string textContent = null, string error = null)
    {
        var file = await _context.MediaFiles.FindAsync(id);
        if (file != null)
        {
            file.ConvertStatus = status;
            file.UpdatedAt = DateTime.UtcNow;

            if (textContent != null)
            {
                file.TextContent = textContent;
            }
            if (error != null)
            {
                file.ConvertError = error;
            }
            if (status == MediaConvertStatus.Completed || status == MediaConvertStatus.Failed)
            {
                file.ConvertedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            DbLogger.Debug(Tag, $"Updated convert status for {id}: {status}");
        }
    }

    // Mark as converted
    public async Task MarkConverted(string id, string textContent)
    {
        await UpdateConvertStatus(id, MediaConvertStatus.Completed, textContent);
    }

    // Mark conversion failed
    public async Task MarkConvertFailed(string id, string error)
    {
        await UpdateConvertStatus(id, MediaConvertStatus.Failed, error: error);
    }

    // Mark as indexed in Kernel Memory
    public async Task MarkIndexed(string id)
    {
        var file = await _context.MediaFiles.FindAsync(id);
        if (file != null)
        {
            file.IsIndexed = true;
            file.IndexedAt = DateTime.UtcNow;
            file.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            DbLogger.Debug(Tag, $"Marked {id} as indexed");
        }
    }

    // Delete
    public async Task<bool> Delete(string id)
    {
        var file = await _context.MediaFiles.FindAsync(id);
        if (file != null)
        {
            _context.MediaFiles.Remove(file);
            await _context.SaveChangesAsync();
            DbLogger.Debug(Tag, $"Deleted media file {id}");
            return true;
        }
        return false;
    }

    // Queries
    public MediaFileQuery FromChat(long chatId) => new MediaFileQuery(_context).FromChat(chatId);
    public MediaFileQuery OfType(string fileType) => new MediaFileQuery(_context).OfType(fileType);
    public MediaFileQuery PendingConversion() => new MediaFileQuery(_context).WithConvertStatus(MediaConvertStatus.Pending);
    public MediaFileQuery NotIndexed() => new MediaFileQuery(_context).NotIndexed();
    public MediaFileQuery Recent(int count = 20) => new MediaFileQuery(_context).Recent(count);
}

public class MediaFileQuery
{
    private readonly BotDbContext _context;
    private IQueryable<MediaFile> _query;
    private int _limit = 50;

    public MediaFileQuery(BotDbContext context)
    {
        _context = context;
        _query = context.MediaFiles.AsQueryable();
    }

    public MediaFileQuery FromChat(long chatId) { _query = _query.Where(m => m.ChatId == chatId); return this; }
    public MediaFileQuery FromUser(long userId) { _query = _query.Where(m => m.UserId == userId); return this; }
    public MediaFileQuery OfType(string fileType) { _query = _query.Where(m => m.FileType == fileType); return this; }
    public MediaFileQuery WithConvertStatus(string status) { _query = _query.Where(m => m.ConvertStatus == status); return this; }
    public MediaFileQuery Converted() { _query = _query.Where(m => m.ConvertStatus == MediaConvertStatus.Completed); return this; }
    public MediaFileQuery NotIndexed() { _query = _query.Where(m => !m.IsIndexed); return this; }
    public MediaFileQuery Indexed() { _query = _query.Where(m => m.IsIndexed); return this; }
    public MediaFileQuery After(DateTime date) { _query = _query.Where(m => m.CreatedAt > date); return this; }
    public MediaFileQuery Before(DateTime date) { _query = _query.Where(m => m.CreatedAt < date); return this; }
    public MediaFileQuery Today()
    {
        var today = DateTime.UtcNow.Date;
        _query = _query.Where(m => m.CreatedAt >= today);
        return this;
    }
    public MediaFileQuery Recent(int count) { _limit = count; _query = _query.OrderByDescending(m => m.CreatedAt); return this; }
    public MediaFileQuery Limit(int count) { _limit = count; return this; }

    // Search in text content
    public MediaFileQuery SearchText(string keyword)
    {
        _query = _query.Where(m => m.TextContent.Contains(keyword));
        return this;
    }

    public async Task<List<MediaFile>> ToListAsync() => await _query.Take(_limit).ToListAsync();
    public async Task<MediaFile> FirstAsync() => await _query.FirstOrDefaultAsync();
    public async Task<int> CountAsync() => await _query.CountAsync();

    // Get all text content (for summary generation)
    public async Task<List<string>> GetAllTextAsync()
    {
        return await _query
            .Where(m => !string.IsNullOrEmpty(m.TextContent))
            .Select(m => m.TextContent)
            .Take(_limit)
            .ToListAsync();
    }
}
