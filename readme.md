# PetaJson

PetaJson is a simple but flexible JSON library implemented in a single C# file.  Features include:

* Standard JSON parsing and generation
* Supports strongly typed serialization through reflection or custom code
* Supports weakly typed serialization
* Supports standard C# collection classes - no JSON specific classes (ie: no "JArray", "JObject" etc...)
* Support for dynamic Expando (read) and anonymous types (write)
* Custom formatting and parsing of any type
* Support for serialization of abstract/virtual types
* Directly reads from TextReader and writes to TextWriter and any underlying stream
* Simple set of custom attributes to control serialization
* Optional non-strict parsing allows comments, non-quoted dictionary keys and trailing commas (great for config files)
* Optional pretty formatting
* No dependencies, one file - PetaJson.cs
* Works on .NET, Mono, Xamarin.Android, Xamarin.iOS.

# Usage

Here goes, a 5 minute whirl-wind tour of using PetaJson...

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

String into an existing instance:

	Json.ParseInto<Person>(jsonFromPersonExampleAbove, person);

From file into an existing instance:

	var person = new Person();
	Json.ParseFileInto<Person>("aboutme.json", person);


## Attributes

PetaJson provides two attributes for decorating objects for serialization - [Json] and [JsonExclude].

The [Json] attribute when applied to a class or struct marks all public properties and fields for serialization:

	[Json]
	class Person
	{
		public string Name;				// Serialized as "name"
		public string Address;			// Serialized as "address"
		public string AlsoSerialized;	// Serialized as "alsoSerialized"
		private string NotSerialized;
	}

When applied to one or more field or properties but not applied to the class itself, only the decorated members
will be serialized:

	class Person
	{
		[Json] public string Name;	// Serialized as "name":
		public string Address;		// Not serialized
	}

By default members are serialized using the name as the field or property with the first letter
lowercased.  To override the serialized name, include the name as a parameter to the [Json] attribute:

	class Person
	{
		[Json("PersonName")] public string Name; 	// Serialized as "PersonName"
	}

Use the [JsonExclude] attribute to exclude public fields or properties from serialization

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

Sometimes you'll want sub-objects to be serialized into an existing object instance.

eg: 

	class MyApp
	{
		public MyApp()
		{
			// Settings object has an owner reference that needs to be maintained
			// across serialization
			CurrentSettings = new Settings(this);
		}

		[Json(KeepInstance=true)]
		Settings CurrentSettings;
	}

In this example the existing CurrentSettings object will be serialized into. If KeepInstance
was set to false, PetaJson would instantiate a new Settings object, load it and then assign
it to the CurrentSettings property.


## Custom Formatting

Custom formatting can be used for any type.  Say we have the following type:

    struct Point
    {
        public int X;
        public int Y;
    }

and we want to serialize points as a comma separated string like this:

	{
		"TopLeft": "10,20",
		"BottomRight: "30,40",
	}

To do this, we need to register a formatter:

    // Register custom formatter
    Json.RegisterFormatter<Point>( (writer,point) => 
    {
        writer.WriteStringLiteral(string.Format("{0},{1}", point.X, point.Y));
    });

And a custom parser:

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

We can now format and parse Points:

	// Format a Point
	var json = Json.Format(new Point() { X= 10, Y=20 });		// "10,20"

	// Parse a Point
	var point = Json.Parse<Point>("\"10,20\"");

Note that in this example we're formatting the point to a string literal containing both
the X and Y components of the Point.  The reader and writer objects passed to the callbacks
however have methods for reading and writing any arbitrary json format - this example just 
happens to use a string literal.

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
		{ "kind": "Rectangle", /* other rectangle properties omitted */ },
		{ "kind": "Shape", /* other ellipse properties omitted */ },
		// etc...
	]

In otherwords a value in the Json dictionary for each object determines the type of object that 
needs to be instantiated for each element.

