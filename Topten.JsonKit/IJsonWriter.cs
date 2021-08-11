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
using System.Reflection;


namespace Topten.JsonKit
{
    /// <summary>
    /// Writes to a JSON output stream
    /// </summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public interface IJsonWriter
    {
        /// <summary>
        /// Writes a string literal
        /// </summary>
        /// <param name="str">The string to write</param>
        void WriteStringLiteral(string str);

        /// <summary>
        /// Write raw characters to the output stream
        /// </summary>
        /// <param name="str">The string to write</param>
        void WriteRaw(string str);

        /// <summary>
        /// Writes array delimeters to the output stream
        /// </summary>
        /// <param name="callback">A callback that should write the array elements</param>
        void WriteArray(Action callback);

        /// <summary>
        /// Writes dictionary delimeters to the output stream
        /// </summary>
        /// <param name="callback">A callback that should write the dictionary keys and values</param>
        void WriteDictionary(Action callback);

        /// <summary>
        /// Writes a value to the output stream
        /// </summary>
        /// <param name="value">The value to write</param>
        void WriteValue(object value);

        /// <summary>
        /// Writes the separator between array elements
        /// </summary>
        void WriteElement();

        /// <summary>
        /// Writes a dictionary key to the output stream
        /// </summary>
        void WriteKey(string key);

        /// <summary>
        /// Writes a dictionary key to the output stream without escaping the key
        /// </summary>
        void WriteKeyNoEscaping(string key);
    }
}
