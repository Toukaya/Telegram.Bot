using ForwardAnalyzerBot.Models;
using Telegram.Bot.Types;

namespace ForwardAnalyzerBot.Services;

public static class ForwardInfoExtractor
{
    public static ForwardAnalysisResult Extract(Message message)
    {
        var result = new ForwardAnalysisResult
        {
            Meta = new MetaInfo
            {
                ProcessedAt = DateTime.UtcNow,
                ReceivedFromChatId = message.Chat.Id,
                ReceivedFromUserId = message.From != null ? message.From.Id : 0
            },
            Source = new SourceInfo
            {
                ForwardDate = message.Date
            }
        };

        ExtractSenderInfo(message, result);
        ExtractContentInfo(message, result);

        return result;
    }

    private static void ExtractSenderInfo(Message message, ForwardAnalysisResult result)
    {
        var origin = message.ForwardOrigin;
        if (origin == null) return;

        if (origin is MessageOriginUser userOrigin)
        {
            var user = userOrigin.SenderUser;
            result.Sender = new SenderInfo
            {
                Name = BuildFullName(user.FirstName, user.LastName),
                Id = user.Id.ToString(),
                Username = user.Username != null ? user.Username : "",
                Type = "User"
            };
            result.Source.OriginalDate = userOrigin.Date;
        }
        else if (origin is MessageOriginHiddenUser hiddenOrigin)
        {
            result.Sender = new SenderInfo
            {
                Name = hiddenOrigin.SenderUserName,
                Type = "HiddenUser"
            };
            result.Source.OriginalDate = hiddenOrigin.Date;
        }
        else if (origin is MessageOriginChat chatOrigin)
        {
            var chat = chatOrigin.SenderChat;
            result.Sender = new SenderInfo
            {
                Name = chat.Title != null ? chat.Title : "",
                Id = chat.Id.ToString(),
                Username = chat.Username != null ? chat.Username : "",
                Type = "Chat",
                Signature = chatOrigin.AuthorSignature != null ? chatOrigin.AuthorSignature : ""
            };
            result.Source.OriginalDate = chatOrigin.Date;
            result.Source.ChatTitle = chat.Title != null ? chat.Title : "";
            result.Source.ChatId = chat.Id;
        }
        else if (origin is MessageOriginChannel channelOrigin)
        {
            var chat = channelOrigin.Chat;
            result.Sender = new SenderInfo
            {
                Name = chat.Title != null ? chat.Title : "",
                Id = chat.Id.ToString(),
                Username = chat.Username != null ? chat.Username : "",
                Type = "Channel",
                Signature = channelOrigin.AuthorSignature != null ? channelOrigin.AuthorSignature : ""
            };
            result.Source.OriginalDate = channelOrigin.Date;
            result.Source.OriginalMessageId = channelOrigin.MessageId;
            result.Source.ChatTitle = chat.Title != null ? chat.Title : "";
            result.Source.ChatId = chat.Id;
            result.Source.MessageLink = BuildChannelLink(chat, channelOrigin.MessageId);
        }
    }

    private static void ExtractContentInfo(Message message, ForwardAnalysisResult result)
    {
        var content = new ContentInfo();

        if (!string.IsNullOrEmpty(message.Text))
        {
            content.Type = "Text";
            content.Text = message.Text;
        }
        else if (message.Photo != null && message.Photo.Length > 0)
        {
            var photo = message.Photo[message.Photo.Length - 1];
            content.Type = "Photo";
            content.Caption = message.Caption != null ? message.Caption : "";
            content.FileId = photo.FileId;
            content.FileSize = photo.FileSize.GetValueOrDefault();
        }
        else if (message.Video != null)
        {
            content.Type = "Video";
            content.Caption = message.Caption != null ? message.Caption : "";
            content.FileId = message.Video.FileId;
            content.FileName = message.Video.FileName != null ? message.Video.FileName : "";
            content.FileSize = message.Video.FileSize.GetValueOrDefault();
        }
        else if (message.Document != null)
        {
            content.Type = "Document";
            content.Caption = message.Caption != null ? message.Caption : "";
            content.FileId = message.Document.FileId;
            content.FileName = message.Document.FileName != null ? message.Document.FileName : "";
            content.FileSize = message.Document.FileSize.GetValueOrDefault();
        }
        else if (message.Sticker != null)
        {
            content.Type = "Sticker";
            content.Text = message.Sticker.Emoji != null ? message.Sticker.Emoji : "";
            content.FileId = message.Sticker.FileId;
        }
        else if (message.Voice != null)
        {
            content.Type = "Voice";
            content.FileId = message.Voice.FileId;
            content.FileSize = message.Voice.FileSize.GetValueOrDefault();
        }
        else if (message.Audio != null)
        {
            content.Type = "Audio";
            content.Caption = message.Caption != null ? message.Caption : "";
            content.FileId = message.Audio.FileId;
            content.FileName = message.Audio.FileName != null ? message.Audio.FileName : "";
            content.FileSize = message.Audio.FileSize.GetValueOrDefault();
        }
        else if (message.VideoNote != null)
        {
            content.Type = "VideoNote";
            content.FileId = message.VideoNote.FileId;
            content.FileSize = message.VideoNote.FileSize.GetValueOrDefault();
        }
        else
        {
            content.Type = "Unknown";
        }

        result.Content = content;
    }

    private static string BuildFullName(string firstName, string lastName)
    {
        if (string.IsNullOrEmpty(lastName))
        {
            return firstName;
        }
        return $"{firstName} {lastName}".Trim();
    }

    private static string BuildChannelLink(Chat chat, int messageId)
    {
        if (!string.IsNullOrEmpty(chat.Username))
        {
            return $"https://t.me/{chat.Username}/{messageId}";
        }

        // Private channel - need to convert chat ID
        var privateChatId = Math.Abs(chat.Id) - 1000000000000;
        return $"https://t.me/c/{privateChatId}/{messageId}";
    }
}
