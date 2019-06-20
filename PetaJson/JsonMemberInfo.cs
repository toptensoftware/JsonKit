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
using System.Reflection;


namespace PetaJson
{
    // Information about a field or property found through reflection
    class JsonMemberInfo
    {
        // The Json key for this member
        public string JsonKey;

        // True if should keep existing instance (reference types only)
        public bool KeepInstance;

        // True if deprecated
        public bool Deprecated;

        // Reflected member info
        MemberInfo _mi;
        public MemberInfo Member
        {
            get { return _mi; }
            set
            {
                // Store it
                _mi = value;

                // Also create getters and setters
                if (_mi is PropertyInfo)
                {
                    GetValue = (obj) => ((PropertyInfo)_mi).GetValue(obj, null);
                    SetValue = (obj, val) => ((PropertyInfo)_mi).SetValue(obj, val, null);
                }
                else
                {
                    GetValue = ((FieldInfo)_mi).GetValue;
                    SetValue = ((FieldInfo)_mi).SetValue;
                }
            }
        }

        // Member type
        public Type MemberType
        {
            get
            {
                if (Member is PropertyInfo)
                {
                    return ((PropertyInfo)Member).PropertyType;
                }
                else
                {
                    return ((FieldInfo)Member).FieldType;
                }
            }
        }

        // Get/set helpers
        public Action<object, object> SetValue;
        public Func<object, object> GetValue;
    }
}
