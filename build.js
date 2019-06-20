var bt = require('./BuildTools/buildTools.js')

bt.options.companyName = "Topten Software";
bt.options.codeSignCertificate = "C:\\Users\\brad\\dropbox\\topten\\ToptenCodeSigningCertificate.pfx";
bt.options.codeSignPasswordFile = "C:\\Users\\brad\\dropbox\\topten\\codesign_password.txt";
bt.symStorePath = "\\\\cool\\public\\ToptenSymbols";


// Load version info
bt.version();

if (bt.options.official)
{
    // Check everything committed
    bt.git_check();

    // Clock version
    bt.clock_version();

    // Run Tests
    bt.dntest("Release", "Topten.JsonKit.Test");

    // Force clean
    bt.options.clean = true;
    bt.clean("./Build");
}

// Build
bt.dnbuild("Release", "JsonKit");

// Build NuGet Package?
if (bt.options.official || bt.options.nuget)
{
	bt.signfile([
        "Build\\Release\\Topten.JsonKit\\netcoreapp2.0\\Topten.JsonKit.dll",
        "Build\\Release\\Topten.JsonKit\\net46\\Topten.JsonKit.dll",
    ], "JsonKit JSON Serialization Library");

    bt.nupack("Topten.JsonKit.nuspec", "./Build");
}

if (bt.options.official)
{
    // Tag and commit
    bt.git_tag();

    // Push nuget package
    bt.nupush(`./build/*.${bt.options.version.build}.nupkg`, "http://nuget.toptensoftware.com/v3/index.json");
}
