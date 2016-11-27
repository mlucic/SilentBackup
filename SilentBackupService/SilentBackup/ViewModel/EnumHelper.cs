using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilentBackup.Classes
{


    public class ValueDescription
    {
        public Enum Value { get; set; }
        public string Description { get; set; }
    }

    public static class EnumHelper
    {
        public static string Description(this Enum eValue)
        {
            var nAttributes = eValue.GetType().GetField(eValue.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (nAttributes.Any())
                return (nAttributes.First() as DescriptionAttribute).Description;

            // If no description is found, the least we can do is replace underscores with spaces
            TextInfo oTI = CultureInfo.CurrentCulture.TextInfo;
            return oTI.ToTitleCase(oTI.ToLower(eValue.ToString().Replace("_", " ")));
        }

        public static IEnumerable<ValueDescription> GetAllValuesAndDescriptions<T>() where T : struct, IConvertible, IComparable, IFormattable
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an enum type");

            return Enum.GetValues(typeof(T)).Cast<Enum>().Select((e) => new ValueDescription() { Value = e, Description = e.Description() }).ToList();
        }
    }
}
