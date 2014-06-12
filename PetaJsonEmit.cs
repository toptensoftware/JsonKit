using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;
using System.Globalization;
using System.IO;

namespace PetaJson
{
    public static class JsonEmit
    {
        public static void Init()
        {
            Json.SetTypeFormatterResolver(Internal.Emit.MakeFormatter);
            Json.SetTypeIntoParserResolver(Internal.Emit.MakeIntoParser);
        }
    }

    namespace Internal
    {
        static class Emit
        {
            public static Action<IJsonWriter, object> MakeFormatter(Type type)
            {
                // Get the reflection info for this type
                var ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri == null)
                    return null;

                // Create a dynamic method that can do the work
                var method = new DynamicMethod("dynamic_formatter", null, new Type[] { typeof(IJsonWriter), typeof(object) }, true);
                var il = method.GetILGenerator();

                // Cast/unbox the target object and store in local variable
                var locTypedObj = il.DeclareLocal(type);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(type.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, type);
                il.Emit(OpCodes.Stloc, locTypedObj);

                // Get Invariant CultureInfo
                var locInvariant = il.DeclareLocal(typeof(IFormatProvider));
                il.Emit(OpCodes.Call, typeof(CultureInfo).GetProperty("InvariantCulture").GetGetMethod());
                il.Emit(OpCodes.Stloc, locInvariant);

                // These are the types we'll call .ToString(Culture.InvariantCulture) on
                var toStringTypes = new Type[] { 
                    typeof(int), typeof(uint), typeof(long), typeof(ulong), 
                    typeof(short), typeof(ushort), typeof(decimal), 
                    typeof(byte), typeof(sbyte)
                };

                // Theses types we also generate for
                var otherSupportedTypes = new Type[] {
                    typeof(double), typeof(float), typeof(string), typeof(char), typeof(bool)
                };

                // Call IJsonWriting if implemented
                if (typeof(IJsonWriting).IsAssignableFrom(type))
                {
                    il.Emit(OpCodes.Ldloc, locTypedObj);
                    if (type.IsValueType)
                        il.Emit(OpCodes.Box, type);
                    il.Emit(OpCodes.Castclass, typeof(IJsonWriting));
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Callvirt, typeof(IJsonWriting).GetMethod("OnJsonWriting", new Type[] { typeof(IJsonWriter) }));
                }

                // Process all members
                foreach (var m in ri.Members)
                {
                    // Ignore write only properties
                    var pi = m.Member as PropertyInfo;
                    if (pi!=null && pi.GetGetMethod() == null)
                    {
                        continue;
                    }

                    // Write the Json key
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, m.JsonKey);
                    il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteKeyNoEscaping", new Type[] { typeof(string) }));

                    // Load the writer
                    il.Emit(OpCodes.Ldarg_0);

                    // Get the member type
                    var memberType = m.MemberType;

                    // Load the target object
                    if (type.IsValueType)
                    {
                        il.Emit(OpCodes.Ldloca, locTypedObj);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldloc, locTypedObj);
                    }

                    // Work out if we need to the value or it's address on the stack
                    bool NeedValueAddress = (memberType.IsValueType && (toStringTypes.Contains(memberType) || otherSupportedTypes.Contains(memberType)));
                    if (Nullable.GetUnderlyingType(memberType) != null)
                    {
                        NeedValueAddress = true;
                    }

                    // Property?
                    if (pi != null)
                    {
                        // Call property's get method
                        if (type.IsValueType)
                            il.Emit(OpCodes.Call, pi.GetGetMethod());
                        else
                            il.Emit(OpCodes.Callvirt, pi.GetGetMethod());

                        // If we need the address then store in a local and take it's address
                        if (NeedValueAddress)
                        {
                            var locTemp = il.DeclareLocal(memberType);
                            il.Emit(OpCodes.Stloc, locTemp);
                            il.Emit(OpCodes.Ldloca, locTemp);
                        }
                    }

