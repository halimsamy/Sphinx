using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;

namespace Sphinx
{
    internal class ContextDependencyResolver
    {
        public static List<Context> Sort(IList<Context> contexts)
        {
            var edges = new List<DependencyGraphEdge>();
            var roots = new HashSet<Context>(contexts);
            var asmMap = contexts
                .GroupBy(ctx => ctx.Module.Assembly.ToAssemblyRef(), AssemblyNameComparer.CompareAll)
                .ToDictionary(gp => gp.Key, gp => gp.ToList(), AssemblyNameComparer.CompareAll);

            foreach (var ctx in contexts)
            foreach (var nameRef in ctx.Module.GetAssemblyRefs())
            {
                if (!asmMap.ContainsKey(nameRef))
                    continue;

                edges.AddRange(asmMap[nameRef].Select(asmModule => new DependencyGraphEdge(asmModule, ctx)));
                roots.Remove(ctx);
            }

            var sorted = SortGraph(roots, edges).ToList();
            Debug.Assert(sorted.Count == contexts.Count);
            return sorted;
        }

        private static IEnumerable<Context> SortGraph(IEnumerable<Context> roots, IList<DependencyGraphEdge> edges)
        {
            var visited = new HashSet<Context>();
            var queue = new Queue<Context>(roots);
            do
            {
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    visited.Add(node);

                    Debug.Assert(edges.All(edge => edge.To.Module != node.Module));
                    yield return node;

                    foreach (var edge in edges.Where(edge => edge.From.Module == node.Module).ToList())
                    {
                        edges.Remove(edge);
                        if (edges.All(e => e.To.Module != edge.To.Module))
                            queue.Enqueue(edge.To);
                    }
                }

                if (edges.Count <= 0) continue;

                foreach (var edge in edges)
                    if (!visited.Contains(edge.From))
                    {
                        queue.Enqueue(edge.From);
                        break;
                    }
            } while (edges.Count > 0);
        }

        private class DependencyGraphEdge
        {
            public DependencyGraphEdge(Context from, Context to)
            {
                this.From = from;
                this.To = to;
            }

            public Context From { get; }
            public Context To { get; }
        }
    }
}