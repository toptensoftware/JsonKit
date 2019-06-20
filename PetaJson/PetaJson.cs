// PetaJson v0.5 - A simple but flexible Json library in a single .cs file.
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

// Define PETAJSON_NO_DYNAMIC to disable Expando support
// Define PETAJSON_NO_EMIT to disable Reflection.Emit
// Define PETAJSON_NO_DATACONTRACT to disable support for [DataContract]/[DataMember]

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Collections;
using System.Threading;
#if !PETAJSON_NO_DYNAMIC
using System.Dynamic;
#endif
#if !PETAJSON_NO_EMIT
using System.Reflection.Emit;
#endif
#if !PETAJSON_NO_DATACONTRACT
using System.Runtime.Serialization;
#endif



namespace PetaJson
{
    // API
    public static class Json
    {
        static Json()
        {
            WriteWhitespaceDefault = true;
            StrictParserDefault = false;

#if !PETAJSON_NO_EMIT
            Json.SetFormatterResolver(Emit.MakeFormatter);
            Json.SetParserResolver(Emit.MakeParser);
            Json.SetIntoParserResolver(Emit.MakeIntoParser);
#endif
        }

        // Pretty format default
        public static bool WriteWhitespaceDefault
        {
            get;
            set;
        }

        // Strict parser
        public static bool StrictParserDefault
        {
            get;
            set;
        }

        // Write an object to a text writer
        public static void Write(TextWriter w, object o, JsonOptions options = JsonOptions.None)
        {
            var writer = new JsonWriter(w, ResolveOptions(options));
            writer.WriteValue(o);
        }

        public static bool SavePreviousVersions
        {
            get;
            set;
        }

        // Write a file atomically by writing to a temp file and then renaming it - prevents corrupted files if crash 
        // in middle of writing file.
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

        // Write an object to a file
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

        // Format an object as a json string
        public static string Format(object o, JsonOptions options = JsonOptions.None)
        {
            var sw = new StringWriter();
            var writer = new JsonWriter(sw, ResolveOptions(options));
            writer.WriteValue(o);
            return sw.ToString();
        }

        // Parse an object of specified type from a text reader
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
				var loc = reader == null ? new JsonLineOffset() : reader.CurrentTokenPosition;
				Console.WriteLine("Exception thrown while parsing JSON at {0}, context:{1}\n{2}", loc, reader?.Context, x.ToString()); 
				throw new JsonParseException(x, reader?.Context, loc);
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
				var loc = reader == null ? new JsonLineOffset() : reader.CurrentTokenPosition;
				Console.WriteLine("Exception thrown while parsing JSON at {0}, context:{1}\n{2}", loc, reader.Context, x.ToString()); 
				throw new JsonParseException(x,reader.Context,loc);
            }
        }

        // Parse an object of specified type from a file
        public static object ParseFile(string filename, Type type, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                return Parse(r, type, options);
            }
        }

        // Parse an object of specified type from a file
        public static T ParseFile<T>(string filename, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                return Parse<T>(r, options);
            }
        }

        // Parse from file into an already instantied object
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

        // Create a clone of an object
        public static T Clone<T>(T source)
        {
            return (T)Reparse(source.GetType(), source);
        }

        // Create a clone of an object (untyped)
        public static object Clone(object source)
        {
            return Reparse(source.GetType(), source);
        }

        // Clone an object into another instance
        public static void CloneInto(object dest, object source)
        {
            ReparseInto(dest, source);
        }

        // Reparse an object by writing to a stream and re-reading (possibly
        // as a different type).
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

        // Typed version of above
        public static T Reparse<T>(object source)
        {
            return (T)Reparse(typeof(T), source);
        }

        // Reparse one object into another object 
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

        // Register a callback that can format a value of a particular type into json
        public static void RegisterFormatter(Type type, Action<IJsonWriter, object> formatter)
        {
            JsonWriter._formatters[type] = formatter;
        }

        // Typed version of above
        public static void RegisterFormatter<T>(Action<IJsonWriter, T> formatter)
        {
            RegisterFormatter(typeof(T), (w, o) => formatter(w, (T)o));
        }

        // Register a parser for a specified type
        public static void RegisterParser(Type type, Func<IJsonReader, Type, object> parser)
        {
            JsonReader._parsers.Set(type, parser);
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

        // Register an into parser
        public static void RegisterIntoParser(Type type, Action<IJsonReader, object> parser)
        {
            JsonReader._intoParsers.Set(type, parser);
        }

        // Register an into parser
        public static void RegisterIntoParser<T>(Action<IJsonReader, object> parser)
        {
            RegisterIntoParser(typeof(T), parser);
        }

        // Register a factory for instantiating objects (typically abstract classes)
        // Callback will be invoked for each key in the dictionary until it returns an object
        // instance and which point it will switch to serialization using reflection
        public static void RegisterTypeFactory(Type type, Func<IJsonReader, string, object> factory)
        {
            JsonReader._typeFactories.Set(type, factory);
        }

        // Register a callback to provide a formatter for a newly encountered type
        public static void SetFormatterResolver(Func<Type, Action<IJsonWriter, object>> resolver)
        {
            JsonWriter._formatterResolver = resolver;
        }

        // Register a callback to provide a parser for a newly encountered value type
        public static void SetParserResolver(Func<Type, Func<IJsonReader, Type, object>> resolver)
        {
            JsonReader._parserResolver = resolver;
        }

        // Register a callback to provide a parser for a newly encountered reference type
        public static void SetIntoParserResolver(Func<Type, Action<IJsonReader, object>> resolver)
        {
            JsonReader._intoParserResolver = resolver;
        }


        // Resolve passed options        
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
