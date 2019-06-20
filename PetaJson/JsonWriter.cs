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
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;


namespace PetaJson
{
    class JsonWriter : IJsonWriter
    {
        static JsonWriter()
        {
            _formatterResolver = ResolveFormatter;

            // Register standard formatters
            _formatters.Add(typeof(string), (w, o) => w.WriteStringLiteral((string)o));
            _formatters.Add(typeof(char), (w, o) => w.WriteStringLiteral(((char)o).ToString()));
            _formatters.Add(typeof(bool), (w, o) => w.WriteRaw(((bool)o) ? "true" : "false"));
            Action<IJsonWriter, object> convertWriter = (w, o) => w.WriteRaw((string)Convert.ChangeType(o, typeof(string), System.Globalization.CultureInfo.InvariantCulture));
            _formatters.Add(typeof(int), convertWriter);
            _formatters.Add(typeof(uint), convertWriter);
            _formatters.Add(typeof(long), convertWriter);
            _formatters.Add(typeof(ulong), convertWriter);
            _formatters.Add(typeof(short), convertWriter);
            _formatters.Add(typeof(ushort), convertWriter);
            _formatters.Add(typeof(decimal), convertWriter);
            _formatters.Add(typeof(byte), convertWriter);
            _formatters.Add(typeof(sbyte), convertWriter);
            _formatters.Add(typeof(DateTime), (w, o) => convertWriter(w, Utils.ToUnixMilliseconds((DateTime)o)));
            _formatters.Add(typeof(float), (w, o) => w.WriteRaw(((float)o).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            _formatters.Add(typeof(double), (w, o) => w.WriteRaw(((double)o).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            _formatters.Add(typeof(byte[]), (w, o) =>
            {
                w.WriteRaw("\"");
                w.WriteRaw(Convert.ToBase64String((byte[])o));
                w.WriteRaw("\"");
            });
        }

        public static Func<Type, Action<IJsonWriter, object>> _formatterResolver;
        public static Dictionary<Type, Action<IJsonWriter, object>> _formatters = new Dictionary<Type, Action<IJsonWriter, object>>();

        static Action<IJsonWriter, object> ResolveFormatter(Type type)
        {
            // Try `void FormatJson(IJsonWriter)`
            var formatJson = ReflectionInfo.FindFormatJson(type);
            if (formatJson != null)
            {
                if (formatJson.ReturnType==typeof(void))
                    return (w, obj) => formatJson.Invoke(obj, new Object[] { w });
                if (formatJson.ReturnType == typeof(string))
                    return (w, obj) => w.WriteStringLiteral((string)formatJson.Invoke(obj, new Object[] { }));
            }

            var ri = ReflectionInfo.GetReflectionInfo(type);
            if (ri != null)
                return ri.Write;
            else
                return null;
        }

        public JsonWriter(TextWriter w, JsonOptions options)
        {
            _writer = w;
            _atStartOfLine = true;
            _needElementSeparator = false;
            _options = options;
        }

        private TextWriter _writer;
        private int IndentLevel;
        private bool _atStartOfLine;
        private bool _needElementSeparator = false;
        private JsonOptions _options;
        private char _currentBlockKind = '\0';

        // Move to the next line
        public void NextLine()
        {
            if (_atStartOfLine)
                return;

            if ((_options & JsonOptions.WriteWhitespace)!=0)
            {
                WriteRaw("\n");
                WriteRaw(new string('\t', IndentLevel));
            }
            _atStartOfLine = true;
        }

        // Start the next element, writing separators and white space
        void NextElement()
        {
            if (_needElementSeparator)
            {
                WriteRaw(",");
                NextLine();
            }
            else
            {
                NextLine();
                IndentLevel++;
                WriteRaw(_currentBlockKind.ToString());
                NextLine();
            }

            _needElementSeparator = true;
        }

        // Write next array element
        public void WriteElement()
        {
            if (_currentBlockKind != '[')
                throw new InvalidOperationException("Attempt to write array element when not in array block");
            NextElement();
        }

        // Write next dictionary key
        public void WriteKey(string key)
        {
            if (_currentBlockKind != '{')
                throw new InvalidOperationException("Attempt to write dictionary element when not in dictionary block");
            NextElement();
            WriteStringLiteral(key);
            WriteRaw(((_options & JsonOptions.WriteWhitespace) != 0) ? ": " : ":");
            _atStartOfLine = false;
        }

        // Write an already escaped dictionary key
        public void WriteKeyNoEscaping(string key)
        {
            if (_currentBlockKind != '{')
                throw new InvalidOperationException("Attempt to write dictionary element when not in dictionary block");
            NextElement();
            WriteRaw("\"");
            WriteRaw(key);
            WriteRaw("\"");
            WriteRaw(((_options & JsonOptions.WriteWhitespace) != 0) ? ": " : ":");
            _atStartOfLine = false;
        }

        // Write anything
        public void WriteRaw(string str)
        {
            _writer.Write(str);
            _atStartOfLine = false;
        }

        static int IndexOfEscapeableChar(string str, int pos)
        {
            int length = str.Length;
            while (pos < length)
            {
                var ch = str[pos];
                if (ch == '\\' || ch == '/' || ch == '\"' || (ch>=0 && ch <= 0x1f) || (ch >= 0x7f && ch <=0x9f) || ch==0x2028 || ch== 0x2029)
                    return pos;
                pos++;
            }

            return -1;
        }

        public void WriteStringLiteral(string str)
        {
            _atStartOfLine = false;
            if (str == null)
            {
                _writer.Write("null");
                return;
            }
            _writer.Write("\"");

            int pos = 0;
            int escapePos;
            while ((escapePos = IndexOfEscapeableChar(str, pos)) >= 0)
            {
                if (escapePos > pos)
                    _writer.Write(str.Substring(pos, escapePos - pos));

                switch (str[escapePos])
                {
                    case '\"': _writer.Write("\\\""); break;
                    case '\\': _writer.Write("\\\\"); break;
                    case '/':  _writer.Write("\\/"); break;
                    case '\b': _writer.Write("\\b"); break;
                    case '\f': _writer.Write("\\f"); break;
                    case '\n': _writer.Write("\\n"); break;
                    case '\r': _writer.Write("\\r"); break;
                    case '\t': _writer.Write("\\t"); break;
                    default:
                        _writer.Write(string.Format("\\u{0:x4}", (int)str[escapePos]));
                        break;
                }

                pos = escapePos + 1;
            }


            if (str.Length > pos)
                _writer.Write(str.Substring(pos));
            _writer.Write("\"");
        }

        // Write an array or dictionary block
        private void WriteBlock(string open, string close, Action callback)
        {
            var prevBlockKind = _currentBlockKind;
            _currentBlockKind = open[0];

            var didNeedElementSeparator = _needElementSeparator;
            _needElementSeparator = false;

            callback();

            if (_needElementSeparator)
            {
                IndentLevel--;
                NextLine();
            }
            else
            {
                WriteRaw(open);
            }
            WriteRaw(close);

            _needElementSeparator = didNeedElementSeparator;
            _currentBlockKind = prevBlockKind;
        }

        // Write an array
        public void WriteArray(Action callback)
        {
            WriteBlock("[", "]", callback);
        }

        // Write a dictionary
        public void WriteDictionary(Action callback)
        {
            WriteBlock("{", "}", callback);
        }

        // Write any value
        public void WriteValue(object value)
        {
            // Special handling for null
            if (value == null)
            {
                _writer.Write("null");
                return;
            }

            var type = value.GetType();

            // Handle nullable types
            var typeUnderlying = Nullable.GetUnderlyingType(type);
            if (typeUnderlying != null)
                type = typeUnderlying;

            // Look up type writer
            Action<IJsonWriter, object> typeWriter;
            if (_formatters.TryGetValue(type, out typeWriter))
            {
                // Write it
                typeWriter(this, value);
                return;
            }

            // Enumerated type?
            if (type.IsEnum)
            {
                if (type.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                    WriteRaw(Convert.ToUInt32(value).ToString(CultureInfo.InvariantCulture));
                else
                    WriteStringLiteral(value.ToString());
                return;
            }

            // Dictionary?
            var d = value as System.Collections.IDictionary;
            if (d != null)
            {
                WriteDictionary(() =>
                {
                    foreach (var key in d.Keys)
                    {
                        WriteKey(key.ToString());
                        WriteValue(d[key]);
                    }
                });
                return;
            }

            // Dictionary?
            var dso = value as IDictionary<string,object>;
            if (dso != null)
            {
                WriteDictionary(() =>
                {
                    foreach (var key in dso.Keys)
                    {
                        WriteKey(key.ToString());
                        WriteValue(dso[key]);
                    }
                });
                return;
            }

            // Array?
            var e = value as System.Collections.IEnumerable;
            if (e != null)
            {
                WriteArray(() =>
                {
                    foreach (var i in e)
                    {
                        WriteElement();
                        WriteValue(i);
                    }
                });
                return;
            }

            // Resolve a formatter
            var formatter = _formatterResolver(type);
            if (formatter != null)
            {
                _formatters[type] = formatter;
                formatter(this, value);
                return;
            }

            // Give up
            throw new InvalidDataException(string.Format("Don't know how to write '{0}' to json", value.GetType()));
        }
    }
}
