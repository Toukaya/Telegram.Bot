# ForwardAnalyzerBot

A Telegram bot that analyzes forwarded messages, extracts original sender information, and runs custom analysis with support for C# plugins and PostgreSQL storage.

## Features

- Extract original sender, date, and message link from forwarded messages
- Async task pool for sequential processing
- Two analysis modes: C# plugin system (recommended) or shell scripts (legacy)
- PostgreSQL database with vector search for semantic retrieval
- Todo/task tracking system
- AI memory storage for long-term context
- Structured JSON output

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    ForwardAnalyzerBot                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐     ┌─────────────┐     ┌──────────────┐  │
│  │  Telegram   │────▶│   Message   │────▶│   Analyzer   │  │
│  │   Update    │     │   Handler   │     │   Service    │  │
│  └─────────────┘     └─────────────┘     └──────┬───────┘  │
│                                                  │          │
│                      ┌───────────────────────────┤          │
│                      ▼                           ▼          │
│               ┌─────────────┐           ┌──────────────┐   │
│               │   Plugins   │           │ Shell Scripts│   │
│               │   (C#)      │           │   (Legacy)   │   │
│               └─────────────┘           └──────────────┘   │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                    PostgreSQL                         │  │
│  │  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────────────┐ │  │
│  │  │Messages│ │ Todos  │ │Memories│ │ Vector Search  │ │  │
│  │  └────────┘ └────────┘ └────────┘ └────────────────┘ │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Installation

### Option 1: Download from GitHub Releases

```bash
# Download latest release
gh release download --pattern 'ForwardAnalyzerBot-*.tar.gz' --repo TelegramBots/Telegram.Bot

# Extract
tar -xzvf ForwardAnalyzerBot-linux-x64.tar.gz

# Run
./ForwardAnalyzerBot
```

### Option 2: Build from Source

```bash
cd bots
dotnet build
dotnet run
```

## Configuration

Create a `.env` file in the bot directory:

```bash
# Required
TELEGRAM_BOT_TOKEN=your_bot_token_here

# Analysis Mode (choose one)
# Option 1: C# Plugin System (recommended)
USE_PLUGINS=true
PLUGINS_PATH=./plugins

# Option 2: Legacy Shell Scripts
TEXT_SCRIPT_PATH=./analyze_text.sh
MEDIA_SCRIPT_PATH=./analyze_media.sh

# Database (optional, for message storage & AI features)
DATABASE_URL=Host=localhost;Database=forwardbot;Username=postgres;Password=yourpass
```

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `TELEGRAM_BOT_TOKEN` | Yes | - | Bot token from @BotFather |
| `USE_PLUGINS` | No | `false` | Enable C# plugin system |
| `PLUGINS_PATH` | No | `./plugins` | Directory for plugin DLLs |
| `TEXT_SCRIPT_PATH` | No | `./analyze_text.sh` | Text analysis script (legacy) |
| `MEDIA_SCRIPT_PATH` | No | `./analyze_media.sh` | Media analysis script (legacy) |
| `DATABASE_URL` | No | - | PostgreSQL connection string |

## Usage

### Starting the Bot

```bash
# Using start script (loads .env automatically)
./start.sh

# Or directly
./ForwardAnalyzerBot
```

### Basic Workflow

1. Forward any message to the bot
2. Bot extracts sender info, date, and message link
3. Bot runs analysis (via plugins or scripts)
4. Bot returns structured JSON response

### Example Output

```json
{
  "sender": {
    "name": "John Doe",
    "id": "123456789",
    "username": "johndoe",
    "type": "User",
    "signature": ""
  },
  "source": {
    "originalDate": "2025-12-09T10:30:00Z",
    "forwardDate": "2025-12-09T10:35:00Z",
    "messageLink": "https://t.me/channel/123",
    "originalMessageId": 123,
    "chatTitle": "News Channel",
    "chatId": -1001234567890
  },
  "content": {
    "type": "Text",
    "text": "Hello world",
    "caption": "",
    "fileName": "",
    "fileId": "",
    "fileSize": 0
  },
  "analysis": {
    "success": true,
    "result": "Analysis result here...",
    "error": "",
    "processingTimeMs": 45.2
  },
  "meta": {
    "processedAt": "2025-12-09T10:35:01Z",
    "botVersion": "1.0.0",
    "receivedFromChatId": 123456789,
    "receivedFromUserId": 987654321
  }
}
```

## Analysis Modes

### Mode 1: C# Plugin System (Recommended)

Enable with `USE_PLUGINS=true`. Plugins are C# classes implementing `IAnalyzer` interface.

**Advantages:**
- Type-safe
- Better performance
- Hot reload support
- Access to full .NET ecosystem

**Creating a Plugin:**

```csharp
public class MyAnalyzer : IAnalyzer
{
    public string Name => "MyAnalyzer";
    public bool CanHandle(AnalyzerContext context) => context.ContentType == "Text";

    public Task<AnalysisResult> AnalyzeAsync(AnalyzerContext context, CancellationToken ct)
    {
        // Your analysis logic here
        return Task.FromResult(new AnalysisResult
        {
            Success = true,
            Result = "Analysis complete"
        });
    }
}
```

### Mode 2: Shell Scripts (Legacy)

Default mode. Uses external shell scripts for analysis.

**Text Script (`analyze_text.sh`):**
- `$1` - escaped text as argument
- `stdin` - raw text

**Media Script (`analyze_media.sh`):**
- `$1` - media type (Photo, Video, Voice, Audio, etc.)
- `$2` - Telegram file ID
- `stdin` - caption (if any)

## Database Setup

### PostgreSQL with pgvector

The bot uses PostgreSQL with the pgvector extension for:
- Message storage
- Semantic search (vector similarity)
- Todo/task tracking
- AI memory/context

### Installation

**Docker (Recommended):**
```bash
docker run -d \
  --name forwardbot-db \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=yourpass \
  -e POSTGRES_DB=forwardbot \
  -p 5432:5432 \
  pgvector/pgvector:pg16
```

**Manual Installation:**
```bash
# Ubuntu/Debian
sudo apt install postgresql postgresql-contrib postgresql-16-pgvector

# macOS
brew install postgresql@16 pgvector
```

### Schema Setup

```bash
# Run the schema file
psql -U postgres -h localhost -d forwardbot -f Database/schema.sql
```

### Connection String Format

```
Host=localhost;Database=forwardbot;Username=postgres;Password=yourpass
     │              │                  │               │
     │              │                  │               └── Password
     │              │                  └── Username
     │              └── Database name
     └── Server host

# With additional options
Host=localhost;Port=5432;Database=forwardbot;Username=postgres;Password=yourpass;SSL Mode=Prefer
```

## Database Schema

### Core Tables

| Table | Purpose |
|-------|---------|
| `users` | Telegram user info |
| `chats` | Chat/channel/group info |
| `messages` | Message content + vector embedding |
| `forward_sources` | Original source info for forwards |

### Todo System

| Table | Purpose |
|-------|---------|
| `todos` | Task items with status, priority, hierarchy |
| `todo_assignees` | Task assignments (many-to-many) |
| `todo_comments` | Comments on tasks |
| `todo_history` | Audit trail |

### AI Features

| Table | Purpose |
|-------|---------|
| `memories` | Long-term memory with vector |
| `tags` | Shared tagging system |
| `session_state` | Conversation state |

### Vector Search

```sql
-- Find semantically similar messages
SELECT * FROM search_messages_semantic(
    query_vector := '[0.1, 0.2, ...]'::vector,
    limit_count := 10,
    chat_filter := 123456789
);

-- Get relevant memories for AI context
SELECT * FROM get_relevant_memories(
    query_vector := '[0.1, 0.2, ...]'::vector,
    p_user_id := 123,
    p_chat_id := 456,
    limit_count := 5
);
```

## Project Structure

```
bots/
├── Bot/
│   ├── BotService.cs           # Bot lifecycle management
│   └── MessageHandler.cs       # Message processing
├── Database/
│   ├── schema.sql              # PostgreSQL schema
│   ├── DESIGN.md               # Database design docs
│   ├── BotDbContext.cs         # EF Core context
│   ├── DbContextFactory.cs     # DB initialization
│   ├── Entities/               # Entity classes
│   │   ├── User.cs
│   │   ├── Chat.cs
│   │   ├── Message.cs
│   │   ├── Todo.cs
│   │   └── ...
│   └── Repositories/           # Data access layer
│       ├── IMessageRepository.cs
│       ├── ITodoRepository.cs
│       ├── IMemoryRepository.cs
│       └── ...
├── Models/                     # JSON output models
│   ├── ForwardAnalysisResult.cs
│   ├── SenderInfo.cs
│   └── ...
├── Services/
│   ├── TaskPool.cs             # Async task queue
│   ├── ForwardInfoExtractor.cs # Extract forward info
│   └── ScriptRunner.cs         # Shell script runner
├── TelegramBotService/         # Plugin system
├── analyze_text.sh             # Text analysis script
├── analyze_media.sh            # Media analysis script
├── start.sh                    # Startup script
├── .env.example                # Example configuration
└── README.md                   # This file
```

## Creating a Release

### Manual Trigger (GitHub Actions)

1. Go to GitHub → Actions → "Release ForwardAnalyzerBot"
2. Click "Run workflow"
3. Enter release notes (required)
4. Version (optional, auto-increments if empty)

### Server Download

```bash
# Download latest release
gh release download --pattern 'ForwardAnalyzerBot-*.tar.gz' --repo YourUsername/Telegram.Bot

# Extract
tar -xzvf ForwardAnalyzerBot-linux-x64.tar.gz

# Setup
cp .env.example .env
nano .env  # Edit configuration

# Run
./start.sh
```

## API Reference

### Message Types

| Type | Description |
|------|-------------|
| `Text` | Plain text message |
| `Photo` | Image |
| `Video` | Video file |
| `Voice` | Voice message |
| `Audio` | Audio file |
| `Document` | File/document |
| `Sticker` | Sticker |
| `VideoNote` | Video note (round video) |

### Forward Source Types

| Type | Description |
|------|-------------|
| `User` | From a known user |
| `HiddenUser` | User with hidden forward |
| `Chat` | From a group/chat |
| `Channel` | From a channel (has message link) |

### Todo Statuses

| Status | Description |
|--------|-------------|
| `Pending` | Not started |
| `InProgress` | Currently working |
| `Completed` | Done |
| `Cancelled` | Cancelled |
| `Blocked` | Blocked by something |

### Todo Priorities

| Priority | Value | Description |
|----------|-------|-------------|
| Normal | 0 | Default |
| Low | 1 | Nice to have |
| Medium | 2 | Should do soon |
| High | 3 | Important |
| Urgent | 4 | Do immediately |

## Troubleshooting

### Bot doesn't respond
- Check `TELEGRAM_BOT_TOKEN` is set correctly
- Verify bot is not blocked
- Check console for error messages

### Database connection fails
- Verify PostgreSQL is running
- Check `DATABASE_URL` format
- Ensure pgvector extension is installed

### Plugin not loading
- Ensure `USE_PLUGINS=true`
- Check plugin DLL is in `PLUGINS_PATH`
- Verify plugin implements `IAnalyzer` interface

### Vector search not working
- Run `CREATE EXTENSION vector;` in PostgreSQL
- Ensure messages have embeddings (content_vector not null)
- Check vector dimension matches (1536 for OpenAI ada-002)

## License

MIT License
