using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Topten.JsonKit;
using System.IO;
using System.Reflection;
using Xunit;

namespace TestCases
{
    public class TestGuid
    {
        [Fact]
        public void Test()
        {
            var src = new Dictionary<Guid, string>()
            {
                { Guid.NewGuid(), "First" },
                { Guid.NewGuid(), "Second" },
                { Guid.NewGuid(), "Third" },
            };

            var json = Json.Format(src);

            var dest = Json.Parse<Dictionary<Guid, string>>(json);

        }
    }
}
