var bt = require('./BuildTools/buildTools.js')

// Crack /debug /release and /all options
var debug = bt.options.switches.debug || bt.options.switches.all;
var release = bt.options.switches.release || bt.options.switches.all;
if (!debug && !release)
	release = true;

// Clock version
bt.version();
bt.clock_version();

// Don't bother cleaning if we're not doing a release build
if (release)
{
	bt.options.clean = true;
	bt.clean(".\\Build");
}

// Debug build
if (debug)
{
	bt.dnbuild("Debug");
	bt.nupack("PetaJson.Debug.nuspec", ".\\Build");
}

// Release build
if (release)
{
	bt.dnbuild("Release");

	bt.signfile([
        "Build\\Release\\PetaJson\\netcoreapp2.0\\PetaJson.dll",
        "Build\\Release\\PetaJson\\net46\\PetaJson.dll",

    ], "PetaJson JSON Serialization Library");

	bt.nupack("PetaJson.nuspec", ".\\Build");
}


bt.copy("build\\*.nupkg", "\\\\cool\\public\\ToptenNuget")
