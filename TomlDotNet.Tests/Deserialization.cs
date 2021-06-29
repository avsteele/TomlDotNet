using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace TomlDotNet.Tests
{
    /// <summary>
    /// Tests of TomlDotNet's deserialization
    /// </summary>
    [TestClass]
    public class Deserialization
    {
        [TestMethod]
        public void BasicTypes()
        {
            var data = new Data(5, 0.123, "hello", true);
            var tt = new Tomlet.Models.TomlTable();
            tt.Put("L", data.L);
            tt.Put("D", data.D);
            tt.Put("S", data.S);
            tt.Put("B", data.B);
            var dout = Deserialize.FromToml<Data>(tt);
            Assert.IsTrue(data == dout);
        }

        [TestMethod]
        public void BasicDeserialization()
        {
            var dIn = TomlDotNet.Deserialize.FromFile<Data>("Example.toml");

            Assert.IsTrue((dIn.L == 5) && (dIn.D == 0.123) && (dIn.S=="hello") && (dIn.B == true));
        }

        [TestMethod]
        public void Array()
        {
            var a = new Tomlet.Models.TomlArray
            {
                5,
                false,
                5.55
            };
            // TODO: can't add string because .Add doesn't allow it, because Add requires types with no-arg constructors(?!)
            var tt = new Tomlet.Models.TomlTable();
            tt.PutValue("A", a);
            var r = new ArrayHolder(new() { 5L, false, 5.55 });
            //var arr = TomlDotNet.Toml.ConvertArray(a);
            Assert.IsTrue(((Tomlet.Models.TomlLong)a[0]).Value == (long)r.A[0]);
            Assert.IsTrue(((Tomlet.Models.TomlBoolean)a[1]).Value == (bool)r.A[1]);
            Assert.IsTrue(((Tomlet.Models.TomlDouble)a[2]).Value == (double)r.A[2]);
        }

        /// <summary>
        /// Test whether Datetime types can be put into toml form and then rad back into a CLR record
        /// Datetimes are serialized without an offset at the end, and without respect to the 'Kind' field. therefore
        /// ther is an ambigutuity.
        /// The optionare to always use the same 'kind' of DateTime, or alwasy use DateTimeOffset
        /// See https://github.com/paiden/Nett/blob/master/dfx/articles/pitfalls.md#datetime-vs-datetimeoffset
        /// </summary>
        [TestMethod]
        public void DateTimes()
        {
            var dtLocal = DateTime.Now;
            var dtUtc = dtLocal.ToUniversalTime();
            var dto = new DateTimeOffset(dtLocal);
            var dtIn = new DatesTimes(dtLocal, dtUtc, dto);

            Tomlet.Models.TomlDocument tt = Tomlet.Models.TomlDocument.CreateEmpty();
            tt.Put("DT", dtIn.DT);
            tt.Put("DTUtc", dtIn.DTUtc);
            tt.Put("Dto", dtIn.Dto);

            var dtOut = Deserialize.FromToml<DatesTimes>(tt);

            Assert.IsTrue(dtIn == dtOut);
        }

        [TestMethod]
        public void DateTimeFile()
        {
            var dtLocal = DateTime.Now;
            var dtUtc = dtLocal.ToUniversalTime();
            var dto = new DateTimeOffset(dtLocal);

            var dtIn = new DatesTimes(dtLocal, dtUtc, dto);

            Tomlet.Models.TomlDocument tt = Tomlet.Models.TomlDocument.CreateEmpty();
            tt.Put("DT", dtIn.DT); // serialized without offset time
            tt.Put("DTUtc", dtIn.DTUtc); //serializded withut offset time
            tt.Put("Dto", dtIn.Dto); // serialized with offsettime


            // now write t file and back to see if same
            var filename = @"dateTimeFile.toml";
            System.IO.File.WriteAllText(filename, tt.SerializedValue);
            
            var dtIn2 = TomlDotNet.Deserialize.FromFile<DatesTimes>(filename);

            Assert.IsTrue(dtIn == dtIn2);
        }

        [TestMethod]
        public void Converters()
        {
            var cIn = new Conv(4L, 5, 6, 7, 100L, 1001L, 8.8, 9.9F);

            Tomlet.Models.TomlTable tt = new();
            tt.Put("L", cIn.L); // inserted as TomlLong : NO CONVERSION
            tt.Put("I", cIn.I); // inserted as TomlLong : needs long=>int
            tt.Put("UI", cIn.UI); // inserted as TomlLong : needs long=> uint
            tt.Put("UL", cIn.UL); // inserted as TomlLong : needs long=> ulong
            tt.Put("LtoF", (long)cIn.LtoF); // inserted as TomlLong : long=>float
            tt.Put("LtoD", (long)cIn.LtoD); // inserted as TomlLong : long=>double
            tt.Put("D", cIn.D); // inserted as TomlDouble : NO CONVERSION
            tt.Put("F", cIn.F); // inserted as TomlDouble : needs double=>float

            Deserialize.Conversions.Add((typeof(long), typeof(int)), (i) => Convert.ToInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(uint)), (i) => Convert.ToUInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(ulong)), (i) => Convert.ToUInt64((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(float)), (i) => Convert.ToSingle((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(double)), (i) => Convert.ToDouble((long)i));
            Deserialize.Conversions.Add((typeof(double), typeof(float)), (d) => Convert.ToSingle((double)d));

            var cOut = Deserialize.FromToml<Conv>(tt); // attempt extaction full clas data, requires conversions
            Assert.IsTrue(cIn == cOut);
        }

        [TestMethod]
        public void NestedTypes()
        {
            var data = new Nested(new Inner(5));
            var I = new Tomlet.Models.TomlTable();
            I.Put("L", 5);
            var tt = new Tomlet.Models.TomlTable();
            // using 'Put' instead of putvalue here does something odd
            tt.PutValue("I", I);
            var dOut = Deserialize.FromToml<Nested>(tt);
            Assert.IsTrue(dOut.I.L == data.I.L);
        }

        [TestMethod]
        public void NullValues()
        {
            Tomlet.Models.TomlTable tt = new();

            try
            {
                NullTypes nte = Deserialize.FromToml<NullTypes>(tt);
                throw new Exception("Should have thrown on missing data because flag to allow null not set");
            }
            catch (InvalidOperationException) { }
            try
            {
                NoNullTypes nte = Deserialize.FromToml<NoNullTypes>(tt, true);
                throw new Exception("Should have thrown on missing data because, even though flag to allow null is set, typ eis not compaitble with it");
            }
            catch (InvalidOperationException) { }
            NullTypes nt = Deserialize.FromToml<NullTypes>(tt, true);
            Assert.IsTrue(nt.In is null && nt.Sn is null);
        }

        [TestMethod]
        public void SerializeBasic()
        {
            Tomlet.Models.TomlTable tt = new();

            tt.PutValue("b", Tomlet.Models.TomlBoolean.ValueOf(false));
            ;

            var dIn = new Data(5, 6.6, "hi", true);
            var s = Serialize.RecordToTomlString(dIn);
            var filename = @"serializeBasic.toml";
            System.IO.File.WriteAllText(filename, s);

            var dOut = Deserialize.FromFile<Data>(filename);

            Assert.IsTrue(dIn == dOut);
            ;
        }
    }
}
