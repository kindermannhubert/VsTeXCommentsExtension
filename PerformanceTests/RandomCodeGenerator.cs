using System;
using System.Text;

namespace PerformanceTests
{
    internal static class RandomCodeGenerator
    {
        private static readonly Random rand = new Random(0);

        private static readonly string[] TeXComments = new[]
        {
            @"//tex:$x^@$",

            @"//tex:$$\frac{x+@}{@+y}$$",

            @"//tex:Formula 1:
//$$\sum_{i=@}^{\infty}{\frac{@}{i!}}$$",

            @"//tex:$$\int{x^{@}}dx$$",

            @"//tex:
//$$
//a+
//b+
//c=
//@
//$$",

            @"//tex: intergrals:
//$$\int{x^{@}}dx + $$
//$$\int{y^{@}}dx + $$
//$$\int{z^{@}}dx = $$
//$$\int{x^{@}+y^{@}+z^{@}}dx$$",
        };

        public static string GenerateCode(int segmentsCount)
        {
            var sb = new StringBuilder();

            int indent = 0;
            for (int i = 0; i < segmentsCount; i++)
            {
                AppendLine($"//some class blah blah");
                AppendLine($"public static class Class{rand.Next()}");
                AppendLine("{");
                ++indent;

                AppendLine($"//some method blah blah");
                AppendLine($"public static void Method{rand.Next()}()");
                AppendLine("{");
                ++indent;
                AppendLine($"//some comment blah blah");
                AppendRandomTeXComment();
                --indent;
                AppendLine("}");
                AppendLine("");

                --indent;
                AppendLine("}");
                AppendLine("");
            }

            void AppendLine(string line)
            {
                Indent();
                sb.AppendLine(line);
            }

            void Indent()
            {
                for (int i = 0; i < 4 * indent; i++) sb.Append(' ');
            }

            void AppendRandomTeXComment()
            {
                var commentLines = TeXComments[rand.Next(TeXComments.Length)].Replace("@", rand.Next().ToString()).Split(new[] { "\r\n" }, StringSplitOptions.None);
                foreach (var line in commentLines) AppendLine(line);
            }

            return sb.ToString();
        }
    }
}