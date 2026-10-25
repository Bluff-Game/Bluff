using System.Runtime.CompilerServices;

// Local development tooling and tests (not part of the repository) drive rooms through internal APIs.
[assembly: InternalsVisibleTo("Bluff.Dev")]
[assembly: InternalsVisibleTo("Bluff.Dev.Editor")]
[assembly: InternalsVisibleTo("Bluff.Tests")]
