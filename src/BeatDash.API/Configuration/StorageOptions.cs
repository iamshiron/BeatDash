namespace Shiron.BeatDash.API.Configuration;

public sealed class StorageOptions {
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "admin";
    public string SecretKey { get; set; } = "admin_pw";
    public bool UseSsl { get; set; }
    public string BucketAssets { get; set; } = "beatdash-assets";
    public string BucketUserData { get; set; } = "beatdash-user-data";
}
