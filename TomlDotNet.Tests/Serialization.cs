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
        public void Basic()
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
            throw new NotImplementedException();
        }

        public record Exclusion([field:NonSerialized] long L);


        [TestMethod]
        public void Nested()
        {
            Nested dIn = new(new(5L));
            var s = Serialize.RecordToTomlString(dIn);
            var filename = @"serializeNested.toml";
            System.IO.File.WriteAllText(filename, s);
            var dOut = Deserialize.FromFile<Nested>(filename);
            Assert.IsTrue(dIn == dOut);
            ;
        }

        [TestMethod]
        public void BasicAll()
        {
            Many dIn = new(5L, 6, 7, 8, 9, 255, 12.12, 15.12F, "hello", true);
            var s = Serialize.RecordToTomlString(dIn);
            var filename = @"serializeMany.toml";
            System.IO.File.WriteAllText(filename, s);

            /// conversion sused when deserializing back from file
            Deserialize.Conversions.Add((typeof(long), typeof(int)), (i) => Convert.ToInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(uint)), (i) => Convert.ToUInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(short)), (i) => Convert.ToInt16((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(ushort)), (i) => Convert.ToUInt16((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(byte)), (i) => Convert.ToByte((long)i));
            Deserialize.Conversions.Add((typeof(double), typeof(float)), (d) => Convert.ToSingle((double)d));

            var dOut = Deserialize.FromFile<Many>(filename);
            Assert.IsTrue(dIn == dOut);
            ;
        }

        [TestMethod]
        public void Array()
        {
            HomoArray dIn = new(new() { 5L, 6L, 7L }, new() { true, false, true });
            var filename = @"serializeArray.toml";
            Serialize.RecordToTomlFile(dIn, filename);

            var dOut = Deserialize.FromFile<HomoArray>(filename);// fails, writes empty tables insytead of arrays
            Assert.IsTrue(dIn.L[1] == dOut.L[1]);
            Assert.IsTrue(dIn.B[1] == dOut.B[1]);
        }
    }
}
