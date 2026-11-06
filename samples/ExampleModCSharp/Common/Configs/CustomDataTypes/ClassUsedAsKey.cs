using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.CustomDataTypes
{
    public class ClassUsedAsKeyConverter : ToFromStringConverter<ClassUsedAsKey> {
    }

    [TypeConverter("ExampleMod.Common.Configs.CustomDataTypes.ClassUsedAsKeyConverter")]

    public class ClassUsedAsKey
    {
        public bool SomeBool { get; set; }
        public int SomeNumber { get; set; }

        public override bool Equals(object obj) {
            if (obj is ClassUsedAsKey other)
                return SomeBool == other.SomeBool && SomeNumber == other.SomeNumber;
            return base.Equals(obj);
        }

        public override int GetHashCode() {
            return new { SomeBool, SomeNumber }.GetHashCode();
        }

        public override string ToString() {
            return $"{SomeBool}, {SomeNumber}";
        }

        public static ClassUsedAsKey FromString(string s) {
            string[] vars = s.Split(new char[] { ',' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (vars.Length != 2)
                throw new Exception($"Invalid input string {s}");

            return new ClassUsedAsKey {
                SomeBool = Convert.ToBoolean(vars[0]),
                SomeNumber = Convert.ToInt32(vars[1])
            };
        }
    }
}
