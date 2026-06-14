using System.Runtime.CompilerServices;

// Expose internal members (solver stepping hooks) to the EditMode test assembly.
[assembly: InternalsVisibleTo("Tests.EditMode")]
