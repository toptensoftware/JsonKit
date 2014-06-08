﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Collections;
#if SIMPLEJSON_DYNAMIC
using System.Dynamic;
#endif

namespace PetaJson
{
    [Flags]
    public enum JsonOptions
    {
        None = 0,
        WriteWhitespace  = 0x00000001,
        DontWriteWhitespace = 0x00000002,
        StrictParser = 0x00000004,
        NonStrictParser = 0x00000008,
    }

    public static class Json
    {
        static Json()
        {
            WriteWhitespaceDefault = true;
            StrictParserDefault = false;
        }

        public static bool WriteWhitespaceDefault
        {
            get;
            set;
        }

        public static bool StrictParserDefault
        {
            get;
            set;
        }

        static JsonOptions ResolveOptions(JsonOptions options)
        {
            JsonOptions resolved = JsonOptions.None;

            if ((options & (JsonOptions.WriteWhitespace|JsonOptions.DontWriteWhitespace))!=0)
                resolved |= options & (JsonOptions.WriteWhitespace | JsonOptions.DontWriteWhitespace);
            else
                resolved |= WriteWhitespaceDefault ? JsonOptions.WriteWhitespace : JsonOptions.DontWriteWhitespace;

            if ((options & (JsonOptions.StrictParser | JsonOptions.NonStrictParser)) != 0)
                resolved |= options & (JsonOptions.StrictParser | JsonOptions.NonStrictParser);
            else
                resolved |= StrictParserDefault ? JsonOptions.StrictParser : JsonOptions.NonStrictParser;

            return resolved;
        }

        // Write an object to a text writer
        public static void Write(TextWriter w, object o, JsonOptions options = JsonOptions.None)
        {
            var writer = new Internal.Writer(w, ResolveOptions(options));
            writer.WriteValue(o);
        }

        // Write an object to a file
        public static void WriteFile(string filename, object o, JsonOptions options = JsonOptions.None)
        {
            using (var w = new StreamWriter(filename))
            {
                Write(w, o, options);
            }
        }

        // Format an object as a json string
        public static string Format(object o, JsonOptions options = JsonOptions.None)
        {
            var sw = new StringWriter();
            var writer = new Internal.Writer(sw, ResolveOptions(options));
            writer.WriteValue(o);
            return sw.ToString();
        }

        // Parse an object of specified type from a text reader
        public static object Parse(TextReader r, Type type, JsonOptions options = JsonOptions.None)
        {
            Internal.Reader reader = null;
            try
            {
                reader = new Internal.Reader(r, ResolveOptions(options));
                var retv = reader.Parse(type);
                reader.CheckEOF();
                return retv;
            }
            catch (Exception x)
            {
                throw new JsonParseException(x, reader==null ? new JsonLineOffset() : reader.CurrentTokenPosition);
            }
        }

        // Parse an object of specified type from a text reader
        public static T Parse<T>(TextReader r, JsonOptions options = JsonOptions.None)
        {
            return (T)Parse(r, typeof(T), options);
        }

        // Parse from text reader into an already instantied object
        public static void ParseInto(TextReader r, Object into, JsonOptions options = JsonOptions.None)
        {
            Internal.Reader reader = null;
            try
            {
                reader = new Internal.Reader(r, ResolveOptions(options));
                reader.ParseInto(into);
                reader.CheckEOF();
            }
            catch (Exception x)
            {
                throw new JsonParseException(x, reader==null ? new JsonLineOffset() : reader.CurrentTokenPosition);
            }
        }

        // Parse an object of specified type from a text reader
        public static object ParseFile(string filename, Type type, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                return Parse(r, type, options);
            }
        }

