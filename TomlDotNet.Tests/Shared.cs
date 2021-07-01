using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomlDotNet.Tests
{
    public record Data(long L, double D, string S, bool B);
    public record Nested(Inner I);
    public record Inner(long L);

    public record ArrayHolder(List<object> A);

    public record Conv(long L, int I, uint UI, ulong UL, double LtoD, float LtoF, double D, float F);

    public record DatesTimes(DateTime DT, DateTime DTUtc, DateTimeOffset Dto);

    public record NullTypes(int? In, string? Sn);
    public record NoNullTypes(string Sn);

    /// <summary>
    /// USed in test Deserialize -> TestCOnstructorFilter
    /// </summary>
    public class ManyConstructors
    {
        //public ManyConstructors() { }
        static ManyConstructors() { } //should be filtered (is static)
        private ManyConstructors(int z, int y, int x) { } //should be filterd(is private)
        public ManyConstructors(int i, int j, int k, int l, int m, int n, int o, int p, int q) { } //should be filtered, # req params to large
        public ManyConstructors(int i) { }
        public ManyConstructors(int i, string s) { }
        public ManyConstructors(int i = 5, int j = 10, int k = 15, int l = 20, int m = 25) { }
    }
}
