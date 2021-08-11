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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;



namespace Topten.JsonKit
{
    // Stores reflection info about a type
    class ReflectionInfo
    {
        // List of members to be serialized
        public List<JsonMemberInfo> Members;

        // Cache of these ReflectionInfos's
        static ThreadSafeCache<Type, ReflectionInfo> _cache = new ThreadSafeCache<Type, ReflectionInfo>();

        public static MethodInfo FindFormatJson(Type type)
        {
            if (type.IsValueType)
            {
                // Try `void FormatJson(IJsonWriter)`
                var formatJson = type.GetMethod("FormatJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(IJsonWriter) }, null);
                if (formatJson != null && formatJson.ReturnType == typeof(void))
                    return formatJson;

                // Try `string FormatJson()`
                formatJson = type.GetMethod("FormatJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
                if (formatJson != null && formatJson.ReturnType == typeof(string))
                    return formatJson;
            }
            return null;
        }

        public static MethodInfo FindParseJson(Type type)
        {
            // Try `T ParseJson(IJsonReader)`
            var parseJson = type.GetMethod("ParseJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(IJsonReader) }, null);
            if (parseJson != null && parseJson.ReturnType == type)
                return parseJson;

            // Try `T ParseJson(string)`
            parseJson = type.GetMethod("ParseJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
            if (parseJson != null && parseJson.ReturnType == type)
                return parseJson;

            return null;
        }

        // Write one of these types
        public void Write(IJsonWriter w, object val)
        {
            w.WriteDictionary(() =>
            {
                var writing = val as IJsonWriting;
                if (writing != null)
                    writing.OnJsonWriting(w);

                foreach (var jmi in Members.Where(x=>!x.Deprecated))
                {
                    // Exclude null?
                    var mval = jmi.GetValue(val);
                    if (mval == null && jmi.ExcludeIfNull)
                        continue;

                    w.WriteKeyNoEscaping(jmi.JsonKey);
                    w.WriteValue(mval);
                }

                var written = val as IJsonWritten;
                if (written != null)
                    written.OnJsonWritten(w);
            });
        }

        // Read one of these types.
        // NB: Although JsonKit.JsonParseInto only works on reference type, when using reflection
        //     it also works for value types so we use the one method for both
        public void ParseInto(IJsonReader r, object into)
        {
            var loading = into as IJsonLoading;
            if (loading != null)
                loading.OnJsonLoading(r);

            r.ParseDictionary(key =>
            {
                ParseFieldOrProperty(r, into, key);
            });

            var loaded = into as IJsonLoaded;
            if (loaded != null)
                loaded.OnJsonLoaded(r);
        }

        // The member info is stored in a list (as opposed to a dictionary) so that
        // the json is written in the same order as the fields/properties are defined
        // On loading, we assume the fields will be in the same order, but need to
        // handle if they're not.  This function performs a linear search, but
        // starts after the last found item as an optimization that should work
        // most of the time.
        int _lastFoundIndex = 0;
        bool FindMemberInfo(string name, out JsonMemberInfo found)
        {
            for (int i = 0; i < Members.Count; i++)
            {
                int index = (i + _lastFoundIndex) % Members.Count;
                var jmi = Members[index];
                if (jmi.JsonKey == name)
                {
                    _lastFoundIndex = index;
                    found = jmi;
                    return true;
                }
            }
            found = null;
            return false;
        }

        // Parse a value from IJsonReader into an object instance
        public void ParseFieldOrProperty(IJsonReader r, object into, string key)
        {
            // IJsonLoadField
            var lf = into as IJsonLoadField;
            if (lf != null && lf.OnJsonField(r, key))
                return;

            // Find member
            JsonMemberInfo jmi;
            if (FindMemberInfo(key, out jmi))
            {
                // Try to keep existing instance
                if (jmi.KeepInstance)
                {
                    var subInto = jmi.GetValue(into);
                    if (subInto != null)
                    {
                        r.ParseInto(subInto);
                        return;
                    }
                }

                // Parse and set
                var val = r.Parse(jmi.MemberType);
                jmi.SetValue(into, val);
                return;
            }
        }

        // Get the reflection info for a specified type
        public static ReflectionInfo GetReflectionInfo(Type type)
        {
            // Check cache
            return _cache.Get(type, () =>
            {
                var allMembers = Utils.GetAllFieldsAndProperties(type); 

                // Does type have a [Json] attribute
                bool typeMarked = type.GetCustomAttributes(typeof(JsonAttribute), true).OfType<JsonAttribute>().Any();

                // Do any members have a [Json] attribute
                bool anyFieldsMarked = allMembers.Any(x => x.GetCustomAttributes(typeof(JsonAttribute), false).OfType<JsonAttribute>().Any());

                // Try with DataContract and friends
                if (!typeMarked && !anyFieldsMarked && type.GetCustomAttributes(typeof(DataContractAttribute), true).OfType<DataContractAttribute>().Any())
                {
                    var ri = CreateReflectionInfo(type, mi =>
                    {
                        // Get attributes
                        var attr = mi.GetCustomAttributes(typeof(DataMemberAttribute), false).OfType<DataMemberAttribute>().FirstOrDefault();
                        if (attr != null)
                        {
                            return new JsonMemberInfo()
                            {
                                Member = mi,
                                JsonKey = attr.Name ?? mi.Name,     // No lower case first letter if using DataContract/Member
                            };
                        }

                        return null;
                    });

                    ri.Members.Sort((a, b) => String.CompareOrdinal(a.JsonKey, b.JsonKey));    // Match DataContractJsonSerializer
                    return ri;
                }

                {
                    // Should we serialize all public methods?
                    bool serializeAllPublics = typeMarked || !anyFieldsMarked;

                    // Build 
                    var ri = CreateReflectionInfo(type, mi =>
                    {
                        // Explicitly excluded?
                        if (mi.GetCustomAttributes(typeof(JsonExcludeAttribute), false).Any())
                            return null;

                        // Get attributes
                        var attr = mi.GetCustomAttributes(typeof(JsonAttribute), false).OfType<JsonAttribute>().FirstOrDefault();
                        if (attr != null)
                        {
                            return new JsonMemberInfo()
                            {
                                Member = mi,
                                JsonKey = attr.Key ?? mi.Name.Substring(0, 1).ToLower() + mi.Name.Substring(1),
                                Attribute = attr,
                            };
                        }

                        // Serialize all publics?
                        if (serializeAllPublics && Utils.IsPublic(mi))
                        {
                            return new JsonMemberInfo()
                            {
                                Member = mi,
                                JsonKey = mi.Name.Substring(0, 1).ToLower() + mi.Name.Substring(1),
                            };
                        }

                        return null;
                    });
                    return ri;
                }
            });
        }

        public static ReflectionInfo CreateReflectionInfo(Type type, Func<MemberInfo, JsonMemberInfo> callback)
        {
            // Work out properties and fields
            var members = Utils.GetAllFieldsAndProperties(type).Select(x => callback(x)).Where(x => x != null).ToList();

            // Anything with KeepInstance must be a reference type
            var invalid = members.FirstOrDefault(x => x.KeepInstance && x.MemberType.IsValueType);
            if (invalid!=null)
            {
                throw new InvalidOperationException(string.Format("KeepInstance=true can only be applied to reference types ({0}.{1})", type.FullName, invalid.Member));
            }

            // Must have some members
            if (!members.Any() && !Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                return null;

            // Create reflection info
            return new ReflectionInfo() { Members = members };
        }
    }
}
