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

            var s = Serialize.ToString(dIn);
            var filename = @"serializeBasic.toml";
            System.IO.File.WriteAllText(filename, s);

            var dOut = Deserialize.FromFile<Data>(filename);

            Assert.IsTrue(dIn == dOut);
        }

        [TestMethod]
        public void Exclude()
        {
            Exclusion dIn = new(5L, true, "hi");
            var filename = @"Exclusion.toml";
            Serialize.ToFile(dIn, filename);

            // shoudl throw
            try
            {
                var dOut = Deserialize.FromFile<Exclusion>(filename);

            } catch (AggregateException) { return; }
            throw new Exception("did not throw as expected");
            //Assert.IsTrue(dIn == dOut);
        }

        public record Exclusion([field:NonSerialized] long L, bool B, string S);


        [TestMethod]
        public void Nested()
        {
            Nested dIn = new(new(5L));
            var s = Serialize.ToString(dIn);
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
            var s = Serialize.ToString(dIn);
            var filename = @"serializeMany.toml";
            System.IO.File.WriteAllText(filename, s);

            /// conversion sused when deserializing back from file
            Deserialize.Conversions.Clear();
            Deserialize.AddNumericConversions();

            var dOut = Deserialize.FromFile<Many>(filename);
            Assert.IsTrue(dIn == dOut);
            ;
        }

        [TestMethod]
        public void Array()
        {
            HomoArray dIn = new(new() { 5L, 6L, 7L }, new() { true, false, true }, new() { 8, 9, 10 });
            var filename = @"serializeArray.toml";
            Serialize.ToFile(dIn, filename);

            var dOut = Deserialize.FromFile<HomoArray>(filename);// fails, writes empty tables insytead of arrays
            Assert.IsTrue(dIn.L[1] == dOut.L[1]);
            Assert.IsTrue(dIn.B[1] == dOut.B[1]);
        }

        [TestMethod]
        public void ArrayOfTablesTest()
        {
            ArrayOfTables dIn = new(new() { new(5, true), new(6, false), new(7, true) });
            var fileName = @"SerializeArrayofTables.toml";
            Serialize.ToFile(dIn, fileName);

            var dOut = Deserialize.FromFile<ArrayOfTables>(fileName);
            Assert.IsTrue(Deserialization.Same(dIn.A, dOut.A));
        }

        [TestMethod]
        public void ArrayOfTablesHarderTest()
        {
            ArrayOfTables2 dIn = new(new() 
            { 
                new(5,true,"hi",7.7, new() { 1.1,2.2,3.3}) , 
                new(6, true, "hi2", 8.8, new() { 4.1, 5.2, 6.3 }) 
            });
            var fileName = @"SerializeArrayofTables2.toml";
            Serialize.ToFile(dIn, fileName);

            var dOut = Deserialize.FromFile<ArrayOfTables2>(fileName);
            // TODO: missing check for correctness
            //Assert.IsTrue(Deserialization.Same(dIn.A, dOut.A));
        }


        public struct TestProp
        {
            public double X { get; set; }
            public double Y { get; }
            public double Z { get; init; }
        }

        [TestMethod]
        public void ValueType()
        {
            var fileName = @"SerializeValueType.toml";
            var tp = new TestProp() { X = 5, Z = 8 };

            Serialize.ToFile(tp, fileName);
            //var tpIn = Deserialize.FromFile<TestProp>(fileName);
            //Console.WriteLine(s);
        }


        public class TestField
        {
            public double X = 1;
            public double Y { get; set; } = 2;
            public double Z { get; } = 3;

            [field: NonSerialized]
            public double A { get; set; } = 4;
            [NonSerialized]
            public double B = 5;

            public IEnumerable<double> C { get; init; } = new List<double>() { 9, 8, 7, 6 };
        }
        [TestMethod]
        public void Field()
        {
            var fileName = @"SerializeField.toml";
            var s = Serialize.ToString(new System.Numerics.Vector2(3f,6f));
            Console.WriteLine(s);


            // that field labeld NonSerialized are not serialized
            var data = new TestField() { A = 99 };
            var s2 = Serialize.ToString(data);

            Console.WriteLine(s2); // looks correct
            Serialize.ToFile(new TestField(), fileName);

            var sOut = Deserialize.FromString<TestField>(s2);
            Assert.IsTrue(sOut.A == 4); // not 99
            ;
        }

        [TestMethod]
        public void Inheritance()
        {
            var data = new Derived() { C = 99 };
            var str = Serialize.ToString(data);
            var dataOut = Deserialize.FromString<Derived>(str);
            Assert.IsTrue(dataOut.C == 7); // not 99
        }

        [TestMethod]
        public void Enum()
        {
            var data = new EnumTypes(Test.Yes,Test.No, Test.Maybe);
            var str = Serialize.ToString(data);
            var dataOut = Deserialize.FromString<EnumTypes>(str);
            Assert.IsTrue(data == dataOut); // not 99
        }
    }
}
