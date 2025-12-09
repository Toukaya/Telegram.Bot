#!/bin/bash
# Sample analysis script
# Input: message text (via argument or stdin)
# Output: analysis result (stdout)

# Read input from argument or stdin
if [ -n "$1" ]; then
    INPUT="$1"
else
    INPUT=$(cat)
fi

# Simple analysis example
CHAR_COUNT=${#INPUT}
WORD_COUNT=$(echo "$INPUT" | wc -w | tr -d ' ')
LINE_COUNT=$(echo "$INPUT" | wc -l | tr -d ' ')

# Check for URLs
URL_COUNT=$(echo "$INPUT" | grep -oE 'https?://[^ ]+' | wc -l | tr -d ' ')

# Check for mentions
MENTION_COUNT=$(echo "$INPUT" | grep -oE '@[a-zA-Z0-9_]+' | wc -l | tr -d ' ')

# Check for hashtags
HASHTAG_COUNT=$(echo "$INPUT" | grep -oE '#[a-zA-Z0-9_]+' | wc -l | tr -d ' ')

# Output analysis
cat << EOF
Text Analysis:
- Characters: $CHAR_COUNT
- Words: $WORD_COUNT
- Lines: $LINE_COUNT
- URLs: $URL_COUNT
- Mentions: $MENTION_COUNT
- Hashtags: $HASHTAG_COUNT
EOF
