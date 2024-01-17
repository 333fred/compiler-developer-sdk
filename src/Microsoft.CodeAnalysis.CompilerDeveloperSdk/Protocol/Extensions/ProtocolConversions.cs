// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Text;

using LSP = Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer
{
    internal static partial class ProtocolConversions
    {
        public static LinePosition PositionToLinePosition(LSP.Position position)
            => new LinePosition(position.Line, position.Character);
        public static LinePositionSpan RangeToLinePositionSpan(LSP.Range range)
            => new(PositionToLinePosition(range.Start), PositionToLinePosition(range.End));

        public static TextSpan RangeToTextSpan(LSP.Range range, SourceText text)
        {
            var linePositionSpan = RangeToLinePositionSpan(range);

            try
            {
                try
                {
                    return text.Lines.GetTextSpan(linePositionSpan);
                }
                catch (ArgumentException ex)
                {
                    // Create a custom error for this so we can examine the data we're getting.
                    throw new ArgumentException($"Range={RangeToString(range)}. text.Length={text.Length}. text.Lines.Count={text.Lines.Count}", ex);
                }
            }
            catch
            {
                throw;
            }

            static string RangeToString(LSP.Range range)
                => $"{{ Start={PositionToString(range.Start)}, End={PositionToString(range.End)} }}";

            static string PositionToString(LSP.Position position)
                => $"{{ Line={position.Line}, Character={position.Character} }}";
        }

        public static LSP.Position LinePositionToPosition(LinePosition linePosition)
            => new LSP.Position { Line = linePosition.Line, Character = linePosition.Character };

        public static LSP.Range LinePositionToRange(LinePositionSpan linePositionSpan)
            => new LSP.Range { Start = LinePositionToPosition(linePositionSpan.Start), End = LinePositionToPosition(linePositionSpan.End) };

        public static LSP.Range TextSpanToRange(TextSpan textSpan, SourceText text)
        {
            var linePosSpan = text.Lines.GetLinePositionSpan(textSpan);
            return LinePositionToRange(linePosSpan);
        }
    }
}