        // Parse an object of specified type from a text reader
        public static T ParseFile<T>(string filename, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                return Parse<T>(r, options);
            }
        }

        // Parse from text reader into an already instantied object
        public static void ParseFileInto(string filename, Object into, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                ParseInto(r, into, options);
            }
        }

        // Parse an object from a string
        public static object Parse(string data, Type type, JsonOptions options = JsonOptions.None)
        {
            return Parse(new StringReader(data), type, options);
        }

        // Parse an object from a string
        public static T Parse<T>(string data, JsonOptions options = JsonOptions.None)
        {
            return (T)Parse<T>(new StringReader(data), options);
        }

        // Parse from string into an already instantiated object
        public static void ParseInto(string data, Object into, JsonOptions options = JsonOptions.None)
        {
            ParseInto(new StringReader(data), into, options);
        }

        public static T Clone<T>(T source)
        {
            return (T)Clone((object)source);
        }

        public static object Clone(object source)
        {
            if (source == null)
                return null;

            return Parse(Format(source), source.GetType());
        }

        public static void CloneInto<T>(T dest, T source)
        {
            ParseInto(Format(source), dest);
        }


        // Register a callback that can format a value of a particular type into json
        public static void RegisterFormatter(Type type, Action<IJsonWriter, object> formatter)
        {
            Internal.Writer._typeWriters[type] = formatter;
        }

        // Typed version of above
        public static void RegisterFormatter<T>(Action<IJsonWriter, T> formatter)
        {
            RegisterFormatter(typeof(T), (w, o) => formatter(w, (T)o));
        }

        // Register a parser for a specified type
        public static void RegisterParser(Type type, Func<IJsonReader, Type, object> parser)
        {
            Internal.Reader._typeReaders[type] = parser;
        }

        // Register a typed parser
        public static void RegisterParser<T>(Func<IJsonReader, Type, T> parser)
        {
            RegisterParser(typeof(T), (r, t) => parser(r, t));
        }

        // Simpler version for simple types
        public static void RegisterParser(Type type, Func<object, object> parser)
        {
            RegisterParser(type, (r, t) => r.ReadLiteral(parser));
        }

        // Simpler and typesafe parser for simple types
        public static void RegisterParser<T>(Func<object, T> parser)
        {
            RegisterParser(typeof(T), literal => parser(literal));
        }

        // Register a factory for instantiating objects (typically abstract classes)
        // Callback will be invoked for each key in the dictionary until it returns an object
        // instance and which point it will switch to serialization using reflection
        public static void RegisterTypeFactory(Type type, Func<IJsonReader, string, object> factory)
        {
            Internal.Reader._typeFactories[type] = factory;
        }
    }

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

    // Passed to registered parsers
    public interface IJsonReader
    {
        object ReadLiteral(Func<object, object> converter);
        object Parse(Type type);
        T Parse<T>();
        void ReadDictionary(Action<string> callback);
        void ReadArray(Action callback);
    }

    // Passed to registered formatters
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

    public class JsonParseException : Exception
    {
        public JsonParseException(Exception inner, JsonLineOffset position) : 
            base(string.Format("Json parse error at {0} - {1}", position, inner.Message), inner)
        {
        }
    }

    public struct JsonLineOffset
    {
        public int Line;
        public int Offset;
        public override string ToString()
        {
            return string.Format("line {0}, position {1}", Line + 1, Offset + 1);
        }
    }

    // Used to decorate fields and properties that should be serialized
    //
    // - [Json] on class or struct causes all public fields and properties to be serialized
    // - [Json] on a public or non-public field or property causes that member to be serialized
    // - [JsonExclude] on a field or property causes that field to be not serialized
    // - A class or struct with no [Json] attribute has all public fields/properties serialized
    // - A class or struct with no [Json] attribute but a [Json] attribute on one or more members only serializes those members
    //
    // Use [Json("keyname")] to explicitly specify the key to be used 
    // [Json] without the keyname will be serialized using the name of the member with the first letter lowercased.
    //
    // [Json(KeepInstance=true)] causes container/subobject types to be serialized into the existing member instance (if not null)
    //
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
    public class JsonAttribute : Attribute
    {
        public JsonAttribute()
        {
            _key = null;
        }
        public JsonAttribute(string key)
        {
            _key = key;
        }

        string _key;
        public string Key
        {
            get { return _key; }
        }

        public bool KeepInstance
        {
            get;
            set;
        }
    }

    // See comments above
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class JsonExcludeAttribute : Attribute
    {
        public JsonExcludeAttribute()
        {
        }
    }

    namespace Internal
    {
        public enum Token
        {
            EOF,
            Identifier,
            Literal,
            OpenBrace,
            CloseBrace,
            OpenSquare,
            CloseSquare,
            Equal,
            Colon,
            SemiColon,
            Comma,
        }

        public class Reader : IJsonReader
        {
            static Reader()
            {
                Func<IJsonReader, Type, object> simpleConverter = (reader, type) =>
                {
                    return reader.ReadLiteral(literal => Convert.ChangeType(literal, type, CultureInfo.InvariantCulture));
                };

                _typeReaders.Add(typeof(string), simpleConverter);
                _typeReaders.Add(typeof(char), simpleConverter);
                _typeReaders.Add(typeof(bool), simpleConverter);
                _typeReaders.Add(typeof(byte), simpleConverter);
                _typeReaders.Add(typeof(sbyte), simpleConverter);
                _typeReaders.Add(typeof(short), simpleConverter);
                _typeReaders.Add(typeof(ushort), simpleConverter);
                _typeReaders.Add(typeof(int), simpleConverter);
                _typeReaders.Add(typeof(uint), simpleConverter);
                _typeReaders.Add(typeof(long), simpleConverter);
                _typeReaders.Add(typeof(ulong), simpleConverter);
                _typeReaders.Add(typeof(decimal), simpleConverter);
                _typeReaders.Add(typeof(float), simpleConverter);
                _typeReaders.Add(typeof(double), simpleConverter);

                _typeReaders.Add(typeof(DateTime), (reader, type) =>
                {
                    return reader.ReadLiteral(literal => Utils.FromUnixMilliseconds((long)Convert.ChangeType(literal, typeof(long), CultureInfo.InvariantCulture)));
                });

                _typeReaders.Add(typeof(byte[]), (reader, type) =>
                {
                    return reader.ReadLiteral(literal => Convert.FromBase64String((string)Convert.ChangeType(literal, typeof(string), CultureInfo.InvariantCulture)));
                });
            }

            public Reader(TextReader r, JsonOptions options)
            {
                _tokenizer = new Tokenizer(r, options);
                _options = options;
            }

            Tokenizer _tokenizer;
            JsonOptions _options;

            public JsonLineOffset CurrentTokenPosition
            {
                get { return _tokenizer.CurrentTokenPosition; }
            }

            // ReadLiteral is implemented with a converter callback so that any
            // errors on converting to the target type are thrown before the tokenizer
            // is advanced to the next token.  This ensures error location is reported 
            // at the start of the literal, not the following token.
            public object ReadLiteral(Func<object, object> converter)
            {
                _tokenizer.Check(Token.Literal);
                var retv = converter(_tokenizer.Literal);
                _tokenizer.NextToken();
                return retv;
            }

            public void CheckEOF()
            {
                _tokenizer.Check(Token.EOF);
            }

            public object Parse(Type type)
            {
                // Null?
                if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.Literal == null)
                {
                    _tokenizer.NextToken();
                    return null;
                }

                // Handle nullable types
                var typeUnderlying = Nullable.GetUnderlyingType(type);
                if (typeUnderlying != null)
                    type = typeUnderlying;

                // See if we have a reader
                Func<IJsonReader, Type, object> typeReader;
                if (Reader._typeReaders.TryGetValue(type, out typeReader))
                {
                    return typeReader(this, type);
                }

                // Enumerated type?
                if (type.IsEnum)
                {
                    return ReadLiteral(literal => Enum.Parse(type, (string)literal));
                }
                
                // See if we have factory
                Func<IJsonReader, string, object> typeFactory;
                if (Reader._typeFactories.TryGetValue(type, out typeFactory))
                {
                    // Try first without passing dictionary keys
                    object into = typeFactory(this, null);
                    if (into == null)
                    {
                        // This is a awkward situation.  The factory requires a value from the dictionary
                        // in order to create the target object (typically an abstract class with the class
                        // kind recorded in the Json).  Since there's no guarantee of order in a json dictionary
                        // we can't assume the required key is first.
                        // So, create a bookmark on the tokenizer, read keys until the factory returns an
                        // object instance and then rewind the tokenizer and continue

                        // Create a bookmark so we can rewind
                        _tokenizer.CreateBookmark();

                        // Skip the opening brace
                        _tokenizer.Skip(Token.OpenBrace);

                        // First pass to work out type
                        ReadDictionaryKeys(key =>
                        {
                            // Try to instantiate the object
                            into = typeFactory(this, key);
                            return into == null;
                        });

                        // Move back to start of the dictionary
                        _tokenizer.RewindToBookmark();

                        // Quit if still didn't get an object from the factory
                        if (into == null)
                            throw new InvalidOperationException("Factory didn't create object instance (probably due to a missing key in the Json)");
                    }

                    // Second pass
                    ParseInto(into);

                    // Done
                    return into;
                }

                // Is it a type we can parse into?
                if (CanParseInto(type))
                {
                    var into = Activator.CreateInstance(type);
                    ParseInto(into);
                    return into;
                }

                // Array?
                if (type.IsArray && type.GetArrayRank() == 1)
                {
                    // First parse as a List<>
                    var listType = typeof(List<>).MakeGenericType(type.GetElementType());
                    var list = Activator.CreateInstance(listType);
                    ParseInto(list);

                    return listType.GetMethod("ToArray").Invoke(list, null);
                }

                // Untyped dictionary?
                if (_tokenizer.CurrentToken == Token.OpenBrace && (type.IsAssignableFrom(typeof(Dictionary<string, object>))))
                {
#if SIMPLEJSON_DYNAMIC
                    var container = (new ExpandoObject()) as IDictionary<string, object>;
#else
                    var container = new Dictionary<string, object>();
#endif
                    ReadDictionary(key =>
                    {
                        container[key] = Parse(typeof(Object));
                    });

                    return container;
                }

                // Untyped list?
                if (_tokenizer.CurrentToken == Token.OpenSquare && (type.IsAssignableFrom(typeof(List<object>))))
                {
                    var container = new List<object>();
                    ReadArray(() =>
                    {
                        container.Add(Parse(typeof(Object)));
                    });
                    return container;
                }

                // Untyped literal?
                if (_tokenizer.CurrentToken == Token.Literal && type.IsAssignableFrom(_tokenizer.Literal.GetType()))
                {
                    var lit = _tokenizer.Literal;
                    _tokenizer.NextToken();
                    return lit;
                }

                throw new InvalidDataException(string.Format("syntax error - unexpected token {0}", _tokenizer.CurrentToken));
            }

            public static bool CanParseInto(Type type)
            {
                /* These two checks are redundant as they're covered by IDictionary/IList below
                if (typeof(IDictionary<,>).IsAssignableFrom(type))
                    return true;

                if (typeof(IList<>).IsAssignableFrom(type))
                    return true;
                */

                if (type.IsArray)
                    return false;

                if (typeof(IDictionary).IsAssignableFrom(type))
                    return true;

                if (typeof(IList).IsAssignableFrom(type))
                    return true;

                if (ReflectionInfo.GetReflectionInfo(type) != null)
                    return true;

                return false;
            }

            public void ParseInto(object into)
            {
                if (TryParseInto(into))
                    return;

                throw new InvalidOperationException(string.Format("Don't know how to load into '{0}'", into.GetType().FullName));
            }

            public Type FindGenericInterface(Type type, Type tItf)
            {
                foreach (var t in type.GetInterfaces())
                {
                    // Is this a generic list?
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == tItf)
                        return type;
                }

                return null;
            }

            public bool TryParseInto(object into)
            {
                if (into == null)
                    return false;

                var type = into.GetType();

                // Generic dictionary?
                var dictType = FindGenericInterface(type, typeof(IDictionary<,>));
                if (dictType!=null)
                {
                    // Get the key and value types
                    var typeKey = dictType.GetGenericArguments()[0];
                    var typeValue = dictType.GetGenericArguments()[1];

                    // Parse it
                    IDictionary dict = (IDictionary)into;
                    dict.Clear();
                    ReadDictionary(key =>
                    {
                        dict.Add(Convert.ChangeType(key, typeKey), Parse(typeValue));
                    });

                    return true;
                }

                // Generic list
                var listType = FindGenericInterface(type, typeof(IList<>));
                if (listType!=null)
                {
                    // Get element type
                    var typeElement = listType.GetGenericArguments()[0];

                    // Parse it
                    IList list = (IList)into;
                    list.Clear();
                    ReadArray(() =>
                    {
                        list.Add(Parse(typeElement));
                    });

                    return true;
                }

                // Untyped dictionary
                var objDict = into as IDictionary;
                if (objDict != null)
                {
                    objDict.Clear();
                    ReadDictionary(key =>
                    {
                        objDict[key] = Parse(typeof(Object));
                    });
                    return true;
                }

                // Untyped list
                var objList = into as IList;
                if (objList!=null)
                {
                    objList.Clear();
                    ReadArray(() =>
                    {
                        objList.Add(Parse(typeof(Object)));
                    });
                    return true;
                }

                // Use reflection?
                var ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri != null)
                {
                    ri.ParseInto(this, into);
                    return true;
                }

                return false;
            }

            public T Parse<T>()
            {
                return (T)Parse(typeof(T));
            }

            public void ReadDictionary(Action<string> callback)
            {
                _tokenizer.Skip(Token.OpenBrace);

                ReadDictionaryKeys(key => { callback(key); return true; });

                _tokenizer.Skip(Token.CloseBrace);
            }

            private void ReadDictionaryKeys(Func<string, bool> callback)
            {
                while (_tokenizer.CurrentToken != Token.CloseBrace)
                {
                    // Parse the key
                    string key = null;
                    if (_tokenizer.CurrentToken == Token.Identifier && (_options & JsonOptions.StrictParser)==0)
                    {
                        key = _tokenizer.String;
                    }
                    else if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.Literal is String)
                    {
                        key = (string)_tokenizer.Literal;
                    }
                    else
                    {
                        throw new InvalidDataException("syntax error, expected string literal or identifier");
                    }

                    _tokenizer.NextToken();
                    _tokenizer.Skip(Token.Colon);

                    _tokenizer.DidMove = false;

                    if (!callback(key))
                        return;

                    if (!_tokenizer.DidMove)
                    {
                        // The callback didn't handle the key, so skip the value
                        Parse(typeof(object));
                    }

                    if (_tokenizer.SkipIf(Token.Comma))
                    {
                        if ((_options & JsonOptions.StrictParser) != 0 && _tokenizer.CurrentToken == Token.CloseBrace)
                        {
                            throw new InvalidDataException("Trailing commas not allowed in strict mode");
                        }
                        continue;
                    }
                    break;
                }
            }

            public void ReadArray(Action callback)
            {
                _tokenizer.Skip(Token.OpenSquare);

                while (_tokenizer.CurrentToken != Token.CloseSquare)
                {
                    callback();

                    if (_tokenizer.SkipIf(Token.Comma))
                    {
                        if ((_options & JsonOptions.StrictParser)!=0 && _tokenizer.CurrentToken==Token.CloseSquare)
                        {
                            throw new InvalidDataException("Trailing commas not allowed in strict mode");
                        }
                        continue;
                    }
                    break;
                }

                _tokenizer.Skip(Token.CloseSquare);
            }

            public static Dictionary<Type, Func<IJsonReader, Type, object>> _typeReaders = new Dictionary<Type, Func<IJsonReader, Type, object>>();
            public static Dictionary<Type, Func<IJsonReader, string, object>> _typeFactories = new Dictionary<Type, Func<IJsonReader, string, object>>();
        }

        public class Writer : IJsonWriter
        {
            static Writer()
            {
                // Strings
                _typeWriters.Add(typeof(string), (w, o) => w.WriteStringLiteral((string)o));
                _typeWriters.Add(typeof(char), (w, o) => w.WriteStringLiteral(((char)o).ToString()));

                // Boolean
                _typeWriters.Add(typeof(bool), (w, o) => w.WriteRaw(((bool)o) ? "true" : "false"));

                // Integer
                Action<IJsonWriter, object> convertWriter = (w, o) => w.WriteRaw((string)Convert.ChangeType(o, typeof(string), System.Globalization.CultureInfo.InvariantCulture));
                _typeWriters.Add(typeof(int), convertWriter);
                _typeWriters.Add(typeof(uint), convertWriter);
                _typeWriters.Add(typeof(long), convertWriter);
                _typeWriters.Add(typeof(ulong), convertWriter);
                _typeWriters.Add(typeof(short), convertWriter);
                _typeWriters.Add(typeof(ushort), convertWriter);
                _typeWriters.Add(typeof(decimal), convertWriter);
                _typeWriters.Add(typeof(byte), convertWriter);
                _typeWriters.Add(typeof(sbyte), convertWriter);

                // Date
                _typeWriters.Add(typeof(DateTime), (w, o) => convertWriter(w, Utils.ToUnixMilliseconds((DateTime)o)));

                // Floating point
                _typeWriters.Add(typeof(float), (w, o) => w.WriteRaw(((float)o).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                _typeWriters.Add(typeof(double), (w, o) => w.WriteRaw(((double)o).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

                // Byte array
                _typeWriters.Add(typeof(byte[]), (w, o) =>
                {
                    w.WriteRaw("\"");
                    w.WriteRaw(Convert.ToBase64String((byte[])o));
                    w.WriteRaw("\"");
                });
            }

            public static Dictionary<Type, Action<IJsonWriter, object>> _typeWriters = new Dictionary<Type, Action<IJsonWriter, object>>();

            public Writer(TextWriter w, JsonOptions options)
            {
                _writer = w;
                _atStartOfLine = true;
                _needElementSeparator = false;
                _options = options;
            }

            TextWriter _writer;

            public int IndentLevel;
            bool _atStartOfLine;
            bool _needElementSeparator = false;
            JsonOptions _options;
            char _currentBlockKind = '\0';

            public void NextLine()
            {
                if (_atStartOfLine)
                    return;

                if ((_options & JsonOptions.WriteWhitespace)!=0)
                {
                    WriteRaw("\n");
                    WriteRaw(new string('\t', IndentLevel));
                }
                _atStartOfLine = true;
            }

            void NextElement()
            {
                if (_needElementSeparator)
                {
                    WriteRaw(",");
                    NextLine();
                }
                else
                {
                    NextLine();
                    IndentLevel++;
                    WriteRaw(_currentBlockKind.ToString());
                    NextLine();
                }

                _needElementSeparator = true;
            }

            public void WriteElement()
            {
                if (_currentBlockKind != '[')
                    throw new InvalidOperationException("Attempt to write array element when not in array block");
                NextElement();
            }

            public void WriteKey(string key)
            {
                if (_currentBlockKind != '{')
                    throw new InvalidOperationException("Attempt to write dictionary element when not in dictionary block");
                NextElement();
                WriteStringLiteral(key);
                WriteRaw(((_options & JsonOptions.WriteWhitespace)!=0) ? ": " : ":");
            }

            public void WriteRaw(string str)
            {
                _atStartOfLine = false;
                _writer.Write(str);
            }

            public void WriteStringLiteral(string str)
            {
                _writer.Write("\"");

                foreach (var ch in str)
                {
                    switch (ch)
                    {
                        case '\"': _writer.Write("\\\""); break;
                        case '\r': _writer.Write("\\r"); break;
                        case '\n': _writer.Write("\\n"); break;
                        case '\t': _writer.Write("\\t"); break;
                        case '\0': _writer.Write("\\0"); break;
                        case '\\': _writer.Write("\\\\"); break;
                        case '\'': _writer.Write("\\'"); break;
                        default: _writer.Write(ch); break;
                    }
                }

                _writer.Write("\"");
            }

            void WriteBlock(string open, string close, Action callback)
            {
                var prevBlockKind = _currentBlockKind;
                _currentBlockKind = open[0];

                var didNeedElementSeparator = _needElementSeparator;
                _needElementSeparator = false;

                callback();

                if (_needElementSeparator)
                {
                    IndentLevel--;
                    NextLine();
                }
                else
                {
                    WriteRaw(open);
                }
                WriteRaw(close);

                _needElementSeparator = didNeedElementSeparator;
                _currentBlockKind = prevBlockKind;
            }

            public void WriteArray(Action callback)
            {
                WriteBlock("[", "]", callback);
            }

            public void WriteDictionary(Action callback)
            {
                WriteBlock("{", "}", callback);
            }

            public void WriteValue(object value)
            {
                // Special handling for null
                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }

                var type = value.GetType();

                // Handle nullable types
                var typeUnderlying = Nullable.GetUnderlyingType(type);
                if (typeUnderlying != null)
                    type = typeUnderlying;

                // Look up type writer
                Action<IJsonWriter, object> typeWriter;
                if (_typeWriters.TryGetValue(type, out typeWriter))
                {
                    // Write it
                    typeWriter(this, value);
                    return;
                }

                // Enumerated type?
                if (type.IsEnum)
                {
                    WriteStringLiteral(value.ToString());
                    return;
                }

                // Dictionary?
                var d = value as System.Collections.IDictionary;
                if (d != null)
                {
                    WriteDictionary(() =>
                    {
                        foreach (var key in d.Keys)
                        {
                            WriteKey(key.ToString());
                            WriteValue(d[key]);
                        }
                    });
                    return;
                }

                // Array?
                var e = value as System.Collections.IEnumerable;
                if (e != null)
                {
                    WriteArray(() =>
                    {
                        foreach (var i in e)
                        {
                            WriteElement();
                            WriteValue(i);
                        }
                    });
                    return;
                }

                // Try using reflection
                var ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri != null)
                {
                    ri.Write(this, value);
                    return;
                }

                // What the?
                throw new InvalidDataException(string.Format("Don't know how to write '{0}' to json", value.GetType()));
            }
        }

        class JsonMemberInfo
        {
            public MemberInfo Member;
            public string JsonKey;
            public bool KeepInstance;

            public Type MemberType
            {
                get
                {
                    if (Member is PropertyInfo)
                    {
                        return ((PropertyInfo)Member).PropertyType;
                    }
                    else
                    {
                        return ((FieldInfo)Member).FieldType;
                    }
                }
            }

            public void SetValue(object target, object value)
            {
                if (Member is PropertyInfo)
                {
                    ((PropertyInfo)Member).SetValue(target, value, null);
                }
                else
                {
                    ((FieldInfo)Member).SetValue(target, value);
                }
            }

            public object GetValue(object target)
            {
                if (Member is PropertyInfo)
                {
                    return ((PropertyInfo)Member).GetValue(target, null);
                }
                else
                {
                    return ((FieldInfo)Member).GetValue(target);
                }
            }
        }

        class ReflectionInfo
        {
            List<JsonMemberInfo> _members;

            static Dictionary<Type, ReflectionInfo> _cache = new Dictionary<Type, ReflectionInfo>();

            public void Write(Writer w, object val)
            {
                w.WriteDictionary(() =>
                {
                    var writing = val as IJsonWriting;
                    if (writing != null)
                        writing.OnJsonWriting(w);

                    foreach (var jmi in _members)
                    {
                        w.WriteKey(jmi.JsonKey);
                        w.WriteValue(jmi.GetValue(val));
                    }

                    var written = val as IJsonWritten;
                    if (written != null)
                        written.OnJsonWritten(w);
                });
            }

            // The member info is stored in a list (as opposed to a dictionary) so that
            // the json is written in the same order as the fields/properties are defined
            // On loading, we assume the fields will be in the same order, but need to
            // handle if they're not.  This function performs a linear search, but
            // starts after the last found item as an optimization that should work
            // most of the time.
            int _lastFoundIndex = 0;
            bool FindMemberInfo(string name, out JsonMemberInfo found)
            {
                for (int i = 0; i < _members.Count; i++)
                {
                    int index = (i + _lastFoundIndex) % _members.Count;
                    var jmi = _members[index];
                    if (jmi.JsonKey == name)
                    {
                        _lastFoundIndex = index;
                        found = jmi;
                        return true;
                    }
                }
                found = null;
                return false;
            }

            public void ParseFieldOrProperty(Reader r, object into, string key)
            {
                var lf = into as IJsonLoadField;
                if (lf != null)
                {
                    if (lf.OnJsonField(r, key))
                        return;
                }

                JsonMemberInfo jmi;
                if (FindMemberInfo(key, out jmi))
                {
                    if (jmi.KeepInstance && Reader.CanParseInto(jmi.MemberType))
                    {
                        var subInto = jmi.GetValue(into);
                        if (subInto != null)
                        {
                            r.ParseInto(subInto);
                            return;
                        }
                    }

                    var val = r.Parse(jmi.MemberType);
                    jmi.SetValue(into, val);
                    return;
                }
            }

            public void ParseInto(Reader r, object into)
            {
                var loading = into as IJsonLoading;

                r.ReadDictionary(key => {

                    if (loading != null)
                    {
                        loading.OnJsonLoading(r);
                        loading = null;
                    }
                    
                    ParseFieldOrProperty(r, into, key);
                });

                var loaded = into as IJsonLoaded;
                if (loaded != null)
                    loaded.OnJsonLoaded(r);
            }

            public static ReflectionInfo GetReflectionInfo(Type type)
            {
                // Already created?
                ReflectionInfo existing;
                if (_cache.TryGetValue(type, out existing))
                    return existing;

                // Does type have a [Json] attribute
                bool typeMarked = type.GetCustomAttributes(typeof(JsonAttribute), true).OfType<JsonAttribute>().Any();

                // Do any members have a [Json] attribute
                bool anyFieldsMarked = GetAllFieldsAndProperties(type).Any(x => x.GetCustomAttributes(typeof(JsonAttribute), false).OfType<JsonAttribute>().Any());

                // Should we serialize all public methods?
                bool serializeAllPublics = typeMarked || !anyFieldsMarked;

                // Build 
                var ri = CreateReflectionInfo(type, mi =>
                {
                    // Explicitly excluded?
                    if (mi.GetCustomAttributes(typeof(JsonExcludeAttribute), false).OfType<JsonExcludeAttribute>().Any())
                        return null;

                    // Get attributes
                    var attr = mi.GetCustomAttributes(typeof(JsonAttribute), false).OfType<JsonAttribute>().FirstOrDefault();
                    if (attr != null)
                    {
                        return new JsonMemberInfo()
                        {
                            Member = mi,
                            JsonKey = attr.Key ?? mi.Name.Substring(0, 1).ToLower() + mi.Name.Substring(1),
                            KeepInstance = attr.KeepInstance,
                        };
                    }

                    // Serialize all publics?
                    if (serializeAllPublics && IsPublic(mi))
                    {
                        return new JsonMemberInfo()
                        {
                            Member = mi,
                            JsonKey = mi.Name.Substring(0, 1).ToLower() + mi.Name.Substring(1),
                        };
                    }

                    return null;
                });

                // Cache it
                _cache[type] = ri;

                return ri;
            }

            static bool IsPublic(MemberInfo mi)
            {
                // Public field
                var fi = mi as FieldInfo;
                if (fi!=null)
                    return fi.IsPublic;

                // Public property
                // (We only check the get method so we can work with anonymous types)
                var pi = mi as PropertyInfo;
                if (pi != null)
                {
                    var gm = pi.GetGetMethod();
                    return (gm != null && gm.IsPublic);
                }

                return false;
            }

            public static ReflectionInfo CreateReflectionInfo(Type type, Func<MemberInfo, JsonMemberInfo> callback)
            {
                // Already created?
                ReflectionInfo existing;
                if (_cache.TryGetValue(type, out existing))
                    return existing;

                // Work out properties and fields
                var members = GetAllFieldsAndProperties(type).Select(x => callback(x)).Where(x => x != null).ToList();

                // Must have some members
                if (!members.Any())
                    return null;

                // Create reflection info
                var ri = new ReflectionInfo()
                {
                    _members = members,
                };

                return ri;
            }

            static IEnumerable<MemberInfo> GetAllFieldsAndProperties(Type t)
            {
                if (t == null)
                    return Enumerable.Empty<FieldInfo>();

                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                return t.GetMembers(flags).Where(x => x is FieldInfo || x is PropertyInfo).Concat(GetAllFieldsAndProperties(t.BaseType));
            }

        }

        internal static class Utils
        {
            public static long ToUnixMilliseconds(DateTime This)
            {
                return (long)This.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
            }

            public static DateTime FromUnixMilliseconds(long timeStamp)
            {
                return new DateTime(1970, 1, 1).AddMilliseconds(timeStamp);
            }

        }

        public class RewindableTextReader
        {
            public RewindableTextReader(TextReader underlying)
            {
                _underlying = underlying;
                ReadUnderlying();
            }

            TextReader _underlying;
            char[] _buf = new char[1024];
            int _pos;
            int _bufUsed;
            StringBuilder _rewindBuffer;
            int _rewindBufferPos;

            void ReadUnderlying()
            {
                _bufUsed = _underlying.Read(_buf, 0, _buf.Length);
                _pos = 0;
            }

            char ReadUnderlyingChar()
            {
                if (_pos >= _bufUsed)
                {
                    if (_bufUsed > 0)
                    {
                        ReadUnderlying();
                    }
                    if (_bufUsed == 0)
                    {
                        return '\0';
                    }
                }

                // Next
                return _buf[_pos++];
            }

            public char ReadChar()
            {
                if (_rewindBuffer == null)
                    return ReadUnderlyingChar();

                if (_rewindBufferPos < _rewindBuffer.Length)
                {
                    return _rewindBuffer[_rewindBufferPos++];
                }
                else
                {
                    char ch = ReadUnderlyingChar();
                    _rewindBuffer.Append(ch);
                    _rewindBufferPos++;
                    return ch;
                }
            }


            Stack<int> _bookmarks = new Stack<int>();

            public void CreateBookmark()
            {
                if (_rewindBuffer == null)
                {
                    _rewindBuffer = new StringBuilder();
                    _rewindBufferPos = 0;
                }

                _bookmarks.Push(_rewindBufferPos);
            }

            public void RewindToBookmark()
            {
                _rewindBufferPos = _bookmarks.Pop();
            }

            public void DiscardBookmark()
            {
                _bookmarks.Pop();
                if (_bookmarks.Count == 0)
                {
                    _rewindBuffer = null;
                    _rewindBufferPos = 0;
                }
            }
        }

        public class Tokenizer
        {
            public Tokenizer(TextReader r, JsonOptions options)
            {
                _reader = new RewindableTextReader(r);
                _options = options;

                _state._nextCharPos.Line = 0;
                _state._nextCharPos.Offset = 0;
                _state._currentCharPos = _state._nextCharPos;

                // Load up
                NextChar();
                NextToken();
            }

            RewindableTextReader _reader;
            JsonOptions _options;
            StringBuilder _sb = new StringBuilder();
            ReaderState _state;

            // this object represents the entire state of the reader
            // which when combined with the RewindableTextReader allows
            // use to rewind to an arbitrary point in the token stream
            struct ReaderState
            {
                public JsonLineOffset _nextCharPos;
                public JsonLineOffset _currentCharPos;
                public JsonLineOffset _currentTokenPos;
                public char _currentChar;
                public char _pendingChar;
                public Token _currentToken;
                public string _string;
                public object _literal;
            }

            Stack<ReaderState> _bookmarks = new Stack<ReaderState>();

            public void CreateBookmark()
            {
                _bookmarks.Push(_state);
                _reader.CreateBookmark();
            }

            public void DiscardBookmark()
            {
                _bookmarks.Pop();
                _reader.DiscardBookmark();
            }

            public void RewindToBookmark()
            {
                _state = _bookmarks.Pop();
                _reader.RewindToBookmark();
            }

            char NextChar()
            {
                // Normalize line endings to '\n'
                char ch;
                if (_state._pendingChar != '\0')
                {
                    ch = _state._pendingChar;
                }
                else
                {
                    ch = _reader.ReadChar();
                    if (ch == '\r')
                    {
                        ch = _reader.ReadChar();
                        if (ch != '\n')
                        {
                            _state._pendingChar = ch;
                            ch = '\n';
                        }
                    }
                }

                _state._currentCharPos = _state._nextCharPos;

                // Update line position counter
                if (ch == '\n')
                {
                    _state._nextCharPos.Line++;
                    _state._nextCharPos.Offset = 0;
                }
                else
                {
                    _state._nextCharPos.Offset++;
                }

                return _state._currentChar = ch;
            }

            public bool DidMove
            {
                get;
                set;
            }

            public void NextToken()
            {
                DidMove = true;

                _sb.Length = 0;
                State state = State.Initial;

                _state._currentTokenPos = _state._currentCharPos;

                while (true)
                {
                    switch (state)
                    {
                        case State.Initial:
                            if (IsIdentifierLeadChar(_state._currentChar))
                            {
                                state = State.Identifier;
                                break;
                            }
                            else if (char.IsDigit(_state._currentChar) || _state._currentChar == '-')
                            {
                                TokenizeNumber();
                                _state._currentToken =  Token.Literal;
                                return;
                            }
                            else if (char.IsWhiteSpace(_state._currentChar))
                            {
                                NextChar();
                                _state._currentTokenPos = _state._currentCharPos;
                                break;
                            }

                            switch (_state._currentChar)
                            {
                                case '/':
                                    if ((_options & JsonOptions.StrictParser)!=0)
                                    {
                                        // Comments not support in strict mode
                                        throw new InvalidDataException(string.Format("syntax error - unexpected character '{0}'", _state._currentChar));
                                    }
                                    else
                                    {
                                        NextChar();
                                        state = State.Slash;
                                    }
                                    break;

                                case '\"':
                                    NextChar();
                                    _sb.Length = 0;
                                    state = State.QuotedString;
                                    break;

                                case '\'':
                                    NextChar();
                                    _sb.Length = 0;
                                    state = State.QuotedChar;
                                    break;

                                case '\0':
                                    _state._currentToken =  Token.EOF;
                                    return;

                                case '{': _state._currentToken =  Token.OpenBrace; NextChar(); return;
                                case '}': _state._currentToken =  Token.CloseBrace; NextChar(); return;
                                case '[': _state._currentToken =  Token.OpenSquare; NextChar(); return;
                                case ']': _state._currentToken =  Token.CloseSquare; NextChar(); return;
                                case '=': _state._currentToken =  Token.Equal; NextChar(); return;
                                case ':': _state._currentToken =  Token.Colon; NextChar(); return;
                                case ';': _state._currentToken =  Token.SemiColon; NextChar(); return;
                                case ',': _state._currentToken =  Token.Comma; NextChar(); return;

                                default:
                                    throw new InvalidDataException(string.Format("syntax error - unexpected character '{0}'", _state._currentChar));
                            }
                            break;

                        case State.Slash:
                            switch (_state._currentChar)
                            {
                                case '/':
                                    NextChar();
                                    state = State.SingleLineComment;
                                    break;

                                case '*':
                                    NextChar();
                                    state = State.BlockComment;
                                    break;

                                default:
                                    throw new InvalidDataException("syntax error - unexpected character after slash");
                            }
                            break;


                        case State.SingleLineComment:
                            if (_state._currentChar == '\r' || _state._currentChar == '\n')
                            {
                                state = State.Initial;
                            }
                            NextChar();
                            break;

                        case State.BlockComment:
                            if (_state._currentChar == '*')
                            {
                                NextChar();
                                if (_state._currentChar == '/')
                                {
                                    state = State.Initial;
                                }
                            }
                            NextChar();
                            break;

                        case State.Identifier:
                            if (IsIdentifierChar(_state._currentChar))
                            {
                                _sb.Append(_state._currentChar);
                                NextChar();
                            }
                            else
                            {
                                _state._string = _sb.ToString();
                                switch (this.String)
                                {
                                    case "true":
                                        _state._literal = true;
                                        _state._currentToken =  Token.Literal;
                                        break;

                                    case "false":
                                        _state._literal = false;
                                        _state._currentToken =  Token.Literal;
                                        break;

                                    case "null":
                                        _state._literal = null;
                                        _state._currentToken =  Token.Literal;
                                        break;

                                    default:
                                        _state._currentToken =  Token.Identifier;
                                        break;
                                }
                                return;
                            }
                            break;

                        case State.QuotedString:
                            if (_state._currentChar == '\\')
                            {
                                AppendSpecialChar();
                            }
                            else if (_state._currentChar == '\"')
                            {
                                _state._literal = _sb.ToString();
                                _state._currentToken =  Token.Literal;
                                NextChar();
                                return;
                            }
                            else
                            {
                                _sb.Append(_state._currentChar);
                                NextChar();
                                continue;
                            }

                            break;

                        case State.QuotedChar:
                            if (_state._currentChar == '\\')
                            {
                                AppendSpecialChar();
                            }
                            else if (_state._currentChar == '\'')
                            {
                                _state._literal = _sb.ToString();

                                _state._currentToken =  Token.Literal;
                                NextChar();
                                return;
                            }
                            else
                            {
                                _sb.Append(_state._currentChar);
                                NextChar();
                                continue;
                            }
                            break;
                    }
                }
            }

            void TokenizeNumber()
            {
                _sb.Length = 0;

                // Leading -
                bool signed = false;
                if (_state._currentChar == '-')
                {
                    signed = true;
                    _sb.Append(_state._currentChar);
                    NextChar();
                    if (!Char.IsDigit(_state._currentChar))
                    {
                        throw new InvalidDataException("syntax error - expected digit to follow negative sign");
                    }
                }

                // Parse all digits
                bool fp = false;
                while (char.IsDigit(_state._currentChar) || _state._currentChar == '.' || _state._currentChar == 'e' || _state._currentChar == 'E' || _state._currentChar == 'x' || _state._currentChar == 'X')
                {
                    if (_state._currentChar == 'e' || _state._currentChar == 'E')
                    {
                        fp = true;
                        _sb.Append(_state._currentChar);

                        NextChar();
                        if (_state._currentChar == '-' || _state._currentChar == '+')
                        {
                            _sb.Append(_state._currentChar);
                            NextChar();
                        }
                    }
                    else
                    {
                        if (_state._currentChar == '.')
                            fp = true;

                        _sb.Append(_state._currentChar);
                        NextChar();
                    }
                }

                Type type = fp ? typeof(double) : (signed ? typeof(long) : typeof(ulong));
                if (char.IsLetterOrDigit(_state._currentChar))
                    throw new InvalidDataException(string.Format("syntax error - invalid character following number '{0}'", _state._currentChar));

                // Convert type
                try
                {
                    _state._literal = Convert.ChangeType(_sb.ToString(), type, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch
                {
                    throw new InvalidDataException(string.Format("syntax error - incorrectly formatted number '{0}'", _sb.ToString()));
                }

            }

            public void Check(Token tokenRequired)
            {
                if (tokenRequired != CurrentToken)
                {
                    throw new InvalidDataException(string.Format("syntax error - expected {0} found {1}", tokenRequired, CurrentToken));
                }
            }

            public void Skip(Token tokenRequired)
            {
                Check(tokenRequired);
                NextToken();
            }


            public bool SkipIf(Token tokenRequired)
            {
                if (tokenRequired == CurrentToken)
                {
                    NextToken();
                    return true;
                }
                return false;
            }


            void AppendSpecialChar()
            {
                NextChar();
                var escape = _state._currentChar;
                NextChar();
                switch (escape)
                {
                    case '\'': _sb.Append('\''); break;
                    case '\"': _sb.Append('\"'); break;
                    case '\\': _sb.Append('\\'); break;
                    case 'r': _sb.Append('\r'); break;
                    case 'n': _sb.Append('\n'); break;
                    case 't': _sb.Append('\t'); break;
                    case '0': _sb.Append('\0'); break;
                    case 'u':
						var sbHex = new StringBuilder();
						for (int i = 0; i < 4; i++)
						{
							sbHex.Append(_state._currentChar);
							NextChar();
						}
						_sb.Append((char)Convert.ToUInt16(sbHex.ToString(), 16));
                        break;

                    default:
                        throw new InvalidDataException(string.Format("Invalid escape sequence in string literal: '\\{0}'", _state._currentChar));
                }
            }

            public Token CurrentToken
            {
                get { return _state._currentToken; }
            }

            public JsonLineOffset CurrentTokenPosition
            {
                get { return _state._currentTokenPos; }
            }

            public string String
            {
                get { return _state._string; }
            }

            public object Literal
            {
                get { return _state._literal; }
            }

            private enum State
            {
                Initial,
                Slash,
                SingleLineComment,
                BlockComment,
                Identifier,
                QuotedString,
                QuotedChar,
            }

            public static bool IsIdentifierChar(char ch)
            {
                return Char.IsLetterOrDigit(ch) || ch == '_' || ch == '$';
            }

            public static bool IsIdentifierLeadChar(char ch)
            {
                return Char.IsLetter(ch) || ch == '_' || ch == '$';
            }

            public static bool IsIdentifier(string str)
            {
                return IsIdentifierLeadChar(str[0]) && str.All(x => IsIdentifierChar(x));
            }
        }
    }
}