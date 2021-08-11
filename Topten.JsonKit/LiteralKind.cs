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

namespace Topten.JsonKit
{
    /// <summary>
    /// Describes the current literal in the json stream
    /// </summary>
    public enum LiteralKind
    {
        /// <summary>
        /// Not currently on a literal
        /// </summary>
        None,

        /// <summary>
        /// A string literal
        /// </summary>
        String,

        /// <summary>
        /// The value `null`
        /// </summary>
        Null,

        /// <summary>
        /// The value `true`
        /// </summary>
        True,

        /// <summary>
        /// The value `false`
        /// </summary>
        False,

        /// <summary>
        /// A signed integer
        /// </summary>
        SignedInteger,

        /// <summary>
        /// An unsigned integer
        /// </summary>
        UnsignedInteger,

        /// <summary>
        /// A floating point value
        /// </summary>
        FloatingPoint,
    }
}
