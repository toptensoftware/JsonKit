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
using System.Text;
using System.IO;
using System.Globalization;

namespace PetaJson
{
    public class Tokenizer
    {
        public Tokenizer(TextReader r, JsonOptions options)
        {
            _underlying = r;
            _options = options;
            FillBuffer();
            NextChar();
            NextToken();
        }

        private JsonOptions _options;
        private StringBuilder _sb = new StringBuilder();
        private TextReader _underlying;
        private char[] _buf = new char[4096];
        private int _pos;
        private int _bufUsed;
        private StringBuilder _rewindBuffer;
        private int _rewindBufferPos;
        private JsonLineOffset _currentCharPos;
        private char _currentChar;
        private Stack<ReaderState> _bookmarks = new Stack<ReaderState>();

        public JsonLineOffset CurrentTokenPosition;
        public Token CurrentToken;
        public LiteralKind LiteralKind;
        public string String;

        public object LiteralValue
        {
            get
            {
                if (CurrentToken != Token.Literal)
                    throw new InvalidOperationException("token is not a literal");
                switch (LiteralKind)
                {
                    case LiteralKind.Null: return null;
                    case LiteralKind.False: return false;
                    case LiteralKind.True: return true;
                    case LiteralKind.String: return String;
                    case LiteralKind.SignedInteger: return long.Parse(String, CultureInfo.InvariantCulture);
                    case LiteralKind.UnsignedInteger:
                        if (String.StartsWith("0x") || String.StartsWith("0X"))
                            return Convert.ToUInt64(String.Substring(2), 16);
                        else
                            return ulong.Parse(String, CultureInfo.InvariantCulture);
                    case LiteralKind.FloatingPoint: return double.Parse(String, CultureInfo.InvariantCulture);
                }
                return null;
            }
        }

        public Type LiteralType
        {
            get
            {
                if (CurrentToken != Token.Literal)
                    throw new InvalidOperationException("token is not a literal");
                switch (LiteralKind)
                {
                    case LiteralKind.Null: return typeof(Object);
                    case LiteralKind.False: return typeof(Boolean);
                    case LiteralKind.True: return typeof(Boolean);
                    case LiteralKind.String: return typeof(string);
                    case LiteralKind.SignedInteger: return typeof(long);
                    case LiteralKind.UnsignedInteger: return typeof(ulong);
                    case LiteralKind.FloatingPoint: return typeof(double);
                }

                return null;
            }
        }

        // This object represents the entire state of the reader and is used for rewind
        struct ReaderState
        {
            public ReaderState(Tokenizer tokenizer)
            {
                _currentCharPos = tokenizer._currentCharPos;
                _currentChar = tokenizer._currentChar;
                _string = tokenizer.String;
                _literalKind = tokenizer.LiteralKind;
                _rewindBufferPos = tokenizer._rewindBufferPos;
                _currentTokenPos = tokenizer.CurrentTokenPosition;
                _currentToken = tokenizer.CurrentToken;
            }

            public void Apply(Tokenizer tokenizer)
            {
                tokenizer._currentCharPos = _currentCharPos;
                tokenizer._currentChar = _currentChar;
                tokenizer._rewindBufferPos = _rewindBufferPos;
                tokenizer.CurrentToken = _currentToken;
                tokenizer.CurrentTokenPosition = _currentTokenPos;
                tokenizer.String = _string;
                tokenizer.LiteralKind = _literalKind;
            }

            private JsonLineOffset _currentCharPos;
            private JsonLineOffset _currentTokenPos;
            private char _currentChar;
            private Token _currentToken;
            private LiteralKind _literalKind;
            private string _string;
            private int _rewindBufferPos;
        }

        // Create a rewind bookmark
        public void CreateBookmark()
        {
            _bookmarks.Push(new ReaderState(this));
            if (_rewindBuffer == null)
            {
                _rewindBuffer = new StringBuilder();
                _rewindBufferPos = 0;
            }
        }

        // Discard bookmark
        public void DiscardBookmark()
        {
            _bookmarks.Pop();
            if (_bookmarks.Count == 0)
            {
                _rewindBuffer = null;
                _rewindBufferPos = 0;
            }
        }

        // Rewind to a bookmark
        public void RewindToBookmark()
        {
            _bookmarks.Pop().Apply(this);
        }

        // Fill buffer by reading from underlying TextReader
        void FillBuffer()
        {
            _bufUsed = _underlying.Read(_buf, 0, _buf.Length);
            _pos = 0;
        }

