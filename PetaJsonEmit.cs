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
            Json.SetFormatterResolver(Internal.Emit.MakeFormatter);
            Json.SetParserResolver(Internal.Emit.MakeParser);
            Json.SetIntoParserResolver(Internal.Emit.MakeIntoParser);
        }
    }

    namespace Internal
    {
        static class Emit
        {

            // Generates a function that when passed an object of specified type, renders it to an IJsonReader
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

                // Get Invariant CultureInfo (since we'll probably be needing this)
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
                    if (type.IsValueType)
                    {
                        il.Emit(OpCodes.Ldloca, locTypedObj);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, type.GetInterfaceMap(typeof(IJsonWriting)).TargetMethods[0]);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldloc, locTypedObj);
                        il.Emit(OpCodes.Castclass, typeof(IJsonWriting));
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriting).GetMethod("OnJsonWriting", new Type[] { typeof(IJsonWriter) }));
                    }
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

                    // Work out if we need the value or it's address on the stack
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
                    if (type.IsValueType)
                    {
                        il.Emit(OpCodes.Ldloca, locTypedObj);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, type.GetInterfaceMap(typeof(IJsonWritten)).TargetMethods[0]);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldloc, locTypedObj);
                        il.Emit(OpCodes.Castclass, typeof(IJsonWriting));
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriting).GetMethod("OnJsonWritten", new Type[] { typeof(IJsonWriter) }));
                    }
                }

                // Done!
                il.Emit(OpCodes.Ret);
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

            // Pseudo box lets us pass a value type by reference.  Used during 
            // deserialization of value types.
            interface IPseudoBox
            {
                object GetValue();
            }
            class PseudoBox<T> : IPseudoBox where T : struct
            {
                public T value;

                object IPseudoBox.GetValue()
                {
                    return value;
                }
            }


            // Make a parser for value types
            public static Func<IJsonReader, Type, object> MakeParser(Type type)
            {
                System.Diagnostics.Debug.Assert(type.IsValueType);

                // Get the reflection info for this type
                var ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri == null)
                    return null;

                // We'll create setters for each property/field
                var setters = new Dictionary<string, Action<IJsonReader, object>>();

                // Store the value in a pseudo box until it's fully initialized
                var boxType = typeof(PseudoBox<>).MakeGenericType(type);

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
                    
                    // Create a dynamic method that can do the work
                    var method = new DynamicMethod("dynamic_parser", null, new Type[] { typeof(IJsonReader), typeof(object) }, true);
                    var il = method.GetILGenerator();

                    // Load the target
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Castclass, boxType);
                    il.Emit(OpCodes.Ldflda, boxType.GetField("value"));

                    // Get the value
                    GenerateGetJsonValue(m, il);

                    // Assign it
                    if (pi != null)
                        il.Emit(OpCodes.Call, pi.GetSetMethod());
                    if (fi != null)
                        il.Emit(OpCodes.Stfld, fi);

                    // Done
                    il.Emit(OpCodes.Ret);

                    // Store in the map of setters
                    setters.Add(m.JsonKey, (Action<IJsonReader, object>)method.CreateDelegate(typeof(Action<IJsonReader, object>)));
                }

                // Create helpers to invoke the interfaces (this is painful but avoids having to really box 
                // the value in order to call the interface).
                Action<object, IJsonReader> invokeLoading = MakeInterfaceCall(type, typeof(IJsonLoading));
                Action<object, IJsonReader> invokeLoaded = MakeInterfaceCall(type, typeof(IJsonLoaded));
                Func<object, IJsonReader, string, bool> invokeField = MakeLoadFieldCall(type);

                // Create the parser
                Func<IJsonReader, Type, object> parser = (reader, Type) =>
                {
                    // Create pseudobox (ie: new PseudoBox<Type>)
                    var box = Activator.CreateInstance(boxType);

                    // Call IJsonLoading
                    if (invokeLoading != null)
                        invokeLoading(box, reader);

                    // Read the dictionary
                    reader.ParseDictionary(key =>
                    {
                        // Call IJsonLoadField
                        if (invokeField != null && invokeField(box, reader, key))
                            return;

                        // Get a setter and invoke it if found
                        Action<IJsonReader, object> setter;
                        if (setters.TryGetValue(key, out setter))
                        {
                            setter(reader, box);
                        }
                    });

                    // IJsonLoaded
                    if (invokeLoaded != null)
                        invokeLoaded(box, reader);

                    // Return the value
                    return ((IPseudoBox)box).GetValue();
                };

                // Done
                return parser;
            }

            // Helper to make the call to a PsuedoBox value's IJsonLoading or IJsonLoaded
            static Action<object, IJsonReader> MakeInterfaceCall(Type type, Type tItf)
            {
                // Interface supported?
                if (!tItf.IsAssignableFrom(type))
                    return null;

                // Resolve the box type
                var boxType = typeof(PseudoBox<>).MakeGenericType(type);

                // Create method
                var method = new DynamicMethod("dynamic_invoke_" + tItf.Name, null, new Type[] { typeof(object), typeof(IJsonReader) }, true);
                var il = method.GetILGenerator();

                // Call interface method
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, boxType);
                il.Emit(OpCodes.Ldflda, boxType.GetField("value"));
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, type.GetInterfaceMap(tItf).TargetMethods[0]);
                il.Emit(OpCodes.Ret);

                // Done
                return (Action<object, IJsonReader>)method.CreateDelegate(typeof(Action<object, IJsonReader>));
            }

            // Similar to above but for IJsonLoadField
            static Func<object, IJsonReader, string, bool> MakeLoadFieldCall(Type type)
            {
                // Interface supported?
                var tItf = typeof(IJsonLoadField);
                if (!tItf.IsAssignableFrom(type))
                    return null;

                // Resolve the box type
                var boxType = typeof(PseudoBox<>).MakeGenericType(type);

                // Create method
                var method = new DynamicMethod("dynamic_invoke_" + tItf.Name, typeof(bool), new Type[] { typeof(object), typeof(IJsonReader), typeof(string) }, true);
                var il = method.GetILGenerator();

                // Call interface method
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, boxType);
                il.Emit(OpCodes.Ldflda, boxType.GetField("value"));
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Call, type.GetInterfaceMap(tItf).TargetMethods[0]);
                il.Emit(OpCodes.Ret);

                // Done
                return (Func<object, IJsonReader, string, bool>)method.CreateDelegate(typeof(Func<object, IJsonReader, string, bool>));
            }

            // Create an "into parser" that can parse from IJsonReader into a reference type (ie: a class)
            public static Action<IJsonReader, object> MakeIntoParser(Type type)
            {
                System.Diagnostics.Debug.Assert(!type.IsValueType);

                // Get the reflection info for this type
                var ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri == null)
                    return null;

                // We'll create setters for each property/field
                var setters = new Dictionary<string, Action<IJsonReader, object>>();

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

                    // Ignore read only properties that has KeepInstance attribute
                    if (pi != null && pi.GetGetMethod() == null && m.KeepInstance)
                    {
                        continue;
                    }

                    // Create a dynamic method that can do the work
                    var method = new DynamicMethod("dynamic_parser", null, new Type[] { typeof(IJsonReader), typeof(object) }, true);
                    var il = method.GetILGenerator();

                    // Load the target
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Castclass, type);

                    // Try to keep existing instance?
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

                    // Get the value from IJsonReader
                    GenerateGetJsonValue(m, il);

                    // Assign it
                    if (pi != null)
                        il.Emit(OpCodes.Callvirt, pi.GetSetMethod());
                    if (fi != null)
                        il.Emit(OpCodes.Stfld, fi);

                    // Done
                    il.Emit(OpCodes.Ret);

                    // Store the handler in map
                    setters.Add(m.JsonKey, (Action<IJsonReader, object>)method.CreateDelegate(typeof(Action<IJsonReader, object>)));
                }


                // Now create the parseInto delegate
                Action<IJsonReader, object> parseInto = (reader, obj) =>
                {
                    // Call IJsonLoading
                    var loading = obj as IJsonLoading;
                    if (loading!=null)
                        loading.OnJsonLoading(reader);

                    // Cache IJsonLoadField
                    var lf = obj as IJsonLoadField;

                    // Read dictionary keys
                    reader.ParseDictionary(key =>
                    {
                        // Call IJsonLoadField
                        if (lf != null && lf.OnJsonField(reader, key))
                            return;

                        // Call setters
                        Action<IJsonReader, object> setter;
                        if (setters.TryGetValue(key, out setter))
                        {
                            setter(reader, obj);
                        }
                    });

                    // Call IJsonLoaded
                    var loaded = obj as IJsonLoaded;
                    if (loaded != null)
                        loaded.OnJsonLoaded(reader);
                };

                // Since we've created the ParseInto handler, we might as well register
                // as a Parse handler too.
                RegisterIntoParser(type, parseInto);

                // Done
                return parseInto;
            }

            // Registers a ParseInto handler as Parse handler that instantiates the object
            // and then parses into it.
            static void RegisterIntoParser(Type type, Action<IJsonReader, object> parseInto)
            {
                // Create a dynamic method that can do the work
                var method = new DynamicMethod("dynamic_factory", typeof(object), new Type[] { typeof(IJsonReader), typeof(Action<IJsonReader, object>) }, true);
                var il = method.GetILGenerator();

                // Create the new object
                var locObj = il.DeclareLocal(typeof(object));
                il.Emit(OpCodes.Newobj, type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[0], null));

                il.Emit(OpCodes.Dup);               // For return value

                il.Emit(OpCodes.Stloc, locObj);

                il.Emit(OpCodes.Ldarg_1);           // parseinto delegate
                il.Emit(OpCodes.Ldarg_0);           // IJsonReader
                il.Emit(OpCodes.Ldloc, locObj);     // new object instance
                il.Emit(OpCodes.Callvirt, typeof(Action<IJsonReader, object>).GetMethod("Invoke"));
                il.Emit(OpCodes.Ret);

                var factory = (Func<IJsonReader, Action<IJsonReader, object>, object>)method.CreateDelegate(typeof(Func<IJsonReader, Action<IJsonReader, object>, object>));

                Json.RegisterParser(type, (reader, type2) =>
                {
                    return factory(reader, parseInto);
                });
            }

            // Generate the MSIL to retrieve a value for a particular field or property from a IJsonReader
            private static void GenerateGetJsonValue(JsonMemberInfo m, ILGenerator il)
            {
                Action<string> generateCallToHelper = helperName =>
                {
                    // Call the helper
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, typeof(Emit).GetMethod(helperName, new Type[] { typeof(IJsonReader) }));

                    // Move to next token
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Callvirt, typeof(IJsonReader).GetMethod("NextToken", new Type[] { }));
                };

                Type[] numericTypes = new Type[] { 
                    typeof(int), typeof(uint), typeof(long), typeof(ulong), 
                    typeof(short), typeof(ushort), typeof(decimal), 
                    typeof(byte), typeof(sbyte), 
                    typeof(double), typeof(float)
                };

                if (m.MemberType == typeof(string))
                {
                    generateCallToHelper("GetLiteralString");
                }

                else if (m.MemberType == typeof(bool))
                {
                    generateCallToHelper("GetLiteralBool");
                }

                else if (m.MemberType == typeof(char))
                {
                    generateCallToHelper("GetLiteralChar");
                }

                else if (numericTypes.Contains(m.MemberType))
                {
                    // Get raw number string
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, typeof(Emit).GetMethod("GetLiteralNumber", new Type[] { typeof(IJsonReader) }));

                    // Convert to a string
                    il.Emit(OpCodes.Call, typeof(CultureInfo).GetProperty("InvariantCulture").GetGetMethod());
                    il.Emit(OpCodes.Call, m.MemberType.GetMethod("Parse", new Type[] { typeof(string), typeof(IFormatProvider) }));

                    // 
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
            }

            // Helper to fetch a literal bool from an IJsonReader
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

            // Helper to fetch a literal character from an IJsonReader
            public static char GetLiteralChar(IJsonReader r)
            {
                if (r.GetLiteralKind() != LiteralKind.String)
                    throw new InvalidDataException("expected a single character string literal");
                var str = r.GetLiteralString();
                if (str==null || str.Length!=1)
                    throw new InvalidDataException("expected a single character string literal");

                return str[0];
            }

            // Helper to fetch a literal string from an IJsonReader
            public static string GetLiteralString(IJsonReader r)
            {
                if (r.GetLiteralKind() != LiteralKind.String)
                    throw new InvalidDataException("expected a string literal");
                return r.GetLiteralString();
            }

            // Helper to fetch a literal number from an IJsonReader (returns the raw string)
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
