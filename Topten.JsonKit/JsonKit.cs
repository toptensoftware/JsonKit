// JsonKit v0.5 - A simple but flexible Json library in a single .cs file.
// 
// Copyright (C) 2014 Topten Software (contact@toptensoftware.com) All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this product 
// except in compliance with the License. You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software distributed under the 
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
// either express or implied. See the License for the specific language governing permissions 
// and limitations under the License.

using System;
using System.IO;

namespace Topten.JsonKit
{
    /// <summary>
    /// The main static interface class to JsonKit
    /// </summary>
    public static class Json
    {
        static Json()
        {
            WriteWhitespaceDefault = true;
            StrictParserDefault = false;

#if !JSONKIT_NO_EMIT
            Json.SetFormatterResolver(Emit.MakeFormatter);
            Json.SetParserResolver(Emit.MakeParser);
            Json.SetIntoParserResolver(Emit.MakeIntoParser);
#endif
        }

        /// <summary>
        /// Controls whether the write whitespace by default
        /// </summary>
        public static bool WriteWhitespaceDefault
        {
            get;
            set;
        }

        /// <summary>
        /// Controls whether parsing should be strict by default
        /// </summary>
        public static bool StrictParserDefault
        {
            get;
            set;
        }

        // Write an object to a text writer
        /// <summary>
        /// Writes an object to a TextWriter
        /// </summary>
        /// <param name="w">The target text writer</param>
        /// <param name="o">The object to be written</param>
        /// <param name="options">Options controlling output formatting</param>
        public static void Write(TextWriter w, object o, JsonOptions options = JsonOptions.None)
        {
            var writer = new JsonWriter(w, ResolveOptions(options));
            writer.WriteValue(o);
        }


        /// <summary>
        /// Controls whether previous version should be saved if AutoSavePreviousVersion is used
        /// </summary>
        public static bool SavePreviousVersions
        {
            get;
            set;
        }

        /// <summary>
        /// Write a file atomically by writing to a temp file and then renaming it - prevents corrupted files if crash 
        /// in middle of writing file.
        /// </summary>
        /// <param name="filename">The output file name</param>
        /// <param name="o">The object to be written</param>
        /// <param name="options">Options controlling output</param>
        /// <param name="backupFilename">An optional back filename where previous version will be written</param>
        public static void WriteFileAtomic(string filename, object o, JsonOptions options = JsonOptions.None, string backupFilename = null)
        {
            var tempName = filename + ".tmp";

            try
            {
                // Write the temp file
                WriteFile(tempName, o, (options | JsonOptions.Flush));

                if (System.IO.File.Exists(filename))
                {
                    bool savePreviousVersion = false;

                    if ((options & JsonOptions.AutoSavePreviousVersion)!=0)
                    {
                        savePreviousVersion = SavePreviousVersions;
                    }
                    else if ((options & JsonOptions.SavePreviousVersion)!=0)
                    {
                        savePreviousVersion = true;
                    }


                    // Work out backup filename
                    if (savePreviousVersion)
                    {
                        // Make sure have a backup filename
                        if (backupFilename == null)
                        {
                            backupFilename = filename + ".previous";
                        }
                    }
                    else
                    {
                        // No backup
                        backupFilename = null;
                    }

                    // Replace it
                    int retry = 0;
                    while (true)
                    {
                        try
                        {
                            File.Replace(tempName, filename, backupFilename);
                            break;
                        }
                        catch (System.IO.IOException x)
                        {
                            retry++;
                            if (retry >= 5)
                            {
                                throw new System.IO.IOException(string.Format("Failed to replace temp file {0} with {1} and backup {2}, reason {3}", tempName, filename, backupFilename, x.Message), x);
                            }
                            System.Threading.Thread.Sleep(2000);
                        }
                    }
                }
                else
                {
                    // Rename it
                    File.Move(tempName, filename);
                }
            }
            catch
            {
                Utils.DeleteFile(tempName);
                throw;
            }
        }

        /// <summary>
        /// Write an object to a file
        /// </summary>
        /// <param name="filename">The output filename</param>
        /// <param name="o">The object to be written</param>
        /// <param name="options">Options controlling output</param>
        public static void WriteFile(string filename, object o, JsonOptions options = JsonOptions.None)
        {
            using (var w = new StreamWriter(filename))
            {
                Write(w, o, options);

                if ((options & JsonOptions.Flush) != 0)
                {
                    w.Flush();
                    w.BaseStream.Flush();
                }
            }
        }

