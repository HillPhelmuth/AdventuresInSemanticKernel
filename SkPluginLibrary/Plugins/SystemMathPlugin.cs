using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkPluginLibrary.Plugins
{
    public class SystemMathPlugin
    {
        [KernelFunction, Description("Adds two numbers and returns the result.")]
        public static double Add(double x, double y)
        {
            return x + y;
        }

        [KernelFunction, Description("Subtracts the second number from the first and returns the result.")]
        public static double Subtract(double x, double y)
        {
            return x - y;
        }

        [KernelFunction, Description("Multiplies two numbers and returns the result.")]
        public static double Multiply(double x, double y)
        {
            return x * y;
        }

        [KernelFunction, Description("Divides the first number by the second and returns the result.")]
        public static double Divide(double x, double y)
        {
            if (y == 0)
            {
                throw new System.DivideByZeroException("Cannot divide by zero.");
            }
            return x / y;
        }
        [KernelFunction, Description("Returns the absolute value of a double-precision floating-point number.")]
        public static double Abs(double value) => Math.Abs(value);

        [KernelFunction, Description("Returns the cube root of a specified number.")]
        public static double Cbrt(double value) => Math.Cbrt(value);

        [KernelFunction, Description("Produces the quotient and the remainder of two signed 32-bit numbers.")]
        public static (int Quotient, int Remainder) DivRem(int a, int b) => (Math.DivRem(a, b, out var remainder), remainder);

        [KernelFunction, Description("Returns e raised to the specified power.")]
        public static double Exp(double value) => Math.Exp(value);

        [KernelFunction, Description("Returns the base 2 integer logarithm of a specified number.")]
        public static int ILogB(double value) => Math.ILogB(value);

        [KernelFunction, Description("Returns the natural (base e) logarithm of a specified number.")]
        public static double Log(double value) => Math.Log(value);

        [KernelFunction, Description("Returns the base 10 logarithm of a specified number.")]
        public static double Log10(double value) => Math.Log10(value);

        [KernelFunction, Description("Returns the base 2 logarithm of a specified number.")]
        public static double Log2(double value) => Math.Log2(value);

        [KernelFunction, Description("Returns a specified number raised to the specified power.")]
        public static double Pow(double x, double y) => Math.Pow(x, y);

        [KernelFunction, Description("Returns the square root of a specified number.")]
        public static double Sqrt(double value) => Math.Sqrt(value);
    }
}
