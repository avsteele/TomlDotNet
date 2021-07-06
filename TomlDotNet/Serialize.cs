using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;

using Tomlet.Models;

namespace TomlDotNet
{
    public static class Serialize
    {
        /// <summary>
        /// See RecordToTomlString. Simply writes that toml string to a file.
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
        /// Converts a CLR record (class) object into a TOML string.
        /// Method: It looks for a constructor on type T.  It uses the The parameter names in 
        /// the constructors to dtermine the values added to the TOML. This works for for
        /// default-made record constructors e.g.: 'public record Data(int I, string S, ...);'
        /// It works for other types on the condition that each constructor parameter name 
        /// corresponds to a properties on the object to be serialized
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

            //iterate over constructor's parameters
            var constructor = SelectConstructor(t);
            var param_list = constructor.GetParameters();

            foreach (var p in param_list)
            {
                if (p.ParameterType.Equals(t))
                    throw new InvalidOperationException("Cannot serialize types with constructors containing an parameter of thier own type");
                if (p.Name is null) throw new InvalidOperationException($"Constructor parameter name {p.Name} is null?");
                var obj = GetPropValue(data, p.Name);
                if (obj is null) throw new InvalidOperationException($"Unexpected property {p.Name}'s value not found on object");
                var tomlValue = ToTomlBase(obj, p.ParameterType);
                tt.PutValue(p.Name, tomlValue);
                //if (tomlValue is not null)
                //    tt.PutValue(p.Name, tomlValue);
                //else
                //    tt.PutValue(p.Name, RecordToToml(obj, p.ParameterType));
            }
            return tt;
        }

        private static TomlValue ToTomlBase(object data, Type targetType) => data switch
        {
            bool b => TomlBoolean.ValueOf(b),
            double _ or float _ => new TomlDouble(Convert.ToDouble(data)),
            long _ or int _ or uint _ or short _ or ushort _ or byte _ => new TomlLong(Convert.ToInt64(data)),
            string s => new TomlString(s),
            DateTime dt => new TomlLocalDateTime(dt),
            DateTimeOffset dto => new TomlOffsetDateTime(dto),
            IEnumerable e => MakeTomlArray(e, targetType),
            _ => RecordToToml(data, targetType) // anything else we try to make a table out of
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        /// <param name="targetType">use only in the case that we have an array of records</param>
        /// <returns></returns>
        private static TomlArray MakeTomlArray(IEnumerable e, Type targetType)
        {
            if (e.GetType().GenericTypeArguments.Length == 0) throw new InvalidOperationException("Unable to convert enumerbale to tomlarray");

            var listType = typeof(List<>);
            var elementType = e.GetType().GenericTypeArguments[0]; // for heterog this might be 'object'
            var constructedListType = listType.MakeGenericType(elementType);

            List<TomlValue> tomlValues = (from object el in e select ToTomlBase(el, elementType)).ToList();
            var a = new TomlArray();
            
            var p = typeof(TomlArray).GetField("ArrayValues");
            p?.SetValue(a, tomlValues);  // jenky, but I can't find any other way to corectly construct a TomlArray

            if(tomlValues.Count>0 && tomlValues[0] is TomlTable)
            {
                var pIsTable = typeof(TomlArray).GetField("IsTableArray", BindingFlags.NonPublic| BindingFlags.Instance);
                pIsTable?.SetValue(a, true);
            }

            return a;
        }

        /// <summary>
        /// Returns the object returned by src.propName's 'getter'
        /// </summary>
        /// <param name="src"></param>
        /// <param name="propName"></param>
        /// <returns></returns>
        public static object? GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName)?.GetValue(src, null);
        }

        /// <summary>
        /// Selects a constructor on type t that is used for serialization.
        /// Default behavior is to choose 1st one returned by Deserialize
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static ConstructorInfo SelectConstructor(Type t)
        {
            return Deserialize.ConstructorTryOrder(t, null)[0];
        }
    }
}
