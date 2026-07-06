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
    });
});

return app.Run(args);
