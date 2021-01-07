var bt = require('./buildtools/buildTools.js')

if (bt.options.official)
{
    // Check everything committed
    bt.git_check();

    // Clock version
    bt.clock_version();

    // Clean build directory
    bt.run("rm -rf ./Build");
}

// Build
bt.run("dotnet build Topten.JsonKit\\Topten.JsonKit.csproj -c Release")
bt.run("dotnet build Topten.JsonKit\\Topten.JsonKit.Lite.csproj -c Release")

if (bt.options.official)
{
    // Run tests
    bt.run("dotnet test Topten.JsonKit.Test -c Release");

    // Tag and commit
    bt.git_tag();

    // Push nuget package
    bt.run(`dotnet nuget push`,
           `./Build/Release/*.${bt.options.version.build}.nupkg`,
           `--source "Topten GitHub"`);
}
