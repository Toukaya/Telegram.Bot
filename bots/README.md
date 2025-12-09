# ForwardAnalyzerBot

A Telegram bot that analyzes forwarded messages, extracts original sender information, and runs custom analysis scripts.

## Features

- Extract original sender, date, and message link from forwarded messages
- Async task pool for sequential processing
- Separate scripts for text and media content analysis
- Structured JSON output

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

Create a `.env` file or set environment variables:

```bash
# Required
export TELEGRAM_BOT_TOKEN=your_bot_token_here

# Optional (defaults shown)
export TEXT_SCRIPT_PATH=./analyze_text.sh
export MEDIA_SCRIPT_PATH=./analyze_media.sh
```

## Usage

```bash
# Using start script
./start.sh

# Or directly
./ForwardAnalyzerBot
```

Then forward any message to your bot. It will return a JSON response with:

```json
{
  "sender": {
    "name": "John Doe",
    "id": "123456789",
    "username": "johndoe",
    "type": "User"
  },
  "source": {
    "originalDate": "2025-12-09T10:30:00Z",
    "forwardDate": "2025-12-09T10:35:00Z",
    "messageLink": "https://t.me/channel/123"
  },
  "content": {
    "type": "Text",
    "text": "Hello world"
  },
  "analysis": {
    "success": true,
    "result": "..."
  }
}
```

## Custom Analysis Scripts

### Text Script (`analyze_text.sh`)

Receives text content via:
- `$1` - escaped text as argument
- `stdin` - raw text

### Media Script (`analyze_media.sh`)

Receives:
- `$1` - media type (Photo, Video, Voice, Audio, VideoNote, Sticker, Document)
- `$2` - Telegram file ID
- `stdin` - caption (if any)

## Creating a Release

```bash
git tag bot-v1.0.0
git push origin bot-v1.0.0
```

This triggers GitHub Actions to build and publish a release.
