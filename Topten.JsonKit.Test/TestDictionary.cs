using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Topten.JsonKit;
using Xunit;

namespace TestCases
{
    public class TestDictionary
    {
        [Fact]
        public void DictionaryNonStringKeys()
        {
            var dict = new Dictionary<int, double>()
            {
                [-1] = 50,
                [0] = 100,
                [1] = 200,
            };

            var json = Json.Format(dict);

            var dict2 = Json.Parse<Dictionary<int, double>>(json);

            Assert.Equal(dict, dict2);
        }
    }
}
