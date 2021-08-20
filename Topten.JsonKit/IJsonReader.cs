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
using System.Globalization;
using System.IO;
using System.Reflection;


namespace Topten.JsonKit
{
    /// <summary>
    /// Provides a reader for JSON data
    /// </summary>
    [Obfuscation(Exclude=true, ApplyToMembers=true)]
    public interface IJsonReader
    {
        /// <summary>
        /// Parses an object of specified type from the JSON stream
        /// </summary>
        /// <param name="type">The type to be parsed</param>
        /// <returns>A reference to the loaded instance</returns>
        object Parse(Type type);

        /// <summary>
        /// Parses an object of specified type from the JSON stream
        /// </summary>
        /// <typeparam name="T">The type to be parsed</typeparam>
        /// <returns>A reference to the loaded instance</returns>
        T Parse<T>();

        /// <summary>
        /// Parses from a JSON stream into an existing object instance
        /// </summary>
        /// <param name="into">The target object</param>
        void ParseInto(object into);

        /// <summary>
        /// The current token in the input JSON stream
        /// </summary>
        Token CurrentToken { get; }

        /// <summary>
        /// Reads a literal value from the JSON stream
        /// </summary>
        /// <param name="converter">A converter function to convert the value</param>
        /// <returns>The parsed and converted value</returns>
        object ReadLiteral(Func<object, object> converter);

        /// <summary>
        /// Reads a dictinary from the input stream
        /// </summary>
        /// <param name="callback">A callback that will be invoked for each encountered dictionary key</param>
        void ParseDictionary(Action<string> callback);

        /// <summary>
        /// Reads an array from the input stream
        /// </summary>
        /// <param name="callback">A callback that will be invoked as each array element is encounters</param>
        void ParseArray(Action callback);

        /// <summary>
        /// Gets the literal kind of the current stream token
        /// </summary>
        /// <returns></returns>
        LiteralKind GetLiteralKind();

        /// <summary>
        /// Gets a string literal from the JSON stream
        /// </summary>
        /// <returns></returns>
        string GetLiteralString();

        /// <summary>
        /// Moves to the next token in the input stream
        /// </summary>
        void NextToken();
    }

    /// <summary>
    /// Helper functions for working with IJsonReader
    /// </summary>
    public static class IJsonReaderExtensions
    {
        /// <summary>
        /// Read a literal number
        /// </summary>
        /// <typeparam name="T">The number type</typeparam>
        /// <param name="reader">The reader to read from</param>
        /// <returns>A number of specified type, or throws an InvalidDataException</returns>
        public static T ReadLiteralNumber<T>(this IJsonReader reader)
        {
            return (T)ReadLiteralNumber(reader, typeof(T));
        }

        /// <summary>
        /// Read a literal number
        /// </summary>
        /// <param name="reader">The reader to read from</param>
        /// <param name="type">The number type to return</param>
        /// <returns>A number of specified type, or throws an InvalidDataException</returns>
        public static object ReadLiteralNumber(this IJsonReader reader, Type type)
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
        }
    }
}
