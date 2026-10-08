using System;
using FOC.Domain.Geography;

namespace FOC.Application.Geography
{
    public enum TravelPreviewFailure
    {
        None, ActorUnavailable, Dead, Captive, AlreadyTravelling, OriginUnavailable,
        DestinationUnavailable, AlreadyAtDestination, NoRoute
    }

    /// <summary>Read-only plan derived from the same graph and time policy as Start. Not a reservation or save entity.</summary>
    public sealed class CharacterTravelPreview
    {
        internal CharacterTravelPreview(TravelPreviewFailure failure, WorldLocationDefinition? origin = null,
            WorldLocationDefinition? destination = null, RoutePath? path = null, long durationTicks = 0)
        { Failure = failure; Origin = origin; Destination = destination; Path = path; DurationTicks = durationTicks; }
        public TravelPreviewFailure Failure { get; }
        public bool CanStart => Failure == TravelPreviewFailure.None;
        public WorldLocationDefinition? Origin { get; }
        public WorldLocationDefinition? Destination { get; }
        public RoutePath? Path { get; }
        public long DurationTicks { get; }
    }
}
