# VsTeXCommentsExtension

Intra-text adornment extension to the Visual Studio Editor for rendering TeX math comments inside editor.

Supports C#, F#, C, C++, VB.NET, MPL, Python, R, D and Fortran.

Extension is transforming all continuous code blocks where all lines starts with single line comment syntax (can be preceeded by white spaces) which for example in C# is '//' and the first line starts with '//tex:' prefix ('//' part is language dependent) to rendered image where math is rendered by [MathJax](https://www.mathjax.org/). Math in this comments has to be surrounded by $ (for inline math) or $$ signs. Syntax of math is LaTex.

Examples:

- C#, F#, C, C++, D:

```C#
  //tex:
  //Formula 1: $$(a+b)^2 = a^2 + 2ab + b^2$$
  //Formula 2: $$a^2-b^2 = (a+b)(a-b)$$
```

- MPL, Python, R:

```Python
  #tex:
  #Formula 1: $$(a+b)^2 = a^2 + 2ab + b^2$$
  #Formula 2: $$a^2-b^2 = (a+b)(a-b)$$
```

- VB.NET:

```VB
  'tex:
  'Formula 1: $$(a+b)^2 = a^2 + 2ab + b^2$$
  'Formula 2: $$a^2-b^2 = (a+b)(a-b)$$
```

- Fortran:

```fortran
  !tex:
  !Formula 1: $$(a+b)^2 = a^2 + 2ab + b^2$$
  !Formula 2: $$a^2-b^2 = (a+b)(a-b)$$
```

### Example of C# code:

![alt tag](https://github.com/kindermannhubert/VsTeXCommentsExtension/blob/master/Screenshot1.png)

Source code for screenshot above looks like this:
![alt tag](https://github.com/kindermannhubert/VsTeXCommentsExtension/blob/master/Screenshot2.png)
