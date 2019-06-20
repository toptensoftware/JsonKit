using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Topten.JsonKit;
using System.Reflection;
using Xunit;

namespace TestCases
{
    [Json]
    struct StructEvents : IJsonLoaded, IJsonLoading, IJsonLoadField, IJsonWriting, IJsonWritten
    {
        public int IntField;

        [JsonExclude] public bool loading;
        [JsonExclude] public bool loaded;
        [JsonExclude] public bool fieldLoaded;

        void IJsonLoaded.OnJsonLoaded(IJsonReader r)
        {
            loaded = true;
        }

        void IJsonLoading.OnJsonLoading(IJsonReader r)
        {
            loading = true;
        }

        bool IJsonLoadField.OnJsonField(IJsonReader r, string key)
        {
            fieldLoaded = true;
            return false;
        }

        void IJsonWriting.OnJsonWriting(IJsonWriter w)
        {
            w.WriteRaw("/* OnJsonWriting */");
        }

        void IJsonWritten.OnJsonWritten(IJsonWriter w)
        {
            w.WriteRaw("/* OnJsonWritten */");
        }
    }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class TestsEvents
    {
        [Fact]
        public void TestStructLoadEvents()
        {
            var o2 = Json.Parse<StructEvents>("{\"IntField\":23}");
            Assert.True(o2.loading);
            Assert.True(o2.loaded);
            Assert.True(o2.fieldLoaded);
        }

        [Fact]
        public void TestStructWriteEvents()
        {
            var o = new StructEvents();
            o.IntField = 23;

            var json = Json.Format(o);
            Assert.Contains("OnJsonWriting", json);
            Assert.Contains("OnJsonWritten", json);
        }
    }
}
