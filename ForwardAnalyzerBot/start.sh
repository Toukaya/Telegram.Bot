#!/bin/bash

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Load .env file if exists
if [ -f ".env" ]; then
    export $(grep -v '^#' .env | xargs)
fi

# Check token
if [ -z "$TELEGRAM_BOT_TOKEN" ]; then
    echo "Error: TELEGRAM_BOT_TOKEN is not set."
    echo ""
    echo "Options:"
    echo "  1. Create .env file with: TELEGRAM_BOT_TOKEN=your_token"
    echo "  2. Export: export TELEGRAM_BOT_TOKEN=your_token"
    exit 1
fi

# Build and run
dotnet build --configuration Release --verbosity quiet

if [ $? -ne 0 ]; then
    echo "Build failed."
    exit 1
fi

dotnet run --configuration Release --no-build
