using System.Collections.Generic;
using Service.RouteService;

namespace Scene.Lobby
{
    /// <summary>
    /// Value object that wraps RouteService.CurrentQuery with strong typing on Lobby scene entry.
    ///
    /// <param name="query">RouteService.CurrentQuery. If null, all values use defaults.</param>
    /// Currently a placeholder. Fill in actual parameters when the Lobby scene is added.
    /// Add new parameter keys as constants in <see cref="GameRouteParams"/>.
    /// </param>
    ///
    /// <example>
    /// Navigation example (other scene → Lobby):
    /// <code>
    /// // Simple entry (no parameters)
    /// await RouteService.NavigateAsync("Lobby");
    ///
    /// // Entry with a pre-selected table
    /// await RouteService.NavigateAsync("Lobby", new Dictionary<string, string>
    /// {
    ///     { GameRouteParams.TableId, "table_001" }
    /// });
    /// </code>
    ///
    /// Usage in Presenter:
    /// <code>
    /// var query = new LobbyQuery(RouteService.CurrentQuery);
    ///
    /// if (query.HasPreselectedTable)
    ///     HighlightTable(query.TableId);
    /// </code>
    /// </example>
    /// </summary>
    public class LobbyQuery
    {
        /// <summary>
        /// Table ID pre-selected before entering the scene.
        /// <c>null</c> if the parameter is absent.
        /// </summary>
        public string TableId { get; }

        /// <summary>Whether a pre-selected table exists.</summary>
        public bool HasPreselectedTable => TableId != null;

        /// <summary>
        /// Creates a LobbyQuery from RouteService.CurrentQuery.
        /// </summary>
        /// <param name="query">RouteService.CurrentQuery. If null, all values use defaults.</param>
        public LobbyQuery(IReadOnlyDictionary<string, string> query)
        {
            if (query != null && query.TryGetValue(GameRouteParams.TableId, out var tableId))
                TableId = tableId;
        }
    }
}
