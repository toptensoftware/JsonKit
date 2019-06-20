using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaJson;
using System.Collections;
using Xunit;

namespace TestCases
{
    public class TestConcreteFromInterface
    {
        [Fact]
        public void TestGenericList()
        {
            var l = new List<int>() { 10, 20, 30 };

            var json = Json.Format(l);

            var l2 = Json.Parse<IList<int>>(json);
            Assert.IsType<List<int>>(l2);

            Assert.Equal(l, l2);
        }

        [Fact]
        public void TestGenericDictionary()
        {
            var l = new Dictionary<string,int>() { 
                {"A", 10}, 
                {"B", 20},
                {"C", 30}
            };

            var json = Json.Format(l);

            var l2 = Json.Parse<IDictionary<string,int>>(json);
            Assert.IsType<Dictionary<string,int>>(l2);

            Assert.Equal(l, l2);
        }

        [Fact]
        public void TestObjectList()
        {
            var l = new List<int>() { 10, 20, 30 };

            var json = Json.Format(l);

            var l2 = Json.Parse<IList>(json);
            Assert.IsType<List<object>>(l2);

            Assert.Equal(l.Count, l2.Count);
        }

        [Fact]
        public void TestObjectDictionary()
        {
            var l = new Dictionary<string, int>() { 
                {"A", 10}, 
                {"B", 20},
                {"C", 30}
            };

            var json = Json.Format(l);

            var l2 = Json.Parse<IDictionary>(json);
            Assert.IsType<Dictionary<string,object>>(l2);
            Assert.Equal(l.Count, l2.Count);
        }

    }
}
