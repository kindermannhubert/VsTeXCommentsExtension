# VsTeXCommentsExtension (Alpha Version)

Intra-text adornment extension to the Visual Studio Editor for rendering TeX math comments inside editor.

Extension is transforming all continuous code blocks where all lines starts with '//' (can be preceeded by white spaces) and the forst line starts with '//tex:' to rendered image where math is rendered by MathJax library. Math in this comments has to be surrounded by $ (for inline math) or $$ signs.

Example:

![alt tag](https://github.com/kindermannhubert/VsTeXCommentsExtension/blob/master/Screenshot1.png)

Source code for screenshot above:
```C#
using System;
using System.Collections.Generic;
 
namespace VsTexCommentsTest
{
    //tex:Class for solving equations $ax^2+bx+c=0$, in real numbers.
    public class QuadraticEquationSolver
    {
        private readonly double a;
        private readonly double b;
        private readonly double c;
 
        public QuadraticEquationSolver(double a, double b, double c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }
 
        public IEnumerable<double> Solve()
        {
            //tex:We compute discriminant $D$ first.
            //$$D=b^2-4ac$$
            //Equation has solutions:
            //$$x_{1,2}=\frac{-b \pm \sqrt{D}}{2a}$$
 
            //tex:$D=b^2-4ac$
            var D = b * b - 4 * a * c;
 
            if (D == 0)
            {
                //tex:If $D=0$, then there is only one solution.
                yield return -b / (2 * a);
            }
            else if (D > 0)
            {
                var d = Math.Sqrt(D);
                yield return (-b + d) / (2 * a);
                yield return (-b - d) / (2 * a);
            }
            else
            {
                //tex:$D<0 \Rightarrow \sqrt{D}\not\in \mathbb{R}$
                throw new InvalidOperationException("Equation does not have any solution in real numbers.");
            }
        }
    }
}

```
