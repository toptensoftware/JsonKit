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
    /// Used to decorate fields and properties that should be serialized
    /// </summary>
    /// <remarks>
    /// - [Json] on class or struct causes all public fields and properties to be serialized
    /// - [Json] on a public or non-public field or property causes that member to be serialized
    /// - [JsonExclude] on a field or property causes that field to be not serialized
    /// - A class or struct with no [Json] attribute has all public fields/properties serialized
    /// - A class or struct with no [Json] attribute but a [Json] attribute on one or more members only serializes those members
    ///
    /// Use [Json("keyname")] to explicitly specify the key to be used 
    /// [Json] without the keyname will be serialized using the name of the member with the first letter lowercased.
    ///
    /// [Json(KeepInstance=true)] causes container/subobject types to be serialized into the existing member instance (if not null)
    ///
    /// You can also use the system supplied DataContract and DataMember attributes.  They'll only be used if there
    /// are no JsonKit attributes on the class or it's members. You must specify DataContract on the type and
    /// DataMember on any fields/properties that require serialization.  There's no need for exclude attribute.
    /// When using DataMember, the name of the field or property is used as is - the first letter is left in upper case
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
    public class JsonAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new Json attribute
        /// </summary>
        public JsonAttribute()
        {
            Key = null;
        }

        /// <summary>
        /// Constructs a new Json attribute, setting the key used for serialization of this item
        /// </summary>
        /// <param name="key"></param>
        public JsonAttribute(string key)
        {
            Key = key;
        }


        /// <summary>
        /// Gets the serialization key for this Json item
        /// </summary>
        public string Key
        {
            get;
            private set;
        }

        /// <summary>
        /// Controls whether a collection is serialized by creating and assigning a new
        /// instance, or by clearing and serializing into the existing instance.
        /// </summary>
        /// <remarks>
        /// If true uses ParseInto to parse into the existing object instance
        /// If false, creates a new instance as assigns it to the property
        /// </remarks>
        public bool KeepInstance
        {
            get;
            set;
        }

        /// <summary>
        /// Used to decorate deprecated properties that should be loaded
        /// but not saved.
        /// </summary>
        /// <remarks>
        /// If true, the property will be loaded, but not saved
        /// Use to upgrade deprecated persisted settings, but not
        /// write them back out again
        /// </remarks>
        public bool Deprecated
        {
            get;
            set;
        }

        /// <summary>
        /// For non-value types controls whether null values should be
        /// written as null, or excluded for output.
        /// </summary>
        public bool ExcludeIfNull
        {
            get;
            set;
        }

        /// <summary>
        /// For collection types controls whether they should be serialized
        /// if the collection is empty or null
        /// </summary>
        public bool ExcludeIfEmpty
        {
            get;
            set;
        }
        
        /// <summary>
        /// Used to simple value types that match a specific value
        /// </summary>
        public object ExcludeIfEquals
        {
            get;
            set;
        }

    }
}
