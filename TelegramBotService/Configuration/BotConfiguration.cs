namespace TelegramBotService.Configuration;

// Main configuration class for the bot
public class BotConfiguration
{
    public StorageConfig Storage { get; set; } = new();
    public MediaConversionConfig MediaConversion { get; set; } = new();
    public MemoryConfig Memory { get; set; } = new();
}

// File storage configuration
public class StorageConfig
{
    public string BasePath { get; set; } = "./storage";
    public string DatabasePath { get; set; } = "./bot.db";
    public bool OrganizeByDate { get; set; } = true;
    public bool OrganizeByType { get; set; } = true;
    public long MaxFileSizeMB { get; set; } = 50;
}

// Media conversion configuration
public class MediaConversionConfig
{
    public bool Enabled { get; set; } = true;
    public string WhisperPath { get; set; } = "whisper";
    public string WhisperModel { get; set; } = "base";
    public string TesseractPath { get; set; } = "tesseract";
    public string TesseractLanguages { get; set; } = "eng+chi_sim+jpn";
    public string FfmpegPath { get; set; } = "ffmpeg";
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxConcurrent { get; set; } = 2;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
}

// Kernel Memory / Vector storage configuration
public class MemoryConfig
{
    public bool Enabled { get; set; } = false;
    public string Backend { get; set; } = "sqlite";  // sqlite, qdrant, postgres
    public string EmbeddingProvider { get; set; } = "siliconflow";  // siliconflow, openai
    public string SqliteStorePath { get; set; } = "./memory.db";
    public string QdrantEndpoint { get; set; } = "http://localhost:6333";
    // OpenAI / OpenAI-compatible embedding settings
    public string EmbeddingEndpoint { get; set; } = "https://api.siliconflow.cn/v1";
    public string EmbeddingApiKey { get; set; } = "";
    public string EmbeddingModel { get; set; } = "BAAI/bge-m3";
    public int EmbeddingMaxTokens { get; set; } = 8192;
}

