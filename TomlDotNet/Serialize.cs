using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Tomlet.Models;

namespace TomlDotNet
{
    public static class Serialize
    {
        /// <summary>
        /// Converts a CLR record (class) object into a TOML string and writes it to a file. 
        /// Method: It looks for a constructor on type T.  It uses the The parameter names in 
        /// the constructors to dtermine the values added to the TOML. This works for for
        /// default-made record constructors e.g.: 'public record Data(int I, string S, ...);'
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="filename"></param>
        public static void RecordToTomlFile<T>(T data, string filename) where T : notnull
        {
            var str = RecordToTomlString(data);
            System.IO.File.WriteAllText(filename, str);
        }

        /// <summary>
        /// See RecordToTomlFile. THis method just lets you pass in the toml file as a string directly
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string RecordToTomlString<T>(T data) where T : notnull
        {
            var table = RecordToToml(data, typeof(T));
            return table.SerializeNonInlineTable(null, false);
        }

        private static TomlTable RecordToToml(object data, Type t)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            var tt = new Tomlet.Models.TomlTable();

            //iterate over constructor
            var constructor = Deserialize.SelectConstructor(t);
            var param_list = constructor.GetParameters();

            foreach (var p in param_list)
            {
                if (p.ParameterType.Equals(t))
                    throw new InvalidOperationException("Cannot types with constructors containing an element of thier own type");
                if (p.Name is null) throw new InvalidOperationException($"Constructor parameter name {p.Name} is null?");
                var obj = GetPropValue(data, p.Name);
                if (obj is null) throw new InvalidOperationException($"Unexpected property {p.Name}'s value not found on object");
                var tomlValue = ToTomlBase(obj);
                if (tomlValue is not null)
                    tt.PutValue(p.Name, tomlValue);
                else
                    RecordToToml(obj, p.ParameterType);
            }
            return tt;
        }

        private static TomlValue? ToTomlBase(object data) => data switch
        {
            bool b => TomlBoolean.ValueOf(b),
            double _ or float _ => new TomlDouble(Convert.ToDouble(data)),
            long _ or int _ or uint _ or short _ or ushort _ or byte _ => new TomlLong(Convert.ToInt64(data)),
            string s => new TomlString(s),
            DateTime dt => new TomlLocalDateTime(dt),
            DateTimeOffset dto => new TomlOffsetDateTime(dto),
            _ => throw new InvalidOperationException($"No known conversion from {data.GetType()} to Toml"),
        };

        /// <summary>
        /// Resturns the object returned by src.propName's 'getter'
        /// </summary>
        /// <param name="src"></param>
        /// <param name="propName"></param>
        /// <returns></returns>
        public static object? GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName)?.GetValue(src, null);
        }

    }
}
