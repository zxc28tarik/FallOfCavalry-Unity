# Religion and Sect Foundation

Religion, Sect, Religious Clique, Faction, House and Character are separate identities. `ReligionId` identifies a broad content-defined tradition. A `SectDefinition` has its own `SectId` and exactly one typed parent `ReligionId`; Character sect affiliation is optional and must match the Character's religion.

`ReligionCampaignState` holds definitions, mutable Character affiliations, profiles, policies and Religious Clique associations. Character affiliation changes pass through `ReligionCommandService`, which verifies the real Character and the Religion/Sect pair. The mutation preserves `CharacterId` and does not change loyalty, relations, Organization membership, Clique membership or other Character state.

City, Faction and Region profiles use typed targets and may contain multiple deterministically ordered components. `RelativePresence` expresses only relative content weight; it is neither an exact historical percentage nor an automatic gameplay modifier.

A Religious Clique remains an Implementation 2 `CliqueState`. Its association names religious context but creates no parallel group model, ownership, soldiers, army or combat bonus.

Religion difference is information, not a cause. `ReligionDifferenceAssessment` deliberately returns no automatic unrest, loyalty, relation, rebellion, war or combat effect. Later systems may react only through explicit policy, violence, institutional, political, economic or event context authorized by their own implementation packages.
