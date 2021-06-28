using System;
using System.Reflection;
using System.Linq;
using Tomlet.Models;
using System.Collections.Generic;


namespace TomlDotNet
{
    public enum TomlData { Table, Array, Comment, String, Integer, Float, DateTime }

    //public delegate object Converter(TomlValue value, Type targetType);
    //public delegate T ValueTypeConverter<T, U>(U from) where T : struct where U : struct;
    /// https://github.com/SamboyCoding/Tomlet
    /// https://github.com/toml-lang/toml/blob/master/toml.md
    /// https://github.com/paiden/Nett
    //
    /// <summary>
    /// 
    /// </summary>
    public static class Toml
    {
        public static Dictionary<(Type from, Type to), Func<object, object>> Conversions { get; private set; } = new();

        public static T GetFromFile<T>(string filePath) where T : class, new()
        => GetFromFile<T>(System.IO.File.ReadAllText(filePath));

        public static T GetFromString<T>(string tomlFileContents) where T : class, new()
        {
            var parser = new Tomlet.TomlParser();
            return Get<T>(parser.Parse(tomlFileContents));
        }

        public static T Get<T>(TomlTable tt) where T : class
            => (T)Get(tt, typeof(T));

        private static object Get(TomlTable tt, Type type)
        {
            // TODO: assuming just one for now
            var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
            var param_list = constructor.GetParameters();

            var params_ = new object[param_list.Length];
            foreach (var (v, i) in param_list.Select((v, i) => (v, i)))
            {
                params_[i] = GetObj(tt, v.ParameterType, v.Name);
            }
            return Convert.ChangeType(constructor.Invoke(params_), type);
        }

        private static object GetObj(TomlTable tt, Type type, string? key)
        {
            var value = tt.GetValue(key ?? throw new ArgumentNullException(nameof(key)));
            return ConvertObj(value, type);
        }

        private static object ConvertObj(TomlValue value, Type type)
            => value switch
            {
                TomlString s => type switch 
                    {
                        Type t when t == typeof(string) => ConvertBaseObj(s),
                        _ => throw new NotImplementedException(),
                    },
                TomlLong i => type switch
                    {
                        Type t when t == typeof(long) => ConvertBaseObj(i),
                        // look for converter
                        //Type t when t == typeof(int) => Convert.ToInt32(i.Value),
                        _ => Conversions.ContainsKey((from: typeof(long), to: type))
                        ? Conversions[(from: typeof(long), to: type)](i.Value)
                        : throw new InvalidCastException($"long->{type}"),
                    },
                TomlBoolean b => ConvertBaseObj(b),                
                TomlDouble d => type switch
                {
                    Type t when t==typeof(double) => ConvertBaseObj(d),
                    _ => Conversions.ContainsKey((from: typeof(double), to: type))
                    ? Conversions[(from: typeof(double), to: type)](d.Value)
                    : throw new InvalidCastException($"double->{type}"),
                },
                TomlArray a => ConvertBaseObj(a),
                TomlLocalDateTime ldt => ConvertBaseObj(ldt),
                TomlOffsetDateTime odt => ConvertBaseObj(odt),
                TomlTable t => Get(t, type),
                null => throw new ArgumentNullException(nameof(value)),
                _ => throw new NotImplementedException(type.ToString()),
            };

        private static object ConvertBaseObj(TomlValue value)
            => value switch
            {
                TomlString s => s.Value,
                TomlLong i => i.Value,
                TomlBoolean b => b.Value,
                TomlDouble d => d.Value,
                TomlArray a => ConvertArray(a),
                TomlLocalDateTime ldt => ldt.Value,
                TomlOffsetDateTime odt => odt.Value,
                TomlTable => throw new NotSupportedException("Array of tables not supported"),
                _ => throw new InvalidOperationException("Only TomlString, long ,bool, double, or array (of obj) allowed")
            };

        public static List<object> ConvertArray(TomlArray a)
            => new(a.AsEnumerable().Select((v,_)=>ConvertBaseObj(v)));


        #region Built-in Converters

        public static int ToInt(long l) => Convert.ToInt32(l);
        public static uint ToUInt(long l) => Convert.ToUInt32(l);
        public static double ToDouble(long l) => Convert.ToDouble(l);
        public static ulong ToULong(long l) => Convert.ToUInt64(l);
        #endregion
    }
}