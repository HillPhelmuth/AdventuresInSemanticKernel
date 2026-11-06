namespace SkPluginLibrary.Plugins.NativePlugins
{
    public class SystemMathPlugin
    {
        [Description("Adds two numbers and returns the result.")]
        public static double Add(double x, double y)
        {
            return x + y;
        }

        [Description("Subtracts the second number from the first and returns the result.")]
        public static double Subtract(double x, double y)
        {
            return x - y;
        }

        [Description("Multiplies two numbers and returns the result.")]
        public static double Multiply(double x, double y)
        {
            return x * y;
        }

        [Description("Divides the first number by the second and returns the result.")]
        public static double Divide(double x, double y)
        {
            if (y == 0)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }
            return x / y;
        }
        [Description("Returns the absolute value of a double-precision floating-point number.")]
        public static double Abs(double value) => Math.Abs(value);

        [Description("Returns the cube root of a specified number.")]
        public static double Cbrt(double value) => Math.Cbrt(value);

        [Description("Produces the quotient and the remainder of two signed 32-bit numbers.")]
        public static (int Quotient, int Remainder) DivRem(int a, int b) => (Math.DivRem(a, b, out var remainder), remainder);

        [Description("Returns e raised to the specified power.")]
        public static double Exp(double value) => Math.Exp(value);

        [Description("Returns the base 2 integer logarithm of a specified number.")]
        public static int ILogB(double value) => Math.ILogB(value);

        [Description("Returns the natural (base e) logarithm of a specified number.")]
        public static double Log(double value) => Math.Log(value);

        [Description("Returns the base 10 logarithm of a specified number.")]
        public static double Log10(double value) => Math.Log10(value);

        [Description("Returns the base 2 logarithm of a specified number.")]
        public static double Log2(double value) => Math.Log2(value);

        [Description("Returns a specified number raised to the specified power.")]
        public static double Pow(double x, double y) => Math.Pow(x, y);

        [Description("Returns the square root of a specified number.")]
        public static double Sqrt(double value) => Math.Sqrt(value);
    }
}
