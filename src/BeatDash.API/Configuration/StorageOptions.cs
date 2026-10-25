namespace Shiron.BeatDash.API.Configuration;

public sealed class StorageOptions {
    public StorageProvider Provider { get; set; } = StorageProvider.RustFs;
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "admin";
    public string SecretKey { get; set; } = "admin_pw";
    public bool UseSsl { get; set; }
    public string BucketAssets { get; set; } = "beatdash-assets";
    public string BucketUserData { get; set; } = "beatdash-user-data";
    /// <summary>Root directory of the <see cref="StorageProvider.FileSystem"/> provider; relative paths resolve against the working directory.</summary>
    public string RootDirectory { get; set; } = "./storage";
}
