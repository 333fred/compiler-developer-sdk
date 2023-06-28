using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSDK;

public readonly record struct SyntaxNodeOrTokenOrTrivia
{
    private readonly SyntaxNodeOrToken? _nodeOrToken;
    private readonly bool _isLeadingTrivia;
    public SyntaxNode? Node => _nodeOrToken?.AsNode();
    public SyntaxToken? Token => _nodeOrToken?.AsToken();
    public SyntaxTrivia? Trivia { get; }

    public static implicit operator SyntaxNodeOrTokenOrTrivia(SyntaxNode node) => new(node);
    public static implicit operator SyntaxNodeOrTokenOrTrivia(SyntaxToken node) => new(node);
    public static implicit operator SyntaxNodeOrTokenOrTrivia(SyntaxNodeOrToken nodeOrToken) => new(nodeOrToken);

    public SyntaxNodeOrTokenOrTrivia(SyntaxNodeOrToken nodeOrToken)
    {
        _nodeOrToken = nodeOrToken;
        Trivia = null;
        _isLeadingTrivia = false;
    }

    public SyntaxNodeOrTokenOrTrivia(SyntaxTrivia trivia, bool isLeadingTrivia)
    {
        Trivia = trivia;
        _nodeOrToken = null;
        _isLeadingTrivia = isLeadingTrivia;
    }

    public string Kind()
    {
        return _nodeOrToken is { } n
            ? n.Kind().ToString()
            : (_isLeadingTrivia ? "Leading: " : "Trailing: ") + Trivia!.Value.Kind().ToString();
    }

    public bool HasChildren()
    {
        if (_nodeOrToken?.AsNode() is { } node)
        {
            return node.ChildNodesAndTokens().Any();
        }
        else if (_nodeOrToken?.AsToken() is { } token)
        {
            return token.HasLeadingTrivia || token.HasTrailingTrivia;
        }

        // TODO: Structured trivia?
        return false;
    }

    public IEnumerable<SyntaxNodeOrTokenOrTrivia> GetChildren()
    {
        if (_nodeOrToken is not { } nodeOrToken)
        {
            return Array.Empty<SyntaxNodeOrTokenOrTrivia>();
        }

        if (nodeOrToken.AsNode() is { } node)
        {
            return node.ChildNodesAndTokens().Select(s => (SyntaxNodeOrTokenOrTrivia)s);
        }
        else
        {
            var token = nodeOrToken.AsToken();
            return token.LeadingTrivia
                .Select(s => new SyntaxNodeOrTokenOrTrivia(s, isLeadingTrivia: true))
                .Concat(token.TrailingTrivia.Select(s => new SyntaxNodeOrTokenOrTrivia(s, isLeadingTrivia: false)));
        }
    }

    public TextSpan GetSpan()
    {
        return _nodeOrToken is { } nodeOrToken ? nodeOrToken.Span : Trivia!.Value.Span;
    }

    public TextSpan GetFullSpan()
    {
        return _nodeOrToken is { } nodeOrToken ? nodeOrToken.FullSpan : Trivia!.Value.FullSpan;
    }

    public SyntaxNodeOrToken Parent => _nodeOrToken is { Parent: var parent } ? parent : Trivia!.Value.Token;

    public Dictionary<string, string> GetPublicProperties()
    {
        var @object = _nodeOrToken is { } nodeOrToken
            ? (nodeOrToken.AsNode() is { } node ? node : nodeOrToken.AsToken())
            : (object)Trivia!.Value;

        var type = @object.GetType();

        return type.GetProperties().Where(p => p.Name != "Kind" && p.CanRead).ToDictionary(
            static s => s.Name,
            s => s.GetValue(@object) switch
            {
                string str => $"""
                    "{str}"
                    """,
                var val => val?.ToString() ?? "<null>"
            });
    }

    public Type GetUnderlyingType()
    {
        return _nodeOrToken is { } nodeOrToken
            ? nodeOrToken.AsNode()?.GetType() ?? nodeOrToken.AsToken().GetType()
            : Trivia!.Value.GetType();
    }

    public override string ToString()
    {
        string nodeString = _nodeOrToken?.ToString() ?? "<null>";
        string leadingString = Trivia != null ? "" : _isLeadingTrivia ? "Leading " : "Trailing ";
        string triviaString = Trivia?.ToString() ?? "<null>";
        return $"({nodeString}, {leadingString}{triviaString})";
    }
}
