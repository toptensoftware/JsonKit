# PetaJson

PetaJson is a simple but flexible JSON library implemented in a single C# file.  Features include:

* Standard JSON format parsing and generation
* Supports strongly typed serialization through reflection, or custom code
* Supports weakly typed serialization
* Supports standard C# collection classes - no JSON specific classes (ie: no "JArray", "JObject" etc...)
* Support for dynamic Expando (read) and anonymous types (write)
* Custom formatting and parsing of any type
* Support for serialization of abstract/virtual types
* Directly reads from TextReader and writes to TextWriter and any underlying stream
* Simple set of custom attributes to control serialization of types
* Optional non-strict parsing allows comments, non-quoted dictionary keys and trailing commas (great for config files)
* Optional pretty formatting
* No dependencies, one file - PetaJson.cs
* Works on .NET, Mono, Xamarin.Android, Xamarin.iOS.

# Usage

## Setup

1. Add PetaJson.cs to your project
2. Optionally add "using PetaJson;" clauses as required
3. That's it

## Generating JSON

To a string:

	var o = new [] { 1, 2, 3 };
	var json = Json.Format(o);

or, write to a file

	Json.WriteFile("MyData.json", o);

using objects

	class Person
	{
		string Name;
		string Address;
	};

	var p = new Person() { Name = "Joe Sixpack", Address = "Home" };
	var json = Json.Format(p);

would yield:

	{
		"name": "Joe Sixpack",
		"address": "Home"
	}


## Parsing JSON

From a string:

	int o = Json.Parse<int>("23");

From string to a dynamic:

	dynamic o = Json.Parse<object>("{\"apples\":\"red\", \"bananas\":\"yellow\" }");
	string appleColor = o.apples;
	string bananaColor = o.bananas;

Weakly typed dictionary:

	var dict = Json.Parse<Dictionary<string, object>>("{\"apples\":\"red\", \"bananas\":\"yellow\" }");

Or an array:

	int[] array = Json.Parse<int[]>("[1,2,3]");

Strongly typed object:

	Person person = Json.Parse<Person>(jsonFromPersonExampleAbove);
	Console.WriteLine(person.Name);
	Console.WriteLine(person.Address);

From a file:

	var person = Json.ParseFile<Person>("aboutme.json");

Into an existing instance:

	var person = new Person();
	Json.ParseFileInto<Person>("aboutme.json", person);

String into existing instance:

	Json.ParseInto<Person>(jsonFromPersonExampleAbove, person);


## Attributes

PetaJson provides two attributes for decorating objects for serialization - [Json] and [JsonExclude].

The Json attribute when applied to a class or struct marks all public properties and fields for serialization:

	[Json]
	class Person
	{
		public string Name;				// Serialized as "name"
		public string Address;			// Serialized as "address"
		public string alsoSerialized;	// Serialized as "alsoSerialized"
		private string NotSerialized;
	}

When applied to one or more field/properties but not applied to the class itself, only the decorated members
will be serialized:

	class Person
	{
		[Json] public string Name;	// Serialized as "name":
		public string Address;		// Not serialized
	}

By default, members are serialized using the same name as the field or properties, but with the first letter
lowercased.  To override the serialized name, include the name as a parameter to the Json attribute:

	class Person
	{
		[Json("PersonName")] public string Name; 	// Serialized as "PersonName"
	}

Use the JsonExclude attribute to exclude public fields/properties from serialization

	[Json]
	class Person
	{
		public string Name;		// Serialized as "name"
		public string Address;	// Serialized as "address"

		[JsonExclude]			// Not serialized
		public int Age
		{
			get { return calculateAge(); }
		}
	}

Sometimes you'll want sub-objects to be serialized into the existing object instance.

eg: 

	class MyApp
	{
		public MyApp()
		{
			// Settings object has an owner pointer back to this so during
			// serialization we don't want to create a new instance of the settings
			// object.
			CurrentSettings = new Settings(this);
		}

		[Json(KeepInstance=true)]
		Settings CurrentSettings;
	}

In this example the existing CurrentSettings object will be instantiated into. If KeepInstance
was set to false, PetaJson would instantiate a new Settings object, load it and then assign
it to the CurrentSettings property.


## Custom Formatting

Custom formatting can be used for any type.  Say we have the following type:

    struct Point
    {
        public int X;
        public int Y;
    }

We can serialize these as a string in the format "x,y" by registering a formatter

    // Register custom formatter
    Json.RegisterFormatter<Point>( (writer,point) => 
    {
        writer.WriteStringLiteral(string.Format("{0},{1}", point.X, point.Y));
    });

We also need a custom parser:

    Json.RegisterParser<Point>( literal => {

        var parts = ((string)literal).Split(',');
        if (parts.Length!=2)
            throw new InvalidDataException("Badly formatted point");

        return new Point()
        {
            X = int.Parse(parts[0], CultureInfo.InvariantCulture),
            Y = int.Parse(parts[0], CultureInfo.InvariantCulture),
        };

    });

