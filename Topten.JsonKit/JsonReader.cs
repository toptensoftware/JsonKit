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

// Define JsonKit_NO_DYNAMIC to disable Expando support
// Define JsonKit_NO_EMIT to disable Reflection.Emit
// Define JsonKit_NO_DATACONTRACT to disable support for [DataContract]/[DataMember]

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Collections;
using System.Dynamic;


namespace Topten.JsonKit
{
    public class JsonReader : IJsonReader
    {
        static JsonReader()
        {
            // Setup default resolvers
            _parserResolver = ResolveParser;
            _intoParserResolver = ResolveIntoParser;

            Func<IJsonReader, Type, object> simpleConverter = (reader, type) =>
            {
                return reader.ReadLiteral(literal => Convert.ChangeType(literal, type, CultureInfo.InvariantCulture));
            };

            Func<IJsonReader, Type, object> numberConverter = (reader, type) =>
            {
                switch (reader.GetLiteralKind())
                {
                    case LiteralKind.SignedInteger:
                    case LiteralKind.UnsignedInteger:
                        {
                            var str = reader.GetLiteralString();
                            if (str.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                            {
                                var tempValue = Convert.ToUInt64(str.Substring(2), 16);
                                object val = Convert.ChangeType(tempValue, type, CultureInfo.InvariantCulture);
                                reader.NextToken();
                                return val;
                            }
                            else
                            {
                                object val = Convert.ChangeType(str, type, CultureInfo.InvariantCulture);
                                reader.NextToken();
                                return val;
                            }
                        }

                    case LiteralKind.FloatingPoint:
                        {
                            object val = Convert.ChangeType(reader.GetLiteralString(), type, CultureInfo.InvariantCulture);
                            reader.NextToken();
                            return val;
                        }
                }
                throw new InvalidDataException("expected a numeric literal");
            };

            // Default type handlers
            _parsers.Set(typeof(string), simpleConverter);
            _parsers.Set(typeof(char), simpleConverter);
            _parsers.Set(typeof(bool), simpleConverter);
            _parsers.Set(typeof(byte), numberConverter);
            _parsers.Set(typeof(sbyte), numberConverter);
            _parsers.Set(typeof(short), numberConverter);
            _parsers.Set(typeof(ushort), numberConverter);
            _parsers.Set(typeof(int), numberConverter);
            _parsers.Set(typeof(uint), numberConverter);
            _parsers.Set(typeof(long), numberConverter);
            _parsers.Set(typeof(ulong), numberConverter);
            _parsers.Set(typeof(decimal), numberConverter);
            _parsers.Set(typeof(float), numberConverter);
            _parsers.Set(typeof(double), numberConverter);
            _parsers.Set(typeof(DateTime), (reader, type) =>
            {
                return reader.ReadLiteral(literal => Utils.FromUnixMilliseconds((long)Convert.ChangeType(literal, typeof(long), CultureInfo.InvariantCulture)));
            });
            _parsers.Set(typeof(byte[]), (reader, type) =>
            {
                if (reader.CurrentToken == Token.OpenSquare)
                    throw new CancelReaderException();
                return reader.ReadLiteral(literal => Convert.FromBase64String((string)Convert.ChangeType(literal, typeof(string), CultureInfo.InvariantCulture)));
            });
        }

        public JsonReader(TextReader r, JsonOptions options)
        {
            _tokenizer = new Tokenizer(r, options);
            _options = options;
        }

        Tokenizer _tokenizer;
        JsonOptions _options;
        List<string> _contextStack = new List<string>();

        public string Context
        {
            get
            {
                return string.Join(".", _contextStack);
            }
        }

        static Action<IJsonReader, object> ResolveIntoParser(Type type)
        {
            var ri = ReflectionInfo.GetReflectionInfo(type);
            if (ri != null)
                return ri.ParseInto;
            else
                return null;
        }

        static Func<IJsonReader, Type, object> ResolveParser(Type type)
        {
            // See if the Type has a static parser method - T ParseJson(IJsonReader)
            var parseJson = ReflectionInfo.FindParseJson(type);
            if (parseJson != null)
            {
                if (parseJson.GetParameters()[0].ParameterType == typeof(IJsonReader))
                {
                    return (r, t) => parseJson.Invoke(null, new Object[] { r });
                }
                else
                {
                    return (r, t) =>
                    {
                        if (r.GetLiteralKind() == LiteralKind.String)
                        {
                            var o = parseJson.Invoke(null, new Object[] { r.GetLiteralString() });
                            r.NextToken();
                            return o;
                        }
                        throw new InvalidDataException(string.Format("Expected string literal for type {0}", type.FullName));
                    };
                }
            }

            return (r, t) =>
            {
                var into = DecoratingActivator.CreateInstance(type);
                r.ParseInto(into);
                return into;
            };
        }

        public LineOffset CurrentTokenPosition
        {
            get { return _tokenizer.CurrentTokenPosition; }
        }

        public Token CurrentToken
        {
            get { return _tokenizer.CurrentToken; }
        }


        // ReadLiteral is implemented with a converter callback so that any
        // errors on converting to the target type are thrown before the tokenizer
        // is advanced to the next token.  This ensures error location is reported 
        // at the start of the literal, not the following token.
        public object ReadLiteral(Func<object, object> converter)
        {
            _tokenizer.Check(Token.Literal);
            var retv = converter(_tokenizer.LiteralValue);
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
            if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.LiteralKind == LiteralKind.Null)
            {
                _tokenizer.NextToken();
                return null;
            }

            // Handle nullable types
            var typeUnderlying = Nullable.GetUnderlyingType(type);
            if (typeUnderlying != null)
                type = typeUnderlying;

            // See if we have a reader
            Func<IJsonReader, Type, object> parser;
            if (JsonReader._parsers.TryGetValue(type, out parser))
            {
                try
                {
                    return parser(this, type);
                }
                catch (CancelReaderException)
                {
                    // Reader aborted trying to read this format
                }
            }

            // See if we have factory
            Func<IJsonReader, string, object> factory;
            if (JsonReader._typeFactories.TryGetValue(type, out factory))
            {
                // Try first without passing dictionary keys
                object into = factory(this, null);
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
                    ParseDictionaryKeys(key =>
                    {
                        // Try to instantiate the object
                        into = factory(this, key);
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

            // Do we already have an into parser?
            Action<IJsonReader, object> intoParser;
            if (JsonReader._intoParsers.TryGetValue(type, out intoParser))
            {
                var into = DecoratingActivator.CreateInstance(type);
                ParseInto(into);
                return into;
            }

            // Enumerated type?
            if (type.IsEnum)
            {
                if (type.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                    return ReadLiteral(literal => {
                        try
                        {
                            return Enum.Parse(type, (string)literal);
                        }
                        catch
                        {
                            return Enum.ToObject(type, literal);
                        }
                    });
                else
					return ReadLiteral(literal => {
							
						try
						{
							return Enum.Parse(type, (string)literal);
						}
						catch (Exception)
						{
							var attr = type.GetCustomAttributes(typeof(JsonUnknownAttribute), false).FirstOrDefault();
							if (attr==null)
								throw;

							return ((JsonUnknownAttribute)attr).UnknownValue;
						}

					});
            }

            // Array?
            if (type.IsArray && type.GetArrayRank() == 1)
            {
                // First parse as a List<>
                var listType = typeof(List<>).MakeGenericType(type.GetElementType());
                var list = DecoratingActivator.CreateInstance(listType);
                ParseInto(list);

                return listType.GetMethod("ToArray").Invoke(list, null);
            }

            // IEnumerable
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                // First parse as a List<>
                var declType = type.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(declType);
                var list = DecoratingActivator.CreateInstance(listType);
                ParseInto(list);

                return list;
            }

            // Convert interfaces to concrete types
            if (type.IsInterface)
                type = Utils.ResolveInterfaceToClass(type);

            // Untyped dictionary?
            if (_tokenizer.CurrentToken == Token.OpenBrace && (type.IsAssignableFrom(typeof(IDictionary<string, object>))))
            {
                var container = (new ExpandoObject()) as IDictionary<string, object>;
                ParseDictionary(key =>
                {
                    container[key] = Parse(typeof(Object));
                });

                return container;
            }
               
            // Untyped list?
            if (_tokenizer.CurrentToken == Token.OpenSquare && (type.IsAssignableFrom(typeof(List<object>))))
            {
                var container = new List<object>();
                ParseArray(() =>
                {
                    container.Add(Parse(typeof(Object)));
                });
                return container;
            }

            // Untyped literal?
            if (_tokenizer.CurrentToken == Token.Literal && type.IsAssignableFrom(_tokenizer.LiteralType))
            {
                var lit = _tokenizer.LiteralValue;
                _tokenizer.NextToken();
                return lit;
            }

            // Call value type resolver
            if (type.IsValueType)
            {
                var tp = _parsers.Get(type, () => _parserResolver(type));
                if (tp != null)
                {
                    return tp(this, type);
                }
            }

            // Call reference type resolver
            if (type.IsClass && type != typeof(object))
            {
                var into = DecoratingActivator.CreateInstance(type);
                ParseInto(into);
                return into;
            }

            // Give up
            throw new InvalidDataException(string.Format("syntax error, unexpected token {0}", _tokenizer.CurrentToken));
        }

        // Parse into an existing object instance
        public void ParseInto(object into)
        {
            if (into == null)
                return;

            if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.LiteralKind == LiteralKind.Null)
            {
                throw new InvalidOperationException("can't parse null into existing instance");
                //return;
            }

            var type = into.GetType();

            // Existing parse into handler?
            Action<IJsonReader,object> parseInto;
            if (_intoParsers.TryGetValue(type, out parseInto))
            {
                parseInto(this, into);
                return;
            }

            // Generic dictionary?
            var dictType = Utils.FindGenericInterface(type, typeof(IDictionary<,>));
            if (dictType!=null)
            {
                // Get the key and value types
                var typeKey = dictType.GetGenericArguments()[0];
                var typeValue = dictType.GetGenericArguments()[1];

                // Parse it
                IDictionary dict = (IDictionary)into;
                dict.Clear();
                ParseDictionary(key =>
                {
                    dict.Add(Convert.ChangeType(key, typeKey), Parse(typeValue));
                });

                return;
            }

            // Generic list
            var listType = Utils.FindGenericInterface(type, typeof(IList<>));
            if (listType!=null)
            {
                // Get element type
                var typeElement = listType.GetGenericArguments()[0];

                // Parse it
                IList list = (IList)into;
                list.Clear();
                ParseArray(() =>
                {
                    list.Add(Parse(typeElement));
                });

                return;
            }

            // Untyped dictionary
            var objDict = into as IDictionary;
            if (objDict != null)
            {
                objDict.Clear();
                ParseDictionary(key =>
                {
                    objDict[key] = Parse(typeof(Object));
                });
                return;
            }

            // Untyped list
            var objList = into as IList;
            if (objList!=null)
            {
                objList.Clear();
                ParseArray(() =>
                {
                    objList.Add(Parse(typeof(Object)));
                });
                return;
            }

            // Try to resolve a parser
            var intoParser = _intoParsers.Get(type, () => _intoParserResolver(type));
            if (intoParser != null)
            {
                intoParser(this, into);
                return;
            }

            throw new InvalidOperationException(string.Format("Don't know how to parse into type '{0}'", type.FullName));
        }

        public T Parse<T>()
        {
            return (T)Parse(typeof(T));
        }

        public LiteralKind GetLiteralKind() 
        { 
            return _tokenizer.LiteralKind; 
        }
            
        public string GetLiteralString() 
        { 
            return _tokenizer.String; 
        }

        public void NextToken() 
        { 
            _tokenizer.NextToken(); 
        }

        // Parse a dictionary
        public void ParseDictionary(Action<string> callback)
        {
            _tokenizer.Skip(Token.OpenBrace);
            ParseDictionaryKeys(key => { callback(key); return true; });
            _tokenizer.Skip(Token.CloseBrace);
        }

        // Parse dictionary keys, calling callback for each one.  Continues until end of input
        // or when callback returns false
        private void ParseDictionaryKeys(Func<string, bool> callback)
        {
            // End?
            while (_tokenizer.CurrentToken != Token.CloseBrace)
            {
                // Parse the key
                string key = null;
                if (_tokenizer.CurrentToken == Token.Identifier && (_options & JsonOptions.StrictParser)==0)
                {
                    key = _tokenizer.String;
                }
                else if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.LiteralKind == LiteralKind.String)
                {
                    key = (string)_tokenizer.LiteralValue;
                }
                else
                {
                    throw new InvalidDataException("syntax error, expected string literal or identifier");
                }
                _tokenizer.NextToken();
                _tokenizer.Skip(Token.Colon);

                // Remember current position
                var pos = _tokenizer.CurrentTokenPosition;

                // Call the callback, quit if cancelled
                _contextStack.Add(key);
                bool doDefaultProcessing = callback(key);
                _contextStack.RemoveAt(_contextStack.Count-1);
                if (!doDefaultProcessing)
                    return;

                // If the callback didn't read anything from the tokenizer, then skip it ourself
                if (pos.Line == _tokenizer.CurrentTokenPosition.Line && pos.Offset == _tokenizer.CurrentTokenPosition.Offset)
                {
                    Parse(typeof(object));
                }

                // Separating/trailing comma
                if (_tokenizer.SkipIf(Token.Comma))
                {
                    if ((_options & JsonOptions.StrictParser) != 0 && _tokenizer.CurrentToken == Token.CloseBrace)
                    {
                        throw new InvalidDataException("Trailing commas not allowed in strict mode");
                    }
                    continue;
                }

                // End
                break;
            }
        }

        // Parse an array
        public void ParseArray(Action callback)
        {
            _tokenizer.Skip(Token.OpenSquare);

            int index = 0;
            while (_tokenizer.CurrentToken != Token.CloseSquare)
            {
                _contextStack.Add(string.Format("[{0}]", index));
                callback();
                _contextStack.RemoveAt(_contextStack.Count-1);

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

        // Yikes!
        public static Func<Type, Action<IJsonReader, object>> _intoParserResolver;
        public static Func<Type, Func<IJsonReader, Type, object>> _parserResolver;
        public static ThreadSafeCache<Type, Func<IJsonReader, Type, object>> _parsers = new ThreadSafeCache<Type, Func<IJsonReader, Type, object>>();
        public static ThreadSafeCache<Type, Action<IJsonReader, object>> _intoParsers = new ThreadSafeCache<Type, Action<IJsonReader, object>>();
        public static ThreadSafeCache<Type, Func<IJsonReader, string, object>> _typeFactories = new ThreadSafeCache<Type, Func<IJsonReader, string, object>>();
    }
}
