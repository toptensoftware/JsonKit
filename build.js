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
    bt.dntest("Release", "PetaJson.Test");

    // Force clean
    bt.options.clean = true;
    bt.clean("./Build");
}

// Build
bt.dnbuild("Release", "PetaJson");

// Build NuGet Package?
if (bt.options.official || bt.options.nuget)
{
	bt.signfile([
        "Build\\Release\\PetaJson\\netcoreapp2.0\\PetaJson.dll",
        "Build\\Release\\PetaJson\\net46\\PetaJson.dll",
    ], "PetaJson JSON Serialization Library");

    bt.nupack("PetaJson.nuspec", "./Build");
}

if (bt.options.official)
{
    // Tag and commit
    bt.git_tag();

    // Push nuget package
    bt.nupush(`./build/*.${bt.options.version.build}.nupkg`, "http://nuget.toptensoftware.com/v3/index.json");
}
