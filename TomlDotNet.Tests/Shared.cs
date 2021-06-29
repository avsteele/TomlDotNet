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
}