        /// <summary>
        /// Format an object as a json string
        /// </summary>
        /// <param name="o">The value to be formatted</param>
        /// <param name="options">Options controlling output</param>
        /// <returns>The formatted string</returns>
        public static string Format(object o, JsonOptions options = JsonOptions.None)
        {
            var sw = new StringWriter();
            var writer = new JsonWriter(sw, ResolveOptions(options));
            writer.WriteValue(o);
            return sw.ToString();
        }

        /// <summary>
        /// Parse an object of specified type from a text reader 
        /// </summary>
        /// <param name="r">The text reader to read from</param>
        /// <param name="type">The type of object to be parsed</param>
        /// <param name="options">Options controlling parsing</param>
        /// <returns>The parsed object</returns>
        public static object Parse(TextReader r, Type type, JsonOptions options = JsonOptions.None)
        {
            JsonReader reader = null;
            try
            {
                reader = new JsonReader(r, ResolveOptions(options));
                var retv = reader.Parse(type);
                reader.CheckEOF();
                return retv;
            }
            catch (Exception x)
            {
				var loc = reader == null ? new LineOffset() : reader.CurrentTokenPosition;
				Console.WriteLine("Exception thrown while parsing JSON at {0}, context:{1}\n{2}", loc, reader?.Context, x.ToString()); 
				throw new JsonParseException(x, reader?.Context, loc);
            }
        }

        /// <summary>
        /// Parse an object of specified type from a text reader 
        /// </summary>
        /// <typeparam name="T">The type of object to be parsed</typeparam>
        /// <param name="r">The text reader to read from</param>
        /// <param name="options">Options controlling parsing</param>
        /// <returns>The parsed object</returns>
        public static T Parse<T>(TextReader r, JsonOptions options = JsonOptions.None)
        {
            return (T)Parse(r, typeof(T), options);
        }

        /// <summary>
        /// Parse from text reader into an already instantied object 
        /// </summary>
        /// <param name="r">The text reader to read from </param>
        /// <param name="into">The object to serialize into</param>
        /// <param name="options">Options controlling parsing</param>
        public static void ParseInto(TextReader r, Object into, JsonOptions options = JsonOptions.None)
        {
            if (into == null)
                throw new NullReferenceException();
            if (into.GetType().IsValueType)
                throw new InvalidOperationException("Can't ParseInto a value type");

            JsonReader reader = null;
            try
            {
                reader = new JsonReader(r, ResolveOptions(options));
                reader.ParseInto(into);
                reader.CheckEOF();
            }
            catch (Exception x)
            {
				var loc = reader == null ? new LineOffset() : reader.CurrentTokenPosition;
				Console.WriteLine("Exception thrown while parsing JSON at {0}, context:{1}\n{2}", loc, reader.Context, x.ToString()); 
				throw new JsonParseException(x,reader.Context,loc);
            }
        }

        /// <summary>
        /// Parse an object of specified type from a file
        /// </summary>
        /// <param name="filename">The input filename</param>
        /// <param name="type">The type of object to be parsed</param>
        /// <param name="options">Options controlling parsing</param>
        /// <returns>The parsed object instance</returns>
        public static object ParseFile(string filename, Type type, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                return Parse(r, type, options);
            }
        }

