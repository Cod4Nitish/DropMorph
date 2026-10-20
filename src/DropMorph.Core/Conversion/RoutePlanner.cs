// Finds the cheapest chain of converters from a source format to a target format.
//
// Why a graph search: engines only know direct conversions (edges). Many useful conversions need
// two hops, e.g. HWPX → PDF (rhwp) → PNG (PDFium). Treating formats as nodes and converter edges as
// weighted arcs lets new engines automatically unlock new routes without touching any other code.

using DropMorph.Core.Formats;

namespace DropMorph.Core.Conversion;

/// <summary>One hop of a route.</summary>
/// <param name="FrameSequence">The hop saves every frame of a video (<see cref="ConversionEdge.FrameSequence"/>).</param>
/// <param name="Unshaped">The hop draws text without complex-script shaping (<see cref="ConversionEdge.Unshaped"/>).</param>
public sealed record RouteStep(IConverter Converter, string From, string To, bool FrameSequence = false, bool Unshaped = false);

/// <summary>A planned conversion from one format to another.</summary>
public sealed record Route(IReadOnlyList<RouteStep> Steps, int Cost)
{
    /// <summary>Index of the first step that turns and mirrors images (<see cref="IImageTransformer"/>), or -1.</summary>
    public int FirstTransformStep()
    {
        for (var i = 0; i < Steps.Count; i++)
        {
            if (Steps[i].Converter is IImageTransformer transformer && transformer.Transforms(Steps[i].From, Steps[i].To))
                return i;
        }
        return -1;
    }
}

/// <summary>Plans conversion routes over the available converters.</summary>
public sealed class RoutePlanner
{
    /// <summary>Maximum number of hops. Longer chains are almost always lossy and slow.</summary>
    public const int MaxSteps = 3;

    /// <summary>Every edge with its cost (priority penalty included), by source format.</summary>
    private readonly Dictionary<string, List<(IConverter Converter, ConversionEdge Edge, int Cost)>> _edgesFrom;

    /// <param name="converters">Converters that are currently available.</param>
    /// <param name="priorityPenalty">
    /// Extra cost per step for a converter, derived from the user's engine priority order.
    /// Keep it small (0..9) so priority breaks ties between engines without making routes longer.
    /// </param>
    public RoutePlanner(IEnumerable<IConverter> converters, Func<IConverter, int>? priorityPenalty = null)
    {
        var penalty = priorityPenalty ?? (_ => 0);
        _edgesFrom = converters
            .SelectMany(c => c.Edges.Select(e =>
                (c, e with { From = e.From.ToLowerInvariant(), To = e.To.ToLowerInvariant() }, e.Cost + penalty(c))))
            .GroupBy(e => e.Item2.From)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>Returns the cheapest route, or <c>null</c> if the conversion is impossible.</summary>
    /// <param name="frames">
    /// Save every frame of a video (<see cref="Presets.Preset.SavesFramesOf"/>): the route is one
    /// <see cref="ConversionEdge.FrameSequence"/> edge from <paramref name="from"/> to <paramref name="to"/>, or none.
    /// Without it those edges are never taken.
    /// </param>
    /// <param name="shaping">
    /// The source has text that needs complex-script shaping: <see cref="ConversionEdge.Unshaped"/> edges are left out.
    /// </param>
    public Route? Plan(string from, string to, bool frames = false, bool shaping = false)
    {
        from = from.ToLowerInvariant();
        to = to.ToLowerInvariant();

        if (frames)
        {
            var sequence = EdgesFrom(from)
                .Where(e => e.Edge.FrameSequence && e.Edge.To == to)
                .OrderBy(e => e.Cost)
                .FirstOrDefault();
            return sequence.Converter is null
                ? null
                : new Route([new RouteStep(sequence.Converter, from, to, FrameSequence: true)], sequence.Cost);
        }

        // Same-format "conversions" (re-encode, split, compress) must use an explicit self edge.
        if (from == to)
        {
            var best = EdgesFrom(from)
                .Where(e => e.Edge.To == to && !e.Edge.FrameSequence)
                .OrderBy(e => e.Cost)
                .FirstOrDefault();
            return best.Converter is null
                ? null
                : new Route([new RouteStep(best.Converter, from, to)], best.Cost);
        }

        // Edges for raster images only (tracing) are left out unless the file itself is one.
        var imageSource = FormatRegistry.FindById(from)?.Category == FormatCategory.Image;

        // Dijkstra over formats. The graph is small (a few hundred nodes).
        var dist = new Dictionary<string, int> { [from] = 0 };
        var prev = new Dictionary<string, (string Node, RouteStep Step)>();
        var hops = new Dictionary<string, int> { [from] = 0 };
        var queue = new PriorityQueue<string, int>();
        queue.Enqueue(from, 0);

        while (queue.TryDequeue(out var node, out var cost))
        {
            if (cost > dist[node])
                continue; // stale queue entry
            if (node == to)
                break;
            if (hops[node] >= MaxSteps)
                continue;

            foreach (var (converter, edge, edgeCost) in EdgesFrom(node))
            {
                if (edge.To == edge.From || edge.FrameSequence
                    || (edge.ImageSourcesOnly && !imageSource) || (edge.NotForImageSources && imageSource)
                    || (edge.Unshaped && shaping)
                    || (edge.DirectOnly && (node != from || edge.To != to)))
                    continue;
                var next = cost + edgeCost;
                if (dist.TryGetValue(edge.To, out var known) && known <= next)
                    continue;

                dist[edge.To] = next;
                hops[edge.To] = hops[node] + 1;
                prev[edge.To] = (node, new RouteStep(converter, edge.From, edge.To, Unshaped: edge.Unshaped));
                queue.Enqueue(edge.To, next);
            }
        }

        if (!prev.ContainsKey(to))
            return null;

        var steps = new List<RouteStep>();
        for (var node = to; node != from; node = prev[node].Node)
            steps.Add(prev[node].Step);
        steps.Reverse();
        return new Route(steps, dist[to]);
    }

    /// <summary>True if <paramref name="from"/> can be converted to <paramref name="to"/>.</summary>
    /// <param name="frames">Every frame of a video as pictures (see <see cref="Plan"/>).</param>
    public bool CanConvert(string from, string to, bool frames = false) => Plan(from, to, frames) is not null;

    private IReadOnlyList<(IConverter Converter, ConversionEdge Edge, int Cost)> EdgesFrom(string format) =>
        _edgesFrom.TryGetValue(format, out var edges) ? edges : [];
}