        // Get the next character from the input stream
        // (this function could be extracted into a few different methods, but is mostly inlined
        //  for performance - yes it makes a difference)
        public char NextChar()
        {
            if (_rewindBuffer == null)
            {
                if (_pos >= _bufUsed)
                {
                    if (_bufUsed > 0)
                    {
                        FillBuffer();
                    }
                    if (_bufUsed == 0)
                    {
                        return _currentChar = '\0';
                    }
                }

                // Next
                _currentCharPos.Offset++;
                return _currentChar = _buf[_pos++];
            }

            if (_rewindBufferPos < _rewindBuffer.Length)
            {
                _currentCharPos.Offset++;
                return _currentChar = _rewindBuffer[_rewindBufferPos++];
            }
            else
            {
                if (_pos >= _bufUsed && _bufUsed > 0)
                    FillBuffer();

                _currentChar = _bufUsed == 0 ? '\0' : _buf[_pos++];
                _rewindBuffer.Append(_currentChar);
                _rewindBufferPos++;
                _currentCharPos.Offset++;
                return _currentChar;
            }
        }

        // Read the next token from the input stream
        // (Mostly inline for performance)
        public void NextToken()
        {
            while (true)
            {
                // Skip whitespace and handle line numbers
                while (true)
                {
                    if (_currentChar == '\r')
                    {
                        if (NextChar() == '\n')
                        {
                            NextChar();
                        }
                        _currentCharPos.Line++;
                        _currentCharPos.Offset = 0;
                    }
                    else if (_currentChar == '\n')
                    {
                        if (NextChar() == '\r')
                        {
                            NextChar();
                        }
                        _currentCharPos.Line++;
                        _currentCharPos.Offset = 0;
                    }
                    else if (_currentChar == ' ')
                    {
                        NextChar();
                    }
                    else if (_currentChar == '\t')
                    {
                        NextChar();
                    }
                    else
                        break;
                }
                    
                // Remember position of token
                CurrentTokenPosition = _currentCharPos;

                // Handle common characters first
                switch (_currentChar)
                {
                    case '/':
                        // Comments not support in strict mode
                        if ((_options & JsonOptions.StrictParser) != 0)
                        {
                            throw new InvalidDataException(string.Format("syntax error, unexpected character '{0}'", _currentChar));
                        }

                        // Process comment
                        NextChar();
                        switch (_currentChar)
                        {
                            case '/':
                                NextChar();
                                while (_currentChar!='\0' && _currentChar != '\r' && _currentChar != '\n')
                                {
                                    NextChar();
                                }
                                break;

                            case '*':
                                bool endFound = false;
                                while (!endFound && _currentChar!='\0')
                                {
                                    if (_currentChar == '*')
                                    {
                                        NextChar();
                                        if (_currentChar == '/')
                                        {
                                            endFound = true;
                                        }
                                    }
                                    NextChar();
                                }
                                break;

                            default:
                                throw new InvalidDataException("syntax error, unexpected character after slash");
                        }
                        continue;

                    case '\"':
                    case '\'':
                    {
                        _sb.Length = 0;
                        var quoteKind = _currentChar;
                        NextChar();
                        while (_currentChar!='\0')
                        {
                            if (_currentChar == '\\')
                            {
                                NextChar();
                                var escape = _currentChar;
                                switch (escape)
                                {
                                    case '\"': _sb.Append('\"'); break;
                                    case '\\': _sb.Append('\\'); break;
                                    case '/': _sb.Append('/'); break;
                                    case 'b': _sb.Append('\b'); break;
                                    case 'f': _sb.Append('\f'); break;
                                    case 'n': _sb.Append('\n'); break;
                                    case 'r': _sb.Append('\r'); break;
                                    case 't': _sb.Append('\t'); break;
                                    case 'u':
                                        var sbHex = new StringBuilder();
                                        for (int i = 0; i < 4; i++)
                                        {
                                            NextChar();
                                            sbHex.Append(_currentChar);
                                        }
                                        _sb.Append((char)Convert.ToUInt16(sbHex.ToString(), 16));
                                        break;

                                    default:
                                        throw new InvalidDataException(string.Format("Invalid escape sequence in string literal: '\\{0}'", _currentChar));
                                }
                            }
                            else if (_currentChar == quoteKind)
                            {
                                String = _sb.ToString();
                                CurrentToken = Token.Literal;
                                LiteralKind = LiteralKind.String;
                                NextChar();
                                return;
                            }
                            else
                            {
                                _sb.Append(_currentChar);
                            }

                            NextChar();
                        }
                        throw new InvalidDataException("syntax error, unterminated string literal");
                    }

                    case '{': CurrentToken =  Token.OpenBrace; NextChar(); return;
                    case '}': CurrentToken =  Token.CloseBrace; NextChar(); return;
                    case '[': CurrentToken =  Token.OpenSquare; NextChar(); return;
                    case ']': CurrentToken =  Token.CloseSquare; NextChar(); return;
                    case '=': CurrentToken =  Token.Equal; NextChar(); return;
                    case ':': CurrentToken =  Token.Colon; NextChar(); return;
                    case ';': CurrentToken =  Token.SemiColon; NextChar(); return;
                    case ',': CurrentToken =  Token.Comma; NextChar(); return;
                    case '\0': CurrentToken = Token.EOF; return;
                }

                // Number?
                if (char.IsDigit(_currentChar) || _currentChar == '-')
                {
                    TokenizeNumber();
                    return;
                }

                // Identifier?  (checked for after everything else as identifiers are actually quite rare in valid json)
                if (Char.IsLetter(_currentChar) || _currentChar == '_' || _currentChar == '$')
                {
                    // Find end of identifier
                    _sb.Length = 0;
                    while (Char.IsLetterOrDigit(_currentChar) || _currentChar == '_' || _currentChar == '$')
                    {
                        _sb.Append(_currentChar);
                        NextChar();
                    }
                    String = _sb.ToString();

                    // Handle special identifiers
                    switch (String)
                    {
                        case "true":
                            LiteralKind = LiteralKind.True;
                            CurrentToken =  Token.Literal;
                            return;

                        case "false":
                            LiteralKind = LiteralKind.False;
                            CurrentToken =  Token.Literal;
                            return;

                        case "null":
                            LiteralKind = LiteralKind.Null;
                            CurrentToken =  Token.Literal;
                            return;
                    }

                    CurrentToken =  Token.Identifier;
                    return;
                }

                // What the?
                throw new InvalidDataException(string.Format("syntax error, unexpected character '{0}'", _currentChar));
            }
        }

