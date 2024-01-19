using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkPluginLibrary.Models.Helpers;

public static class TypeHelpers
{
    public static bool IsNumericType(this Type type)
    {
        Type[] numericTypes =
        [
            typeof(byte), typeof(sbyte),
            typeof(short), typeof(ushort),
            typeof(int), typeof(uint),
            typeof(long), typeof(ulong),
            typeof(float), typeof(double),
            typeof(decimal)
        ];
        return numericTypes.Contains(type);
    }
    public static bool IsCollectionType(this Type type)
    {
        return typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
               && type != typeof(string);
    }
}