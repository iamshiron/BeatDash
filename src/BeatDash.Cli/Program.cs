using Shiron.BeatDash.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config => {
    config.SetApplicationName("beatdash");

    config.AddBranch("beatmap", beatmap => {
        beatmap.SetDescription("Beat Saber map parsing / debugging commands");

        beatmap.AddCommand<ParseMapCommand>("parse")
            .WithDescription("Parse a Beat Saber map folder on disk and print its unified model");

        beatmap.AddCommand<IndexMapsCommand>("index")
            .WithDescription("Parse every map folder under a directory and emit a per-map JSON summary");

        beatmap.AddCommand<CalibrateCommand>("calibrate")
            .WithDescription("Fit metric Scale factors over a corpus and print the suggested config (applies nothing)");

        beatmap.AddCommand<PushMapsCommand>("push")
            .WithDescription("Bulk-import a CustomLevels directory into the server via the admin import endpoint");
    });
});

return app.Run(args);
