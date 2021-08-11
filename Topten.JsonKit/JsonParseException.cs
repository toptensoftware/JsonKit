﻿// JsonKit v0.5 - A simple but flexible Json library in a single .cs file.
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

namespace Topten.JsonKit
{
    /// <summary>
    /// Exception thrown for any parse error 
    /// </summary>
    public class JsonParseException : Exception
    {
        /// <summary>
        /// Constructs a new JsonParseException
        /// </summary>
        /// <param name="inner">The inner exception</param>
        /// <param name="context">A string describing the context of the serialization (parent key path)</param>
        /// <param name="position">The position in the JSON stream where the error occured</param>
        public JsonParseException(Exception inner, string context, LineOffset position) : 
            base(string.Format("JSON parse error at {0}{1} - {2}", position, string.IsNullOrEmpty(context) ? "" : string.Format(", context {0}", context), inner.Message), inner)
        {
            Position = position;
            Context = context;
        }

        /// <summary>
        /// The position in the JSON stream where the error occured
        /// </summary>
        public LineOffset Position { get; private set; }

        /// <summary>
        /// A string describing the context of the serialization (parent key path)
        /// </summary>
        public string Context { get; private set; }
    }
}