                    // Field?
                    var fi = m.Member as FieldInfo;
                    if (fi != null)
                    {
                        if (NeedValueAddress)
                        {
                            il.Emit(OpCodes.Ldflda, fi);
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldfld, fi);
                        }
                    }

                    Label? lblFinished = null;

                    // Is it a nullable type?
                    var typeUnderlying = Nullable.GetUnderlyingType(memberType);
                    if (typeUnderlying != null)
                    {
                        // Duplicate the address so we can call get_HasValue() and then get_Value()
                        il.Emit(OpCodes.Dup);

                        // Define some labels
                        var lblHasValue = il.DefineLabel();
                        lblFinished = il.DefineLabel();

                        // Call has_Value
                        il.Emit(OpCodes.Call, memberType.GetProperty("HasValue").GetGetMethod());
                        il.Emit(OpCodes.Brtrue, lblHasValue);

                        // No value, write "null:
                        il.Emit(OpCodes.Pop);
                        il.Emit(OpCodes.Ldstr, "null");
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteRaw", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Br_S, lblFinished.Value);

                        // Get it's value
                        il.MarkLabel(lblHasValue);
                        il.Emit(OpCodes.Call, memberType.GetProperty("Value").GetGetMethod());

                        // Switch to the underlying type from here on
                        memberType = typeUnderlying;
                        NeedValueAddress = (memberType.IsValueType && (toStringTypes.Contains(memberType) || otherSupportedTypes.Contains(memberType)));

                        // Work out again if we need the address of the value
                        if (NeedValueAddress)
                        {
                            var locTemp = il.DeclareLocal(memberType);
                            il.Emit(OpCodes.Stloc, locTemp);
                            il.Emit(OpCodes.Ldloca, locTemp);
                        }
                    }

                    // ToString()
                    if (toStringTypes.Contains(memberType))
                    {
                        // Convert to string
                        il.Emit(OpCodes.Ldloc, locInvariant);
                        il.Emit(OpCodes.Call, memberType.GetMethod("ToString", new Type[] { typeof(IFormatProvider) }));
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteRaw", new Type[] { typeof(string) }));
                    }

                    // ToString("R")
                    else if (memberType == typeof(float) || memberType == typeof(double))
                    {
                        il.Emit(OpCodes.Ldstr, "R");
                        il.Emit(OpCodes.Ldloc, locInvariant);
                        il.Emit(OpCodes.Call, memberType.GetMethod("ToString", new Type[] { typeof(string), typeof(IFormatProvider) }));
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteRaw", new Type[] { typeof(string) }));
                    }

                    // String?
                    else if (memberType == typeof(string))
                    {
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteStringLiteral", new Type[] { typeof(string) }));
                    }

                    // Char?
                    else if (memberType == typeof(char))
                    {
                        il.Emit(OpCodes.Call, memberType.GetMethod("ToString", new Type[] {  }));
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteStringLiteral", new Type[] { typeof(string) }));
                    }

                    // Bool?
                    else if (memberType == typeof(bool))
                    {
                        var lblTrue = il.DefineLabel();
                        var lblCont = il.DefineLabel();
                        il.Emit(OpCodes.Brtrue_S, lblTrue);
                        il.Emit(OpCodes.Ldstr, "false");
                        il.Emit(OpCodes.Br_S, lblCont);
                        il.MarkLabel(lblTrue);
                        il.Emit(OpCodes.Ldstr, "true");
                        il.MarkLabel(lblCont);
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteRaw", new Type[] { typeof(string) }));
                    }

                    // NB: We don't support DateTime as it's format can be changed

                    else
                    {
                        // Unsupported type, pass through
                        if (memberType.IsValueType)
                        {
                            il.Emit(OpCodes.Box, memberType);
                        }
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteValue", new Type[] { typeof(object) }));
                    }

                    if (lblFinished.HasValue)
                        il.MarkLabel(lblFinished.Value);
                }

                // Call IJsonWritten
                if (typeof(IJsonWritten).IsAssignableFrom(type))
                {
                    il.Emit(OpCodes.Ldloc, locTypedObj);
                    if (type.IsValueType)
                        il.Emit(OpCodes.Box, type);
                    il.Emit(OpCodes.Castclass, typeof(IJsonWritten));
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Callvirt, typeof(IJsonWritten).GetMethod("OnJsonWritten", new Type[] { typeof(IJsonWriter) }));
                }

                // Done!
                il.Emit(OpCodes.Ret);

                // Create delegate to our IL code
                var impl = (Action<IJsonWriter, object>)method.CreateDelegate(typeof(Action<IJsonWriter, object>));

                // Wrap it in a call to WriteDictionary
                return (w, obj) =>
                {
                    w.WriteDictionary(() =>
                    {
                        impl(w, obj);
                    });
                };

            }

            public static Action<IJsonReader, object> MakeIntoParser(Type type)
            {
                // Get the reflection info for this type
                var ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri == null)
                    return null;

                // We'll create setters for each property/field
                var setters = new Dictionary<string, Action<IJsonReader, object>>();

                // These types we'll call <type>.Parse(reader.String) on
                var numericTypes = new Type[] { 
                    typeof(int), typeof(uint), typeof(long), typeof(ulong), 
                    typeof(short), typeof(ushort), typeof(decimal), 
                    typeof(byte), typeof(sbyte), 
                    typeof(double), typeof(float)
                };

                // Process all members
                foreach (var m in ri.Members)
                {
                    // Ignore write only properties
                    var pi = m.Member as PropertyInfo;
                    var fi = m.Member as FieldInfo;
                    if (pi != null && pi.GetSetMethod() == null)
                    {
                        continue;
                    }
                    if (pi != null && pi.GetGetMethod() == null && m.KeepInstance)
                    {
                        continue;
                    }

                    // Create a dynamic method that can do the work
                    var method = new DynamicMethod("dynamic_parser", null, new Type[] { typeof(IJsonReader), typeof(object) }, true);
                    var il = method.GetILGenerator();

                    // Load the target
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(type.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, type);

                    if (m.KeepInstance)
                    {
                        // Get existing existing instance
                        il.Emit(OpCodes.Dup);
                        if (pi != null)
                            il.Emit(OpCodes.Callvirt, pi.GetGetMethod());
                        else
                            il.Emit(OpCodes.Ldfld, fi);

                        var existingInstance = il.DeclareLocal(m.MemberType);
                        var lblExistingInstanceNull = il.DefineLabel();

                        // Keep a copy of the existing instance in a locale
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Stloc, existingInstance);

                        // Compare to null
                        il.Emit(OpCodes.Ldnull);
                        il.Emit(OpCodes.Ceq);
                        il.Emit(OpCodes.Brtrue_S, lblExistingInstanceNull);

                        il.Emit(OpCodes.Ldarg_0);                       // reader
                        il.Emit(OpCodes.Ldloc, existingInstance);       // into
                        il.Emit(OpCodes.Callvirt, typeof(IJsonReader).GetMethod("ParseInto", new Type[] { typeof(Object) }));

                        il.Emit(OpCodes.Pop);       // Clean up target left on stack (1)
                        il.Emit(OpCodes.Ret);

                        il.MarkLabel(lblExistingInstanceNull);
                    }

                    Action<string> callHelper = helperName =>
                    {
                        // check we have a string
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, typeof(Emit).GetMethod(helperName, new Type[] { typeof(IJsonReader) }));

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Callvirt, typeof(IJsonReader).GetMethod("NextToken", new Type[] { }));
                    };

                    if (m.MemberType == typeof(string))
                    {
                        callHelper("GetLiteralString");
                    }

                    else if (m.MemberType == typeof(bool))
                    {
                        callHelper("GetLiteralBool");
                    }

                    else if (m.MemberType == typeof(char))
                    {
                        callHelper("GetLiteralChar");
                    }

                    else if (numericTypes.Contains(m.MemberType))
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, typeof(Emit).GetMethod("GetLiteralNumber", new Type[] { typeof(IJsonReader) }));

                        // Convert to a string
                        il.Emit(OpCodes.Call, typeof(CultureInfo).GetProperty("InvariantCulture").GetGetMethod());
                        il.Emit(OpCodes.Call, m.MemberType.GetMethod("Parse", new Type[] { typeof(string), typeof(IFormatProvider) }));

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Callvirt, typeof(IJsonReader).GetMethod("NextToken", new Type[] { }));
                    }

                    else
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldtoken, m.MemberType);
                        il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", new Type[] { typeof(RuntimeTypeHandle) }));
                        il.Emit(OpCodes.Callvirt, typeof(IJsonReader).GetMethod("Parse", new Type[] { typeof(Type) }));
                        il.Emit(m.MemberType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, m.MemberType);
                    }

                    if (pi != null)
                    {
                        il.Emit(type.IsValueType ? OpCodes.Call : OpCodes.Callvirt, pi.GetSetMethod());
                    }

                    if (fi != null)
                    {
                        il.Emit(OpCodes.Stfld, fi);
                    }

                    il.Emit(OpCodes.Ret);

                    // Store the handler in map
                    setters.Add(m.JsonKey, (Action<IJsonReader, object>)method.CreateDelegate(typeof(Action<IJsonReader, object>)));
                }

                // Now create the parseInto delegate
                bool hasLoading = typeof(IJsonLoading).IsAssignableFrom(type);
                bool hasLoaded = typeof(IJsonLoaded).IsAssignableFrom(type);
                Action<IJsonReader, object> parseInto = (reader, obj) =>
                {
                    if (hasLoading)
                    {
                        ((IJsonLoading)obj).OnJsonLoading(reader);
                    }

                    var lf = obj as IJsonLoadField;

                    reader.ReadDictionary(key =>
                    {
                        if (lf != null)
                        {
                            if (lf.OnJsonField(reader, key))
                                return;
                        }

                        Action<IJsonReader, object> setter;
                        if (setters.TryGetValue(key, out setter))
                        {
                            setter(reader, obj);
                        }
                    });

                    if (hasLoaded)
                    {
                        ((IJsonLoaded)obj).OnJsonLoaded(reader);
                    }
                };

                // While we're at it, we might as well create a direct type converter too
                RegisterParser(type, parseInto);

                // Done
                return parseInto;
            }

            static void RegisterParser(Type type, Action<IJsonReader, object> parseInto)
            {
                // Create a dynamic method that can do the work
                var method = new DynamicMethod("dynamic_factory", typeof(object), new Type[] { typeof(IJsonReader), typeof(Action<IJsonReader, object>)}, true);
                var il = method.GetILGenerator();

                // Create the new object
                var locObj = il.DeclareLocal(typeof(object));
                if (type.IsValueType)
                {
                    // Create boxed type
                    var locTempStruct = il.DeclareLocal(type);
                    il.Emit(OpCodes.Ldloca, locTempStruct);
                    il.Emit(OpCodes.Initobj);
                    il.Emit(OpCodes.Ldloc, locTempStruct);
                    il.Emit(OpCodes.Box, type);
                }
                else
                {
                    il.Emit(OpCodes.Newobj, type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[0], null));
                }

                il.Emit(OpCodes.Dup);               // For return value

                il.Emit(OpCodes.Stloc, locObj);

                il.Emit(OpCodes.Ldarg_1);           // parseinto delegate
                il.Emit(OpCodes.Ldarg_0);           // IJsonReader
                il.Emit(OpCodes.Ldloc, locObj);     // new object instance
                il.Emit(OpCodes.Callvirt, typeof(Action<IJsonReader, object>).GetMethod("Invoke"));
                il.Emit(OpCodes.Ret);

                var factory = (Func<IJsonReader, Action<IJsonReader,object>, object>)method.CreateDelegate(typeof(Func<IJsonReader, Action<IJsonReader, object>, object>));

                Json.RegisterParser(type, (reader, type2) =>
                {
                    return factory(reader, parseInto);
                });
            }

            public static bool GetLiteralBool(IJsonReader r)
            {
                switch (r.GetLiteralKind())
                {
                    case LiteralKind.True:
                        return true;

                    case LiteralKind.False:
                        return false;

                    default:
                        throw new InvalidDataException("expected a boolean value");
                }
            }

            public static char GetLiteralChar(IJsonReader r)
            {
                if (r.GetLiteralKind() != LiteralKind.String)
                    throw new InvalidDataException("expected a single character string literal");
                var str = r.GetLiteralString();
                if (str==null || str.Length!=1)
                    throw new InvalidDataException("expected a single character string literal");

                return str[0];
            }

            public static string GetLiteralString(IJsonReader r)
            {
                if (r.GetLiteralKind() != LiteralKind.String)
                    throw new InvalidDataException("expected a string literal");
                return r.GetLiteralString();
            }

            public static string GetLiteralNumber(IJsonReader r)
            {
                switch (r.GetLiteralKind())
                {
                    case LiteralKind.SignedInteger:
                    case LiteralKind.UnsignedInteger:
                    case LiteralKind.FloatingPoint:
                        return r.GetLiteralString();
                }
                throw new InvalidDataException("expected a numeric literal");
            }

        }
    }
}
