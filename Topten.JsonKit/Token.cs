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

using System.Reflection;


namespace Topten.JsonKit
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum Token
    {
        /// <summary>End of file</summary>
        EOF,

        /// <summary>An identifier</summary>
        Identifier,

        /// <summary>A literal value</summary>
        Literal,

        /// <summary>An open brace '{'</summary>
        OpenBrace,

        /// <summary>A closing brace '}'</summary>
        CloseBrace,

        /// <summary>An open square bracket '['</summary>
        OpenSquare,

        /// <summary>A closing square bracket ']'</summary>
        CloseSquare,

        /// <summary>An equal sign '='</summary>
        Equal,

        /// <summary>A colon ':'</summary>
        Colon,

        /// <summary>A semi-colon ';'</summary>
        SemiColon,

        /// <summary>A comma ','</summary>
        Comma,
    }
}
