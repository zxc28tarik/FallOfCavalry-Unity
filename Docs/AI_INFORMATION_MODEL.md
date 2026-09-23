# AI Information Model

`AI Perception != World Truth` and `AIDecisionContext != CampaignRuntimeState`.

The context contains only own facts explicitly derived from the controlled Character or supplied by an authorized owning subsystem, plus foreign observations from reports already present in the relevant `ActorInformationState`.

Undelivered reports are absent. A delivered observation carries report ID, subject, observed/arrived timestamps, quality, precision, detail, value/range/qualitative payload, and calculable staleness. Policies may interpret this metadata; the core invents no universal staleness penalty and never creates false world state.

The anti-omniscience mutation test changes hidden foreign truth without changing ActorInformation and obtains the same context/decision. Delivering a real report then makes the observation available. Difficulty profiles receive identical information; higher quality does not unlock world truth.

Battle observation follows the same architectural rule. Proof tests inject an explicit observation context and show that an unrelated hidden campaign mutation cannot alter the tactical proposal.
