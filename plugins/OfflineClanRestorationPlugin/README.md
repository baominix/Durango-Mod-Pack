# Offline Clan Restoration Plugin

Restores the Original Clan screens with a local persistent clan model. The
plugin keeps the client UI and `ClanSystem` data model, replacing only calls
that previously required the retired online backend.

Initial scope:

- Clan creation with a zero-cost T-Stone option
- Persistent clan id, name, level, EXP, fund, notice and introduction
- Local leader membership and permissions
- Clan search against the local clan
- Rename, notice/introduction updates and leaving the clan
- Empty offline alliance list

This is an offline single-profile clan. Network membership, real alliances,
clan chat, wars and shared multi-player storage are not simulated yet.