        // Parse an object of specified type from a file
        /// <summary>
        /// Parse an object of specified type from a file
        /// </summary>
        /// <typeparam name="T">The type of object to be parsed</typeparam>
        /// <param name="filename">The input filename</param>
        /// <param name="options">Options controlling parsing</param>
        /// <returns></returns>
        public static T ParseFile<T>(string filename, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                return Parse<T>(r, options);
            }
        }

        /// <summary>
        /// Parse from file into an already instantied object 
        /// </summary>
        /// <param name="filename">The input filename</param>
        /// <param name="into">The object to serialize into</param>
        /// <param name="options">Options controlling parsing</param>
        public static void ParseFileInto(string filename, Object into, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                ParseInto(r, into, options);
            }
        }

        /// <summary>
        /// Parse an object from a string 
        /// </summary>
        /// <param name="data">The JSON data</param>
        /// <param name="type">The type of object to be parsed</param>
        /// <param name="options">Options controlling parsing</param>
        /// <returns>The parsed object instance</returns>
        public static object Parse(string data, Type type, JsonOptions options = JsonOptions.None)
        {
            return Parse(new StringReader(data), type, options);
        }

        /// <summary>
        /// Parse an object from a string 
        /// </summary>
        /// <typeparam name="T">The type of object to be parsed</typeparam>
        /// <param name="data">The JSON data</param>
        /// <param name="options">Options controlling parsing</param>
        /// <returns></returns>
        public static T Parse<T>(string data, JsonOptions options = JsonOptions.None)
        {
            return (T)Parse<T>(new StringReader(data), options);
        }

        /// <summary>
        /// Parse from string into an already instantiated object 
        /// </summary>
        /// <param name="data">The JSON data</param>
        /// <param name="into">The object to serialize into</param>
        /// <param name="options">Options controlling parsing</param>
        public static void ParseInto(string data, Object into, JsonOptions options = JsonOptions.None)
        {
            ParseInto(new StringReader(data), into, options);
        }

        /// <summary>
        /// Create a clone of an object by serializing to JSON and the deserializing into a new instance
        /// </summary>
        /// <typeparam name="T">The type of object to be cloned</typeparam>
        /// <param name="source">The object to be cloned</param>
        /// <returns>A cloned instance</returns>
        public static T Clone<T>(T source)
        {
            return (T)Reparse(source.GetType(), source);
        }

        // Create a clone of an object (untyped)
        /// <summary>
        /// Create a clone of an object by serializing to JSON and the deserializing into a new instance
        /// </summary>
        /// <param name="source">The object to be cloned</param>
        /// <returns>A cloned instance</returns>
        public static object Clone(object source)
        {
            return Reparse(source.GetType(), source);
        }

        /// <summary>
        /// Clone an object into another instance 
        /// </summary>
        /// <param name="dest">The object to clone to</param>
        /// <param name="source">The object to clone from</param>
        public static void CloneInto(object dest, object source)
        {
            ReparseInto(dest, source);
        }

        /// <summary>
        /// Reparse an object by writing to a stream and re-reading (possibly
        /// as a different type).
        /// </summary>
        /// <param name="type">The type of object to deserialize as</param>
        /// <param name="source">The source object to be reparsed</param>
        /// <returns>The newly parsed object instance</returns>
        public static object Reparse(Type type, object source)
        {
            if (source == null)
                return null;
            var ms = new MemoryStream();
            try
            {
                // Write
                var w = new StreamWriter(ms);
                Json.Write(w, source);
                w.Flush();

                // Read
                ms.Seek(0, SeekOrigin.Begin);
                var r = new StreamReader(ms);
                return Json.Parse(r, type);
            }
            finally
            {
                ms.Dispose();
            }
        }

        /// <summary>
        /// Reparse an object by writing to a stream and re-reading (possibly
        /// as a different type).
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize as</typeparam>
        /// <param name="source">The source object to be reparsed</param>
        /// <returns>The newly parsed object instance</returns>
        public static T Reparse<T>(object source)
        {
            return (T)Reparse(typeof(T), source);
        }

        /// <summary>
        /// Reparse one object into another object  
        /// </summary>
        /// <param name="dest">The destination object</param>
        /// <param name="source">The source object</param>
        public static void ReparseInto(object dest, object source)
        {
            var ms = new MemoryStream();
            try
            {
                // Write
                var w = new StreamWriter(ms);
                Json.Write(w, source);
                w.Flush();

                // Read
                ms.Seek(0, SeekOrigin.Begin);
                var r = new StreamReader(ms);
                Json.ParseInto(r, dest);
            }
            finally
            {
                ms.Dispose();
            }
        }

        /// <summary>
        /// Register a callback that can format a value of a particular type into json 
        /// </summary>
        /// <param name="type">The type of object to be formatted</param>
        /// <param name="formatter">The formatter callback</param>
        public static void RegisterFormatter(Type type, Action<IJsonWriter, object> formatter)
        {
            JsonWriter._formatters.Set(type, formatter);
        }

        /// <summary>
        /// Register a callback to format the key values of dictionaries
        /// </summary>
        /// <remarks>
        /// These formatters are only used when writing .NET dictionary 
        /// key instances - not when writing properties names.
        /// </remarks>
        /// <param name="type">The type of object to be formatted</param>
        /// <param name="formatter">The formatter callback</param>
        public static void RegisterKeyFormatter(Type type, Func<object, string> formatter)
        {
            JsonWriter._keyFormatters.Set(type, formatter);
        }

        /// <summary>
        /// Register a callback to format the key values of dictionaries
        /// </summary>
        /// <remarks>
        /// These formatters are only used when writing .NET dictionary 
        /// key instances - not when writing properties names.
        /// </remarks>
        /// <typeparam name="T">The type of object to be formatted</typeparam>
        /// <param name="formatter">The formatter callback</param>
        public static void RegisterKeyFormatter<T>(Func<T, string> formatter)
        {
            JsonWriter._keyFormatters.Set(typeof(T), (o) => formatter((T)o));
        }

        /// <summary>
        /// Register a callback that can format a value of a particular type into json 
        /// </summary>
        /// <typeparam name="T">The type of object to be formatted</typeparam>
        /// <param name="formatter">The formatter callback</param>
        public static void RegisterFormatter<T>(Action<IJsonWriter, T> formatter)
        {
            RegisterFormatter(typeof(T), (w, o) => formatter(w, (T)o));
        }

        /// <summary>
        /// Register a parser for a specified type
        /// </summary>
        /// <param name="type">The type of object to be parsed</param>
        /// <param name="parser">The parser callback</param>
        public static void RegisterParser(Type type, Func<IJsonReader, Type, object> parser)
        {
            JsonReader._parsers.Set(type, parser);
        }

        /// <summary>
        /// Register a parser for a specified type
        /// </summary>
        /// <typeparam name="T">The type of object to be parsed</typeparam>
        /// <param name="parser">The parser callback</param>
        public static void RegisterParser<T>(Func<IJsonReader, Type, T> parser)
        {
            RegisterParser(typeof(T), (r, t) => parser(r, t));
        }

        /// <summary>
        /// Registers a parser for a simple literal type
        /// </summary>
        /// <param name="type">The type to be parsed</param>
        /// <param name="parser">The parser callback</param>
        public static void RegisterParser(Type type, Func<object, object> parser)
        {
            RegisterParser(type, (r, t) => r.ReadLiteral(parser));
        }

        /// <summary>
        /// Register a parser for a simple literal type
        /// </summary>
        /// <typeparam name="T">The type to be parsed</typeparam>
        /// <param name="parser">The parser callback</param>
        public static void RegisterParser<T>(Func<object, T> parser)
        {
            RegisterParser(typeof(T), literal => parser(literal));
        }

        /// <summary>
        /// Register a parser for loading into an existing instance
        /// </summary>
        /// <param name="type">The type to be parsed</param>
        /// <param name="parser">The parser callback</param>
        public static void RegisterIntoParser(Type type, Action<IJsonReader, object> parser)
        {
            JsonReader._intoParsers.Set(type, parser);
        }


        /// <summary>
        /// Register a parser for loading into an existing instance
        /// </summary>
        /// <typeparam name="T">The type to be parsed</typeparam>
        /// <param name="parser">The parser callback</param>
        public static void RegisterIntoParser<T>(Action<IJsonReader, object> parser)
        {
            RegisterIntoParser(typeof(T), parser);
        }

        /// <summary>
        /// Register a factory for instantiating objects (typically abstract classes)
        /// Callback will be invoked for each key in the dictionary until it returns an object
        /// instance and which point it will switch to serialization using reflection
        /// </summary>
        /// <param name="type">The type to be instantiated</param>
        /// <param name="factory">The factory callback</param>
        public static void RegisterTypeFactory(Type type, Func<IJsonReader, string, object> factory)
        {
            JsonReader._typeFactories.Set(type, factory);
        }

        /// <summary>
        /// Register a callback to provide a formatter for a newly encountered type 
        /// </summary>
        /// <param name="resolver">The resolver callback</param>
        public static void SetFormatterResolver(Func<Type, Action<IJsonWriter, object>> resolver)
        {
            JsonWriter._formatterResolver = resolver;
        }

        /// <summary>
        /// Register a callback to provide a parser for a newly encountered value type 
        /// </summary>
        /// <param name="resolver">The resolver callback</param>
        public static void SetParserResolver(Func<Type, Func<IJsonReader, Type, object>> resolver)
        {
            JsonReader._parserResolver = resolver;
        }

        /// <summary>
        /// Register a callback to provide a parser for a newly encountered reference type 
        /// </summary>
        /// <param name="resolver"></param>
        public static void SetIntoParserResolver(Func<Type, Action<IJsonReader, object>> resolver)
        {
            JsonReader._intoParserResolver = resolver;
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
    }
}
