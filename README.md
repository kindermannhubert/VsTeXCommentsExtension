# VsTeXCommentsExtension (Beta Version)

Intra-text adornment extension to the Visual Studio Editor for rendering TeX math comments inside editor.

Extension is transforming all continuous code blocks where all lines starts with '//' (can be preceeded by white spaces) and the forst line starts with '//tex:' to rendered image where math is rendered by MathJax library. Math in this comments has to be surrounded by $ (for inline math) or $$ signs.

Example:

![alt tag](https://github.com/kindermannhubert/VsTeXCommentsExtension/blob/master/Screenshot1.png)

Source code for screenshot above looks like this:
![alt tag](https://github.com/kindermannhubert/VsTeXCommentsExtension/blob/master/Screenshot2.png)
