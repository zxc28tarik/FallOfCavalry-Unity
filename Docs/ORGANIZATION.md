# Organization

Organization is the actor's human network; it is not a Character subtype. Its only top-level branches are Party, Household, Army, Settlements, Estates, Production, Trade and Diplomacy. SpecialAgents, CityAdministration, Caravan and Retinue are deliberately not top-level branches.

`OrganizationMembershipState` records a Character, branch, membership type, start time and active state. Membership does not contain or mutate loyalty, authority, assignment or physical location. A Character may hold memberships in several branches.

`AssignmentState` is independently identified and records a controlled role code, typed discriminated target, authority, presence requirement, start time and status. The five authority values describe decision scope, not rank, role, loyalty or membership. Remote-capable assignments do not imply a communication simulation. Manager, Deputy and FullAuthority are future autonomy hooks only.

Multiple assignments are allowed. Active physical-presence assignments must share the same target, and a target that maps to City, Army, Caravan or world position must agree with the Character's authoritative `CharacterLocation`. Dead Characters cannot receive active assignments and captive Characters cannot receive physical-presence assignments.

Army, Trade, Settlement and other branch names are only organizational boundaries in this package. They do not create those later gameplay domains.
