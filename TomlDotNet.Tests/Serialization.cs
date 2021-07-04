using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TomlDotNet.Tests
{
    /// <summary>
    /// Tests of TomlDotNet's serialization
    /// </summary>
    [TestClass]
    public class Serialization
    {
        [TestMethod]
        public void SerializeBasic()
        {
            var dIn = new Data(5, 6.6, "hi", true);

            var s = Serialize.RecordToTomlString(dIn);
            var filename = @"serializeBasic.toml";
            System.IO.File.WriteAllText(filename, s);

            var dOut = Deserialize.FromFile<Data>(filename);

            Assert.IsTrue(dIn == dOut);
            ;
        }

        [TestMethod]
        public void Exclude()
        {

        }

        public record Exclusion([field:NonSerialized] long L);
    }
}