We can write out the shape kind by implementing the IJsonWriting interface which gets called
before the other properties of the object are written:

    abstract class Shape : IJsonWriting
    {
        // Override OnJsonWriting to write out the derived class type
        void IJsonWriting.OnJsonWriting(IJsonWriter w)
        {
            w.WriteKey("kind");
            w.WriteStringLiteral(GetType().Name);
        }
    }

For parsing, we need to register a callback function that creates the correct instances:

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

Note that the field used to hold the type (ie: "kind") does not need to be the first field in the
 in the dictionary being parsed. After instantiating the object, the input stream is re-wound to the
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


For example, it's often necessary to wire up ownership references on loaded subobjects:

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

Note: although these methods could have been implemented using reflection rather than interfaces,
the use of interfaces is more discoverable through Intellisense/Autocomplete.

## Options

PetaJson has a couple of formatting/parsing options. These can be set as global defaults:

	Json.WriteWhitespaceDefault = true;		// Pretty formatting
	Json.StrictParserDefault = true;		// Enable strict parsing

or, provided on a case by case basis:

	Json.Format(person, JsonOption.DontWriteWhitespace);		// Force pretty formatting off
	Json.Format(person, JsonOption.WriteWhitespace);			// Force pretty formatting on
	Json.Parse<object>(jsonData, JsonOption.StrictParser);		// Force strict parsing
	Json.Parse<object>(jsonData, JsonOption.NonStrictParser);	// Disable strict parsing

Non-strict mode relaxes the parser to allow:

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


## IJsonReader and IJsonWriter

These interfaces only need to be used when writing custom formatters and parsers.  They are the low
level interfaces used to read and write the Json stream.

The IJsonReader interface reads from the Json input stream.  

    public interface IJsonReader
    {
        object ReadLiteral(Func<object, object> converter);
        void ReadDictionary(Action<string> callback);
        void ReadArray(Action callback);
        object Parse(Type type);
        T Parse<T>();
    }

*ReadLiteral* - reads a single literal value from the input stream.  Throws an exception if
the next token isn't a literal value.  You should provide a callback that converts the raw
literal to the required value, which will then be returned as the return value from ReadLiteral.

Wherever possible, conversion should be done in the callback to ensure that errors in the conversion
report the error location just before the bad literal, instead of after it.


*ReadDictionary* - reads a Json dictionary, calling the callback for each key encountered.  The
callback routine should read the key's value using the IJsonReader interface.  If nothing is read
by the callback, PetaJson will skip the value and move onto the next key.

*ReadArray* - reads a Json array, calling the callback at each element position. The callback 
routine must read each value from the IJsonReader before returning.

*Parse* - parses a typed value from the input stream.

The IJsonWriter interface writes to the Json output stream:

    public interface IJsonWriter
    {
        void WriteStringLiteral(string str);
        void WriteRaw(string str);
        void WriteArray(Action callback);
        void WriteDictionary(Action callback);
        void WriteValue(object value);
        void WriteElement();
        void WriteKey(string key);
    }

*WriteStringLiteral* - writes a string literal to the output stream, including the surrounding quotes and
 escaping the content as required.
*WriteRaw* - writes directly to the output stream.  Use for comments, or self genarated Json data.
*WriteArray* - writes an array to the output stream.  The callback should write each element.
*WriteDictionary* - writes a dictionary to the output stream.  The callback should write each element.
*WriteValue* - formats and writes any object value.
*WriteElement* - call from the callback of WriteArray to indicate that the next element is about to be 
written.  Causes PetaJson to write separating commas and whitespace.
*WriteKey* - call from the callback of WriteDictionary to write the key part of the next element.  Writes
whitespace, separating commas, the key and it's quotes, the colon.

eg: to write a dictionary:

	writer.WriteDictionary(() =>
	{
		writer.WriteKey("apples");
		writer.WriteValue("red");
		writer.WriteKey("bananas");
		writer.WriteValue("yellow");
	});

eg: to write an array:

	writer.WriteArray(()=>
	{
		for (int i=0; i<10; i++)
		{
			writer.WriteElement();
			writer.WriteValue(i);
		}
	});