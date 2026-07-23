# Support Organization Restoration Plugin

Restores the PC client's **Support Organization** (`Faction`) menu in offline mode.

Recovered client-side components:

- seven organizations: Chlorophyl Forum, Chamber of Pioneer, The Firm, The Committee, LAMA, Rescue TF and Sub Story;
- organization summary, friendship point/level state and history/talk data;
- organization missions and their shuffle state;
- support requests, costs, required items, fixed/random rewards and cooldowns.

The original offline server has no handlers for these messages. Version 0.1.0 supplies:

- persisted friendship points for all seven organizations;
- a valid empty mission snapshot so `FactionSystem` finishes initialization;
- one repeatable, zero-cost test support request per organization;
- proper request/reply messages and cooldown state.

This first restoration stage does not synthesize the original live-service mission rotation or paid rewards.
