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

namespace Topten.JsonKit
{
    /// <summary>
    /// Options to controls formatting/parsing and write behaviour
    /// </summary>
    [Flags]
    public enum JsonOptions
    {
        /// <summary>
        /// No special options
        /// </summary>
        None = 0,

        /// <summary>
        /// Force writing of whitespace (pretty print)
        /// </summary>
        WriteWhitespace  = 0x00000001,

        /// <summary>
        /// Disable writing of whitespace
        /// </summary>
        DontWriteWhitespace = 0x00000002,

        /// <summary>
        /// Enforce strict parsing (comments, array commas etc..)
        /// </summary>
        StrictParser = 0x00000004,

        /// <summary>
        /// Relax some parsing rules
        /// </summary>
        NonStrictParser = 0x00000008,

        /// <summary>
        /// Flush the textwriter stream when finished writing
        /// </summary>
        Flush = 0x00000010,

        /// <summary>
        /// Use the Json.SavePreviousVersion property to control saving previous versions
        /// </summary>
        AutoSavePreviousVersion = 0x00000020,

        /// <summary>
        /// Always save a previous version
        /// </summary>
        SavePreviousVersion = 0x00000040,           
    }
}