        // Parse a sequence of characters that could make up a valid number
        // For performance, we don't actually parse it into a number yet.  When using PetaJsonEmit we parse
        // later, directly into a value type to avoid boxing
        private void TokenizeNumber()
        {
            _sb.Length = 0;

            // Leading negative sign
            bool signed = false;
            if (_currentChar == '-')
            {
                signed = true;
                _sb.Append(_currentChar);
                NextChar();
            }

            // Hex prefix?
            bool hex = false;
            if (_currentChar == '0' && (_options & JsonOptions.StrictParser)==0)
            {
                _sb.Append(_currentChar);
                NextChar();
                if (_currentChar == 'x' || _currentChar == 'X')
                {
                    _sb.Append(_currentChar);
                    NextChar();
                    hex = true;
                }
            }

            // Process characters, but vaguely figure out what type it is
            bool cont = true;
            bool fp = false;
            while (cont)
            {
                switch (_currentChar)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        _sb.Append(_currentChar);
                        NextChar();
                        break;

                    case 'A':
                    case 'a':
                    case 'B':
                    case 'b':
                    case 'C':
                    case 'c':
                    case 'D':
                    case 'd':
                    case 'F':
                    case 'f':
                        if (!hex)
                            cont = false;
                        else
                        {
                            _sb.Append(_currentChar);
                            NextChar();
                        }
                        break;

                    case '.':
                        if (hex)
                        {
                            cont = false;
                        }
                        else
                        {
                            fp = true;
                            _sb.Append(_currentChar);
                            NextChar();
                        }
                        break;

                    case 'E':
                    case 'e':
                        if (!hex)
                        {
                            fp = true;
                            _sb.Append(_currentChar);
                            NextChar();
                            if (_currentChar == '+' || _currentChar == '-')
                            {
                                _sb.Append(_currentChar);
                                NextChar();
                            }
                        }
                        break;

                    default:
                        cont = false;
                        break;
                }
            }

            if (char.IsLetter(_currentChar))
                throw new InvalidDataException(string.Format("syntax error, invalid character following number '{0}'", _sb.ToString()));

            // Setup token
            String = _sb.ToString();
            CurrentToken = Token.Literal;

            // Setup literal kind
            if (fp)
            {
                LiteralKind = LiteralKind.FloatingPoint;
            }
            else if (signed)
            {
                LiteralKind = LiteralKind.SignedInteger;
            }
            else
            {
                LiteralKind = LiteralKind.UnsignedInteger;
            }
        }

        // Check the current token, throw exception if mismatch
        public void Check(Token tokenRequired)
        {
            if (tokenRequired != CurrentToken)
            {
                throw new InvalidDataException(string.Format("syntax error, expected {0} found {1}", tokenRequired, CurrentToken));
            }
        }

        // Skip token which must match
        public void Skip(Token tokenRequired)
        {
            Check(tokenRequired);
            NextToken();
        }

        // Skip token if it matches
        public bool SkipIf(Token tokenRequired)
        {
            if (tokenRequired == CurrentToken)
            {
                NextToken();
                return true;
            }
            return false;
        }
    }
}
