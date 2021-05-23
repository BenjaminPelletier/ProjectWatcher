using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectWatcher
{
    public static class ExtensionMethods
    {
        static Regex DateTimeFinder = new Regex(@"(\d{4})(\d\d)(\d\d)_(\d\d)(\d\d)(\d\d)");

        public static int IndexOfMax<TItem, TValue>(this IEnumerable<TItem> items, Func<TItem, TValue> value) where TValue : IComparable<TValue>
        {
            bool first = true;
            int maxIndex = -1;
            TValue maxValue = default(TValue);
            int index = 0;
            foreach (TItem item in items)
            {
                if (first)
                {
                    maxIndex = 0;
                    maxValue = value(item);
                    first = false;
                }
                else
                {
                    TValue itemValue = value(item);
                    if (itemValue.CompareTo(maxValue) > 0)
                    {
                        maxIndex = index;
                        maxValue = itemValue;
                    }
                }
                index++;
            }
            return maxIndex;
        }

        public static string ToFilename(this DateTime t)
        {
            return t.ToString("yyyyMMdd_HHmmss") + ".jpg";
        }

        public static DateTime? ToDateTime(this string s)
        {
            var m = DateTimeFinder.Match(s);
            if (m.Success)
            {
                return new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), int.Parse(m.Groups[6].Value));
            }
            else
            {
                return null;
            }
        }

        public static IEnumerable<T> As<T>(this ListBox.ObjectCollection items) where T : class
        {
            foreach (object item in items)
            {
                T typedItem = item as T;
                if (typedItem != null) yield return typedItem;
            }
        }
    }
}
