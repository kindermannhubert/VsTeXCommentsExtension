using System;
using System.Diagnostics;

namespace VsTeXCommentsExtension
{
    [DebuggerDisplay("FirstLine: {FirstLine}, LastLine: {LastLine}")]
    public struct LineSpan : IEquatable<LineSpan>
    {
        public readonly int FirstLine;
        public readonly int LastLine;

        public int Count => LastLine - FirstLine + 1;

        public LineSpan(int firstLine, int lastLine)
        {
            FirstLine = firstLine;
            LastLine = lastLine;
        }

        public bool Equals(LineSpan other) => this == other;

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj is LineSpan)
            {
                return this == (LineSpan)obj;
            }
            return false;
        }

        public override int GetHashCode() => FirstLine ^ LastLine;

        public static bool operator ==(LineSpan a, LineSpan b) => a.FirstLine == b.FirstLine && a.LastLine == b.LastLine;

        public static bool operator !=(LineSpan a, LineSpan b) => a.FirstLine != b.FirstLine || a.LastLine != b.LastLine;
    }
}
