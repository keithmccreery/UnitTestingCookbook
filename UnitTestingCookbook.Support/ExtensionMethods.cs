namespace UnitTestingCookbook.Support;

/// <summary>
/// Extension Methods
/// </summary>
public static class ExtensionMethods
{
    /// <summary>
    /// Split and Keep
    /// </summary>
    /// <param name="this"></param>
    /// <param name="delimiters"></param>
    /// <returns></returns>
    public static IEnumerable<string> SplitAndKeep( this string @this, char[] delimiters )
    {
        ArgumentNullException.ThrowIfNull( @this );
        ArgumentNullException.ThrowIfNull( delimiters );

        return SplitAndKeepInner( @this, delimiters );

        // Inner
        IEnumerable<string> SplitAndKeepInner( string @this, char[] delimiters )
        {
            int start = 0;
            int index;

            while ( ( index = @this.IndexOfAny( delimiters, start ) ) != -1 )
            {
                if ( index - start > 0 )
                    yield return @this[ start..index ]; // word
                yield return @this[ index ].ToString(); // delimiter
                start = index + 1;
            }
            if ( start < @this.Length )
                yield return @this[ start.. ]; // last word

        }
    }
}
