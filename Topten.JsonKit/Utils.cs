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
using System.Reflection;
using System.Collections;

namespace Topten.JsonKit
{
    static class Utils
    {
        // Get all fields and properties of a type
        public static IEnumerable<MemberInfo> GetAllFieldsAndProperties(Type t)
        {
            if (t == null)
                return Enumerable.Empty<FieldInfo>();

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            return t.GetMembers(flags).Where(x => x is FieldInfo || x is PropertyInfo).Concat(GetAllFieldsAndProperties(t.BaseType));
        }

        public static Type FindGenericInterface(Type type, Type tItf)
        {
            foreach (var t in type.GetInterfaces())
            {
                // Is this a generic list?
                if (t.IsGenericType && t.GetGenericTypeDefinition() == tItf)
                    return t;
            }

            return null;
        }

        public static bool IsPublic(MemberInfo mi)
        {
            // Public field
            var fi = mi as FieldInfo;
            if (fi != null)
                return fi.IsPublic;

            // Public property
            // (We only check the get method so we can work with anonymous types)
            var pi = mi as PropertyInfo;
            if (pi != null)
            {
                var gm = pi.GetGetMethod(true);
                return (gm != null && gm.IsPublic);
            }

            return false;
        }

        public static Type ResolveInterfaceToClass(Type tItf)
        {
            // Generic type
            if (tItf.IsGenericType)
            {
                var genDef = tItf.GetGenericTypeDefinition();

                // IList<> -> List<>
                if (genDef == typeof(IList<>))
                {
                    return typeof(List<>).MakeGenericType(tItf.GetGenericArguments());
                }

                // IDictionary<string,> -> Dictionary<string,>
                if (genDef == typeof(IDictionary<,>) && tItf.GetGenericArguments()[0] == typeof(string))
                {
                    return typeof(Dictionary<,>).MakeGenericType(tItf.GetGenericArguments());
                }
            }

            // IEnumerable -> List<object>
            if (tItf == typeof(IEnumerable))
                return typeof(List<object>);

            // IDicitonary -> Dictionary<string,object>
            if (tItf == typeof(IDictionary))
                return typeof(Dictionary<string, object>);
            return tItf;
        }

        public static long ToUnixMilliseconds(DateTime This)
        {
            return (long)This.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        public static DateTime FromUnixMilliseconds(long timeStamp)
        {
            return new DateTime(1970, 1, 1).AddMilliseconds(timeStamp);
        }

        public static void DeleteFile(string filename)
        {
            try
            {
                System.IO.File.Delete(filename);
            }
            catch
            {
                // Don't care
            }
        }

    }
}
