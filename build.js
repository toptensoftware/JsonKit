var bt = require('./buildtools/buildTools.js')

// Load version info
bt.version();

if (bt.options.official)
{
    // Check everything committed
    bt.git_check();

    // Clock version
    bt.clock_version();

    // Clean build directory
    bt.cli("rm -rf ./Build");
}

// Build
bt.cli("dotnet build Topten.JsonKit -c Release")

if (bt.options.official)
{
    bt.cli("dotnet test Topten.JsonKit.Test -c Release");

    // Tag and commit
    bt.git_tag();

    // Push nuget package
    bt.cli(`dotnet nuget push`,
           `./Build/Release/Topten.JsonKit/*.${bt.options.version.build}.nupkg`,
           `--source "Topten GitHub"`);
}
