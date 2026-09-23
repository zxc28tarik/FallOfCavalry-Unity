# AI Utility and Priority

Eligibility is a hard gate, never a magic negative score. Only legal candidates reach utility policies. Each result retains ordered `AIUtilityContribution` records with factor ID, deterministic integer value, reason key, and optional evidence report.

Faction priorities and Character decision profiles add data-driven adjustments to named factors. They alter preference among legal candidates only; they cannot create actions, bypass authority, modify loyalty, invent betrayal, or turn religious difference into hostility.

Candidate, contribution, profile, and policy identities are stable typed IDs. Candidate enumeration and contributions are canonicalized. Equal total utility is resolved by stable candidate ID. There is no `System.Random`, `UnityEngine.Random`, wall-clock time, collection-order tie-break, faction-name branch, or frame callback in the AI source.

`ProofAIContent` exists only to prove composition. Its IDs and content keys contain `proof-only` / `PROOF_ONLY`; its numbers are not production balance authority.
