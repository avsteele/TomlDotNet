using System;
using System.Reflection;
using System.Linq;
using Tomlet.Models;
using System.Collections.Generic;

namespace TomlDotNet
{
    public enum TomlData { Table, Array, Comment, String, Integer, Float, DateTime }

    public delegate object Converter(TomlValue value, Type targetType);

    public delegate T ValueTypeConverter<T, U>(U from) where T : struct where U : struct;

    public static class Toml
    {
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
            return Convert.ChangeType( constructor.Invoke(params_), type);
        }

        private static object GetObj(TomlTable tt, Type type, string? key)
        {
            var value = tt.GetValue(key ?? throw new ArgumentNullException(nameof(key)));
            return ConvertObj(value, type);
        }

        private static object ConvertObj(TomlValue value, Type type)
            => value switch
            {
                TomlString s => ConvertBaseObj(s),
                TomlLong i => ConvertBaseObj(i),
                TomlBoolean b => ConvertBaseObj(b),
                TomlDouble d => ConvertBaseObj(d),
                TomlArray a => ConvertBaseObj(a),
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
                _ => throw new InvalidOperationException("Only TomlString, long ,bool, double *or* objects iwth user-defined TOML->CLR converters allowed")
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