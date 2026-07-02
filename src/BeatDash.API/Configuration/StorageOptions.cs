namespace Shiron.BeatDash.API.Configuration;

public sealed class StorageOptions {
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public bool UseSsl { get; set; }
    public string BucketAssets { get; set; } = "beatdash-assets";
    public string BucketUserData { get; set; } = "beatdash-user-data";
}