We can now format and parse Point structs:

	// Format a Point
	var json = Json.Format(new Point() { X= 10, Y=20 });		// "10,20"

	// Parse a Point
	var point = Json.Parse<Point>("\"10,20\"");

Note that in this example we're formatting the point to a string literal containing both
the X and Y components of the Point.  The reader and writer objects passed to the callbacks
however have methods for reading and writing any arbitrary json format - I just happened to
use a single string literal for this example.

## Custom factories

Suppose we have a class heirarchy something like this:

    abstract class Shape
    {
    	// Omitted
    }

    class Rectangle : Shape
    {
    	// Omitted
    }

    class Ellipse : Shape
    {
    	// Omitted
    }

and we'd like to serialize a list of Shapes to Json like this:

	[
		{ "kind": "Rectangle", /* omitted */ },
		{ "kind": "Shape", /* omitted */ },
		// etc...
	]

In otherwords a key value in the dictionary for each object determines the type of object that 
needs to be instantiated for each element.

We can do this by firstly writing the element kind when saving using the IJsonWriting interface

    abstract class Shape : IJsonWriting
    {
        // Override OnJsonWriting to write out the derived class type
        void IJsonWriting.OnJsonWriting(IJsonWriter w)
        {
            w.WriteKey("kind");
            w.WriteStringLiteral(GetType().Name);
        }
    }

For parsing, we need to register a callback function that creates the correct instances of Shape:

    // Register a type factory that can instantiate Shape objects
    Json.RegisterTypeFactory(typeof(Shape), (reader, key) =>
    {
        // This method will be called back for each key in the json dictionary
        // until an object instance is returned

        // We saved the object type in a key called "kind", look for it
        if (key != "kind")
            return null;

        // Read the next literal (which better be a string) and instantiate the object
        return reader.ReadLiteral(literal =>
        {
            switch ((string)literal)
            {
                case "Rectangle": return new Rectangle();
                case "Ellipse": return new Ellipse();
                default:
                    throw new InvalidDataException(string.Format("Unknown shape kind: '{0}'", literal));
            }
        });
    });

When attempting to deserialize Shape objects, the registered callback will be called with each 
key in the dictionary until it returns an object instance.  In this case we're looking for a key
named "kind" and we use it's value to create a new Rectangle or Ellipse instance.

Note that the field used to hold the type (aka "kind") does not need to be the first field in the
 in the dictionary being parsed. After instantiating the object, the input stream is rewound to the
 start of the dictionary and then re-parsed directly into the instantiated object.  Note too that
 the underlying stream doesn't need to support seeking - the rewind mechanism is implemented in 
 PetaJson.

## Serialization Events

An object can get notifications of various events during the serialization/deserialization process
by implementing one or more of the following interfaces:

    // Called before loading via reflection
    public interface IJsonLoading
    {
        void OnJsonLoading(IJsonReader r);
    }

    // Called after loading via reflection
    public interface IJsonLoaded
    {
        void OnJsonLoaded(IJsonReader r);
    }

    // Called for each field while loading from reflection
    // Return true if handled
    public interface IJsonLoadField
    {
        bool OnJsonField(IJsonReader r, string key);
    }

    // Called when about to write using reflection
    public interface IJsonWriting
    {
        void OnJsonWriting(IJsonWriter w);
    }

    // Called after written using reflection
    public interface IJsonWritten
    {
        void OnJsonWritten(IJsonWriter w);
    }


For example, it's often necessary to wire up ownership chains on loaded subobjects:

	class Drawing : IJsonLoaded
	{
		[Json]
		public List<Shape> Shapes;

		void IJsonLoaded.OnJsonLoaded()
		{
			// Shapes have been loaded, set back reference to self
			foreach (var s in Shapes)
			{
				s.Owner = this;
			}
		}
	}

## Options

PetaJson has a couple of options that can be set as global defaults:

	Json.WriteWhitespaceDefault = true;		// Pretty formatting
	Json.StrictParserDefault = true;		// Enable strict parsing

or, overridden on a case by case basis:

	Json.Format(person, JsonOption.DontWriteWhitespace);		// Force pretty formatting off
	Json.Format(person, JsonOption.WriteWhitespace);			// Force pretty formatting on
	Json.Parse<object>(jsonData, JsonOption.StrictParser);		// Force strict parsing
	Json.Parse<object>(jsonData, JsonOption.NonStrictParser);	// Disable strict parsing

Non-strict parsing allows the following:

* Inline C /* */ or C++ // style comments
* Trailing commas in arrays and dictionaries
* Non-quoted dictionary keys

eg: the non-strict parser will allow this:

	{
		/* This is a C-style comment */
		"quotedKey": "allowed",
		nonQuotedKey: "also allowed",
		"arrayWithTrailingComma": [1,2,3,],	
		"trailing commas": "allowed ->",	// <- see the comma, not normally allowed
	}

