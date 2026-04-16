using Shiron.BeatDash.CLI.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(c => {
    c.SetApplicationName("bdcli");
    c.SetApplicationVersion("0.0.0");

    c.AddCommand<RecordCommand>("record");
    c.AddCommand<ReplayCommand>("replay");
    c.AddCommand<AnalyzeCommand>("analyze");
    c.AddCommand<UploadCommand>("upload");
    c.AddCommand<ParseMapCommand>("parse-map");
});
await app.RunAsync(args);
