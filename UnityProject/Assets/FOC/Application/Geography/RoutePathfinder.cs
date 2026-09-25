using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Geography;

namespace FOC.Application.Geography
{
    public sealed class RoutePath
    {
        public RoutePath(IEnumerable<TravelRouteId> routes, long totalDistanceMeters) { Routes = (routes ?? throw new ArgumentNullException(nameof(routes))).ToList().AsReadOnly(); if (Routes.Count == 0 || totalDistanceMeters <= 0) throw new ArgumentException("Route path is empty or has no distance."); TotalDistanceMeters = totalDistanceMeters; }
        public IReadOnlyList<TravelRouteId> Routes { get; }
        public long TotalDistanceMeters { get; }
    }

    /// <summary>Integer-cost Dijkstra. Equal-cost paths resolve by the ordinal route-ID signature.</summary>
    public sealed class DeterministicRoutePathfinder
    {
        private sealed class Candidate
        {
            public Candidate(WorldLocationId location, long cost, List<TravelRouteId> path) { Location = location; Cost = cost; Path = path; Signature = string.Join("\n", path.Select(x => x.Value)); }
            public WorldLocationId Location { get; }
            public long Cost { get; }
            public List<TravelRouteId> Path { get; }
            public string Signature { get; }
        }

        public RoutePath Find(WorldGeography world, WorldLocationId origin, WorldLocationId destination)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.Location(origin); world.Location(destination);
            if (origin.Equals(destination)) throw new InvalidOperationException("Origin and destination must differ.");
            var pending = new List<Candidate> { new Candidate(origin, 0, new List<TravelRouteId>()) };
            var best = new Dictionary<WorldLocationId, Candidate>();
            while (pending.Count > 0)
            {
                pending.Sort(Compare);
                var current = pending[0]; pending.RemoveAt(0);
                if (best.TryGetValue(current.Location, out var known) && Compare(current, known) >= 0) continue;
                best[current.Location] = current;
                if (current.Location.Equals(destination)) return new RoutePath(current.Path, current.Cost);
                foreach (var route in world.RoutesFrom(current.Location))
                {
                    var nextLocation = route.Other(current.Location);
                    var nextPath = new List<TravelRouteId>(current.Path) { route.Id };
                    pending.Add(new Candidate(nextLocation, checked(current.Cost + route.DistanceMeters), nextPath));
                }
            }
            throw new InvalidOperationException("No travel path connects the requested locations.");
        }

        private static int Compare(Candidate x, Candidate y)
        {
            var c = x.Cost.CompareTo(y.Cost); if (c != 0) return c;
            c = StringComparer.Ordinal.Compare(x.Signature, y.Signature); if (c != 0) return c;
            return x.Location.CompareTo(y.Location);
        }
    }
}
